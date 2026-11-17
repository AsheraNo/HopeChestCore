using System.Globalization;
using Microsoft.Data.Sqlite;

namespace HopeChestCore;

internal sealed class ReplayStore
{
    private readonly string _connectionString;
    // Revisit existing files to record a content hash for each replay still available.
    private const int ScanVersion = ChestCounter.ReplayAnalyzer.CurrentVersion * 100 + 2;

    public ReplayStore(string path)
    {
        SQLitePCL.Batteries_V2.Init();
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS matches (
                id TEXT PRIMARY KEY, played_at TEXT NOT NULL, player TEXT NOT NULL,
                mode TEXT NOT NULL, replay TEXT NOT NULL, opened INTEGER NOT NULL,
                destroyed INTEGER NOT NULL, squad INTEGER NOT NULL, vehicle_missing INTEGER NOT NULL,
                duration_seconds INTEGER
            );
            CREATE TABLE IF NOT EXISTS scanned_files (
                fingerprint TEXT PRIMARY KEY, parser_version INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS settings (
                name TEXT PRIMARY KEY, value TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        command.CommandText = "SELECT count(*) FROM pragma_table_info('matches') WHERE name = 'duration_seconds'";
        if ((long)command.ExecuteScalar()! == 0)
        {
            command.CommandText = "ALTER TABLE matches ADD COLUMN duration_seconds INTEGER";
            command.ExecuteNonQuery();
        }

        using var migration = connection.BeginTransaction();
        command.Transaction = migration;
        command.CommandText = "PRAGMA user_version";
        if ((long)command.ExecuteScalar()! == 0)
        {
            command.CommandText = """
                ALTER TABLE matches ADD COLUMN replay_hash TEXT;
                CREATE UNIQUE INDEX matches_replay_hash ON matches(replay_hash);
                WITH ranked AS (
                    SELECT id, row_number() OVER (
                        PARTITION BY lower(trim(substr(id, instr(id, '/') + 1)))
                        ORDER BY duration_seconds IS NOT NULL DESC, played_at DESC, id) AS position
                    FROM matches
                )
                DELETE FROM matches WHERE id IN (SELECT id FROM ranked WHERE position > 1);
                UPDATE matches SET id = lower(trim(substr(id, instr(id, '/') + 1)));
                DELETE FROM settings WHERE name = 'statistics_start';
                PRAGMA user_version = 1;
                """;
            command.ExecuteNonQuery();
        }
        migration.Commit();
    }

    public string? GetFolder() => GetSetting("replay_folder");
    public void SetFolder(string path) => SetSetting("replay_folder", path);
    public string? GetLanguage() => GetSetting("language");
    public void SetLanguage(string code) => SetSetting("language", code);
    public string? GetLuck(string period) => GetSetting("luck:" + period);
    public void SetLuck(string period, string data) => SetSetting("luck:" + period, data);

    private string? GetSetting(string name)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM settings WHERE name = $name";
        command.Parameters.AddWithValue("$name", name);
        return command.ExecuteScalar() as string;
    }

    private void SetSetting(string name, string value)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO settings VALUES ($name, $value)";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    public HashSet<string> ReadScannedFiles()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT fingerprint FROM scanned_files WHERE parser_version = $version";
        command.Parameters.AddWithValue("$version", ScanVersion);
        using var rows = command.ExecuteReader();
        var fingerprints = new HashSet<string>(StringComparer.Ordinal);
        while (rows.Read())
            fingerprints.Add(rows.GetString(0));
        return fingerprints;
    }

    public void Save(string fingerprint, MatchSummary match, string? replayHash)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT value FROM settings WHERE name = 'history_reset_at'";
        var resetAt = command.ExecuteScalar() as string;
        var playedAt = ToUtcText(match.PlayedAt);
        if (resetAt is null || string.CompareOrdinal(playedAt, resetAt) >= 0)
        {
            command.CommandText = """
                DELETE FROM matches WHERE replay_hash = $hash AND id <> $id;
                INSERT INTO matches (id, played_at, player, mode, replay, opened, destroyed, squad, vehicle_missing, duration_seconds, replay_hash)
                VALUES ($id, $time, $player, $mode, $replay, $opened, $destroyed, $squad, $missing, $duration, $hash)
                ON CONFLICT(id) DO UPDATE SET played_at = excluded.played_at, player = excluded.player,
                    mode = excluded.mode, replay = excluded.replay, opened = excluded.opened,
                    destroyed = excluded.destroyed, squad = excluded.squad, vehicle_missing = excluded.vehicle_missing,
                    duration_seconds = COALESCE(excluded.duration_seconds, matches.duration_seconds),
                    replay_hash = COALESCE(excluded.replay_hash, matches.replay_hash)
                """;
            command.Parameters.AddWithValue("$id", match.Key);
            command.Parameters.AddWithValue("$time", playedAt);
            command.Parameters.AddWithValue("$hash", (object?)replayHash ?? DBNull.Value);
            command.Parameters.AddWithValue("$player", match.Player);
            command.Parameters.AddWithValue("$mode", match.Mode);
            command.Parameters.AddWithValue("$replay", match.ReplayFile);
            command.Parameters.AddWithValue("$opened", match.Opened);
            command.Parameters.AddWithValue("$destroyed", match.Destroyed);
            command.Parameters.AddWithValue("$squad", match.Squad);
            command.Parameters.AddWithValue("$missing", match.VehicleEventsMissing);
            command.Parameters.AddWithValue("$duration", (object?)match.DurationSeconds ?? DBNull.Value);
            command.ExecuteNonQuery();
            command.Parameters.Clear();
        }
        command.CommandText = "INSERT OR REPLACE INTO scanned_files VALUES ($file, $version)";
        command.Parameters.AddWithValue("$file", fingerprint);
        command.Parameters.AddWithValue("$version", ScanVersion);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void ClearHistory(DateTimeOffset resetAt)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA secure_delete = ON";
        command.ExecuteNonQuery();
        using var transaction = connection.BeginTransaction();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM matches;
            DELETE FROM scanned_files;
            DELETE FROM settings WHERE name LIKE 'luck:%';
            INSERT OR REPLACE INTO settings VALUES ('history_reset_at', $resetAt);
            """;
        command.Parameters.AddWithValue("$resetAt", ToUtcText(resetAt));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public List<MatchSummary> ReadHistory()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, played_at, player, mode, replay, opened, destroyed, squad, vehicle_missing, duration_seconds
            FROM matches ORDER BY played_at DESC, id
            """;
        using var rows = command.ExecuteReader();
        var matches = new List<MatchSummary>();
        while (rows.Read())
        {
            var time = DateTimeOffset.Parse(rows.GetString(1), CultureInfo.InvariantCulture).ToLocalTime();
            matches.Add(new(rows.GetString(0), time, rows.GetString(2), rows.GetString(3), rows.GetString(4),
                rows.GetInt32(5), rows.GetInt32(6), rows.GetInt32(7), rows.GetBoolean(8),
                rows.IsDBNull(9) ? null : rows.GetInt32(9)));
        }
        return matches;
    }

    private static string ToUtcText(DateTimeOffset time) => time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
