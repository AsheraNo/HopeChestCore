using System.Text.Json;
using System.Windows;

namespace HopeChestCore;

public partial class App : Application
{
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args is ["--analyze-replay", var input, var output])
        {
            AnalysisResult result;
            try
            {
                result = ReplayAnalysis.Read(input);
            }
            catch (IOException ex) when ((ex.HResult & 0xffff) is 2 or 3 or 32 or 33)
            {
                result = new("pending", "Replay-Datei wird noch von Fortnite verarbeitet.");
            }
            catch (Exception ex)
            {
                result = new("error", "Replay konnte nicht gelesen werden: " + ex.Message);
            }

            File.WriteAllText(output, JsonSerializer.Serialize(result));
            Shutdown();
            return;
        }

        var dataRoot = e.Args is ["--data-dir", var directory]
            ? Path.GetFullPath(directory)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HopeChestCore");
        var directoryKey = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(dataRoot.ToUpperInvariant())));
        _mutex = new Mutex(true, @"Local\HopeChestCore-" + directoryKey, out var first);
        if (!first)
        {
            MessageBox.Show("HopeChestCore läuft bereits. Das Fenster lässt sich über das Symbol im Infobereich öffnen.", "HopeChestCore");
            Shutdown();
            return;
        }

        try
        {
            Directory.CreateDirectory(dataRoot);
            MainWindow = new MainWindow(new ReplayStore(Path.Combine(dataRoot, "history.db")));
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show("HopeChestCore konnte nicht gestartet werden.\n\n" + ex.Message, "HopeChestCore", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
