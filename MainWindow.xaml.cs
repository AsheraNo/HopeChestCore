using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ChestCounter;
using Forms = System.Windows.Forms;

namespace HopeChestCore;

public partial class MainWindow : Window
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private static readonly int OutfitOneIn = TreasureCatalog.Odds.Single(odds => odds.Kind == TreasureCatalog.GoldOutfit).OneIn;
    private static readonly int PirateShopOneIn = TreasureCatalog.Odds.Single(odds => odds.Kind == TreasureCatalog.PirateShop).OneIn;
    private readonly ReplayStore _store;
    private readonly ReplayMonitor _monitor;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(12) };
    private readonly CancellationTokenSource _stop = new();
    private readonly Forms.NotifyIcon _tray;
    private readonly System.Drawing.Icon _trayIcon;
    private readonly Forms.ContextMenuStrip _menu = new();
    private string _folder;
    private bool _scanning;
    private static readonly StatisticsPeriod AllDays = new("Alle Tage");
    private static readonly StatisticsPeriod Today = new("Heute");
    private bool _updatingPeriods;
    private MatchSummary[] _chartRows = [];

    private sealed record StatisticsPeriod(string Label, DateOnly? Day = null);

    internal MainWindow(ReplayStore store)
    {
        _store = store;
        _monitor = new(store);
        _folder = store.GetFolder() ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Demos");
        InitializeComponent();
        OutfitBaseOdds.Text = $"1 : {OutfitOneIn.ToString("N0", German)} pro Truhe";
        PirateShopBaseOdds.Text = $"1 : {PirateShopOneIn.ToString("N0", German)} pro Truhe";
        _menu.Items.Add("Öffnen", null, (_, _) => RestoreWindow());
        _menu.Items.Add("Jetzt prüfen", null, async (_, _) => await Scan(true));
        _menu.Items.Add("Beenden", null, (_, _) => Close());
        using var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/HopeChestCore;component/Assets/HopeChestCore.ico")).Stream;
        _trayIcon = new System.Drawing.Icon(iconStream, new System.Drawing.Size(32, 32));
        _tray = new() { Icon = _trayIcon, Text = "HopeChestCore", ContextMenuStrip = _menu };
        _tray.DoubleClick += (_, _) => RestoreWindow();
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                _tray.Visible = true;
                Hide();
            }
        };

        RefreshHistory();
        Loaded += async (_, _) =>
        {
            _timer.Start();
            await Scan();
        };
        _timer.Tick += async (_, _) => await Scan();
        Closed += (_, _) =>
        {
            _timer.Stop();
            _stop.Cancel();
            _tray.Dispose();
            _trayIcon.Dispose();
            _menu.Dispose();
        };
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        _tray.Visible = false;
        Activate();
    }

    private async void ChooseFolder(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Ordner mit Fortnite-Replays auswählen" };
        if (Directory.Exists(_folder))
            picker.InitialDirectory = _folder;
        if (picker.ShowDialog(this) != true)
            return;

        try
        {
            _store.SetFolder(picker.FolderName);
            _folder = picker.FolderName;
            await Scan(true);
        }
        catch (Exception ex)
        {
            Status.Text = "Ordner konnte nicht gespeichert werden. " + ex.Message;
        }
    }

    private async void ScanNow(object sender, RoutedEventArgs e) => await Scan(true);

    private void ResetStatistics(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this,
            "Alle gespeicherten Matches und Import-Einträge endgültig löschen? HopeChestCore zählt danach Matches ab dem Reset. Deine Replay-Dateien bleiben auf der Festplatte.",
            "Statistik zurücksetzen", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        try
        {
            _store.ClearHistory(DateTimeOffset.Now);
            RefreshHistory();
            Status.Text = "Verlauf gelöscht. Warte auf neue Matches.";
            ShowIssues([]);
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
        {
            MessageBox.Show(this, "Die Statistik konnte nicht zurückgesetzt werden.\n\n" + ex.Message,
                "HopeChestCore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ChangeStatisticsPeriod(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingPeriods)
            RefreshHistory();
    }

    private async Task Scan(bool retry = false)
    {
        if (_scanning || _stop.IsCancellationRequested)
            return;

        _scanning = true;
        ScanButton.IsEnabled = false;
        ResetButton.IsEnabled = false;
        var folder = _folder;
        Folder.Text = folder;
        Folder.ToolTip = folder;
        if (retry || Status.Text.Length == 0)
            Status.Text = "Replays werden geprüft …";

        try
        {
            if (!Directory.Exists(folder))
            {
                RefreshHistory();
                Status.Text = "Replay-Ordner fehlt. Aktiviere in Fortnite „Replays aufzeichnen“ oder wähle deinen Ordner.";
                ShowIssues([]);
            }
            else
            {
                var result = await Task.Run(() => _monitor.Scan(folder, retry, _stop.Token));
                if (_stop.IsCancellationRequested)
                    return;

                RefreshHistory();
                Status.Text = result switch
                {
                    { Files: 0 } => "Keine Replays gefunden. Aktiviere in Fortnite „Replays aufzeichnen“ und beende ein Match.",
                    { Pending: > 0 } => $"Automatischer Import aktiv · {result.Pending} Aufnahme(n) warten auf Abschluss.",
                    _ => "Automatischer Import aktiv · Der Replay-Ordner wird alle 12 Sekunden geprüft."
                };
                ShowIssues(result.Issues);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Status.Text = "Import unterbrochen. " + ex.Message;
        }
        finally
        {
            _scanning = false;
            ScanButton.IsEnabled = true;
            ResetButton.IsEnabled = true;
        }

        if (folder != _folder)
            await Scan(true);
    }

    private void ShowIssues(List<string> issues)
    {
        IssuePanel.Visibility = issues.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        IssuePanel.Header = $"Übersprungene Replays ({issues.Count})";
        Issues.ItemsSource = issues;
    }

    private void RefreshHistory()
    {
        var history = _store.ReadHistory();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var selected = (StatisticsPeriod?)Days.SelectedItem ?? AllDays;
        var periods = new[] { AllDays, Today }.Concat(history
            .Select(match => DateOnly.FromDateTime(match.PlayedAt.Date))
            .Distinct().Where(day => day != today)
            .Select(day => new StatisticsPeriod(day.ToString("ddd, dd.MM.yyyy", German), day))).ToArray();
        if (Days.ItemsSource is not StatisticsPeriod[] existingPeriods || !existingPeriods.SequenceEqual(periods))
        {
            // When a date vanishes after a reset, select a period that still exists.
            selected = periods.Contains(selected) ? selected : AllDays;
            _updatingPeriods = true;
            Days.ItemsSource = periods;
            Days.SelectedItem = selected;
            _updatingPeriods = false;
        }

        var day = selected == Today ? today : selected.Day;
        Period.Text = selected == Today ? $"Heute · {today:dd.MM.yyyy}" : selected.Label;
        var rows = day is null ? history : history.Where(match => DateOnly.FromDateTime(match.PlayedAt.Date) == day).ToList();
        if (History.ItemsSource is List<MatchSummary> existing && existing.SequenceEqual(rows))
            return;

        History.ItemsSource = rows;
        HistoryEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var own = rows.Sum(item => item.OwnChests);
        var team = rows.Sum(item => (long)item.Squad);
        var total = own + team;
        var timed = rows.Where(item => item.DurationSeconds is > 0).ToArray();
        var seconds = timed.Sum(item => (long)item.DurationSeconds!.Value);

        var totalLabel = total.ToString("N0", German);
        TotalChests.Text = totalLabel;
        OwnChests.Text = own.ToString("N0", German);
        Matches.Text = rows.Count.ToString("N0", German);
        Squad.Text = team.ToString("N0", German);
        ChanceChestCount.Text = $"{totalLabel} Truhen gesamt";
        OutfitChance.Text = FormatChance(OutfitOneIn, total);
        PirateShopChance.Text = FormatChance(PirateShopOneIn, total);
        TotalPerMatch.Text = FormatRate(total, rows.Count, "N1");
        OwnPerMatch.Text = FormatRate(own, rows.Count, "N1");
        TotalPerMinute.Text = FormatRate(timed.Sum(item => item.TotalChests) * 60.0, seconds, "N2");
        OwnPerMinute.Text = FormatRate(timed.Sum(item => item.OwnChests) * 60.0, seconds, "N2");
        DurationHint.Text = rows.Count == timed.Length
            ? "Berechnung pro Minute anhand der Replay-Dauer"
            : $"Replay-Dauer verfügbar für {timed.Length} von {rows.Count} Matches";

        _chartRows = rows.Take(20).Reverse().ToArray();
        ChartRange.Text = _chartRows.Length switch
        {
            0 => "",
            1 => "1 Match",
            _ => $"Letzte {_chartRows.Length} Matches"
        };
        DrawCharts();
    }

    private static string FormatRate(double chests, long duration, string format) =>
        duration > 0 ? (chests / duration).ToString(format, German) : "–";

    private static string FormatChance(int oneIn, long chests)
    {
        var chance = TreasureCatalog.AtLeastOnce(oneIn, chests);
        if (chance >= 0.9999995)
            return "< 100 %";

        return chance.ToString(chests == 0 ? "P0" : "P4", German);
    }
}
