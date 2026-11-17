using System.Security.Cryptography;
using ChestCounter;

namespace HopeChestCore;

internal sealed record MatchSummary(string Key, DateTimeOffset PlayedAt, string Player,
    string Mode, string ReplayFile, int Opened, int Destroyed, int Squad, bool VehicleEventsMissing, int? DurationSeconds)
{
    public long OwnChests => (long)Opened + Destroyed;
    public long TotalChests => OwnChests + Squad;
    public double? OwnPerMinute => DurationSeconds is > 0 ? OwnChests * 60.0 / DurationSeconds : null;
    public double? TotalPerMinute => DurationSeconds is > 0 ? TotalChests * 60.0 / DurationSeconds : null;
    public string DurationLabel => DurationSeconds is > 0 ? TimeSpan.FromSeconds(DurationSeconds.Value).ToString(@"m\:ss") : "–";
}

internal sealed record AnalysisResult(string State, string MessageKey, string? Detail = null,
    MatchSummary? Match = null, string? ReplayHash = null);

internal static class ReplayAnalysis
{
    private enum HeaderStatus { Finished, RecordingPending, InvalidHeader, UnknownHeader }

    public static AnalysisResult Read(string path)
    {
        using var replay = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        var header = ReadHeader(replay);
        if (header != HeaderStatus.Finished)
            return new(header == HeaderStatus.RecordingPending ? "pending" : "error", header.ToString());

        var item = new ReplayAnalyzer().Analyze(replay, path, username: null);
        if (!item.IsReplayOwner)
            return new("error", "OwnerMissing");

        if (item.IsCustomMatch || !GameModes.IsBattleRoyalePlaylist(item.Playlist))
            return new("skipped", "UnsupportedMode", item.Playlist);

        var sourceName = Path.GetFileName(path);
        // The upstream parser uses the timestamp and filename as a fallback session ID.
        var sessionKey = item.SessionId;
        replay.Position = 0;
        var replayHash = Convert.ToHexStringLower(SHA256.HashData(replay));
        if (string.IsNullOrWhiteSpace(sessionKey) || sessionKey.EndsWith("-" + sourceName, StringComparison.Ordinal))
            sessionKey = replayHash;

        var match = new MatchSummary(
            sessionKey.Trim().ToLowerInvariant(),
            item.MatchStartUtc is { } started ? new DateTimeOffset(DateTime.SpecifyKind(started, DateTimeKind.Utc))
                : new DateTimeOffset(File.GetLastWriteTimeUtc(path)),
            item.PlayerName ?? "Replay-Spieler",
            item.Playlist!.StartsWith("Playlist_NoBuild", StringComparison.OrdinalIgnoreCase) ? "Zero Build" : "Battle Royale",
            sourceName,
            item.Chests.Count(chest => !chest.Destroyed),
            item.Chests.Count(chest => chest.Destroyed),
            item.TeamChestsOpened,
            item.VehicleChestsMissing,
            item.DurationSeconds);
        return new("counted", "", Match: match, ReplayHash: replayHash);
    }

    private static HeaderStatus ReadHeader(Stream replay)
    {
        using var header = new BinaryReader(replay, System.Text.Encoding.UTF8, leaveOpen: true);
        if (header.ReadUInt32() != 0x1CA2E27F)
            return HeaderStatus.InvalidHeader;

        var version = header.ReadUInt32();
        if (version >= 7)
        {
            var versions = header.ReadInt32();
            if (versions is < 0 or > 64)
                return HeaderStatus.UnknownHeader;
            replay.Seek(versions * 20L, SeekOrigin.Current);
        }

        replay.Seek(12, SeekOrigin.Current);
        var nameLength = header.ReadInt32();
        replay.Seek(nameLength < 0 ? -2L * nameLength : nameLength, SeekOrigin.Current);
        var live = header.ReadUInt32() != 0;
        if (!live)
        {
            replay.Position = 0;
            return HeaderStatus.Finished;
        }

        // Some finished captures keep the live flag. A nonzero key is written at match end.
        if (version < 6)
            return HeaderStatus.RecordingPending;

        header.ReadInt64();
        header.ReadUInt32();
        if (header.ReadUInt32() == 0)
            return HeaderStatus.RecordingPending;

        var keyLength = header.ReadInt32();
        if (keyLength is <= 0 or > 256)
            return HeaderStatus.RecordingPending;

        var key = header.ReadBytes(keyLength);
        replay.Position = 0;
        return key.Length == keyLength && key.Any(value => value != 0) ? HeaderStatus.Finished : HeaderStatus.RecordingPending;
    }
}
