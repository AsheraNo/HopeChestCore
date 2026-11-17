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
                result = new("pending", "RecordingPending");
            }
            catch (Exception ex)
            {
                result = new("error", "ReplayReadError", ex.Message);
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
        var culture = UiText.For(null);
        _mutex = new Mutex(true, @"Local\HopeChestCore-" + directoryKey, out var first);
        if (!first)
        {
            MessageBox.Show(UiText.Get(culture, "AlreadyOpen"), "HopeChestCore");
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
            MessageBox.Show(UiText.Get(culture, "StartupError", ex.Message), "HopeChestCore", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
