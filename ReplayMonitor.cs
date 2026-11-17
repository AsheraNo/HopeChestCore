using System.Diagnostics;
using System.Text.Json;

namespace HopeChestCore;

internal sealed record ReplayIssue(string File, string MessageKey, string? Detail);

internal sealed class ReplayMonitor(ReplayStore store)
{
    private readonly Dictionary<string, ReplayIssue> _blocked = new();

    public async Task<(int Files, int Pending, List<ReplayIssue> Issues)> Scan(string folder, bool retry, CancellationToken token)
    {
        if (retry)
            _blocked.Clear();

        var scanned = store.ReadScannedFiles();
        var files = new DirectoryInfo(folder).GetFiles("*.replay");
        var active = new HashSet<string>();
        var pending = 0;

        foreach (var file in files.OrderByDescending(file => file.LastWriteTimeUtc))
        {
            token.ThrowIfCancellationRequested();
            var key = Fingerprint(file);
            if (key == null)
                continue;

            active.Add(key);
            if (scanned.Contains(key) || _blocked.ContainsKey(key))
                continue;

            if (DateTime.UtcNow - file.LastWriteTimeUtc < TimeSpan.FromSeconds(8))
            {
                pending++;
                continue;
            }

            var result = await Analyze(file.FullName, token);
            file.Refresh();
            if (Fingerprint(file) != key || result.State == "pending")
            {
                pending++;
                continue;
            }

            if (result.Match is { } match)
                store.Save(key, match, result.ReplayHash);
            else
                _blocked[key] = new(file.Name, result.MessageKey, result.Detail);
        }

        foreach (var key in _blocked.Keys.Where(key => !active.Contains(key)).ToArray())
            _blocked.Remove(key);

        return (files.Length, pending, _blocked.Values.ToList());
    }

    private static string? Fingerprint(FileInfo file)
    {
        try
        {
            return file.Exists ? $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}" : null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static async Task<AnalysisResult> Analyze(string replay, CancellationToken token)
    {
        var output = Path.GetTempFileName();
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--analyze-replay");
            start.ArgumentList.Add(replay);
            start.ArgumentList.Add(output);
            using var worker = Process.Start(start);
            if (worker is null)
                return new("error", "ParserStart");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            try
            {
                await worker.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { worker.Kill(); }
                catch (InvalidOperationException) { }
                await worker.WaitForExitAsync();
                token.ThrowIfCancellationRequested();
                return new("error", "ParserTimeout");
            }

            if (worker.ExitCode != 0 || new FileInfo(output).Length == 0)
                return new("error", "ParserExit");

            return JsonSerializer.Deserialize<AnalysisResult>(await File.ReadAllTextAsync(output, token))
                ?? new("error", "ParserEmpty");
        }
        finally
        {
            File.Delete(output);
        }
    }
}
