using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Threading;
using ChestCounter;
using Forms = System.Windows.Forms;

namespace HopeChestCore;

public partial class MainWindow : Window
{
    private static readonly int OutfitOneIn = TreasureCatalog.Odds.Single(odds => odds.Kind == TreasureCatalog.GoldOutfit).OneIn;
    private static readonly int PirateShopOneIn = TreasureCatalog.Odds.Single(odds => odds.Kind == TreasureCatalog.PirateShop).OneIn;
    private readonly ReplayStore _store;
    private readonly ReplayMonitor _monitor;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(12) };
    private readonly CancellationTokenSource _stop = new();
    private readonly Forms.NotifyIcon _tray;
    private readonly System.Drawing.Icon _trayIcon;
    private readonly Forms.ContextMenuStrip _menu = new();
    private readonly AnimatedGif _brandAnimation;
    private CultureInfo _culture;
    private ResourceDictionary? _languageResources;
    private string? _statusKey;
    private object[] _statusArguments = [];
    private List<ReplayIssue> _issues = [];
    private string _folder;
    private bool _scanning;
    private bool _updatingPeriods;
    private MatchSummary[] _chartRows = [];

    private sealed record StatisticsPeriod(string Label, DateOnly? Day = null, bool IsToday = false);

    internal MainWindow(ReplayStore store)
    {
        _store = store;
        _monitor = new(store);
        _culture = UiText.For(store.GetLanguage());
        _folder = store.GetFolder() ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Demos");
        InitializeComponent();
        _menu.Items.Add("", null, (_, _) => RestoreWindow());
        _menu.Items.Add("", null, async (_, _) => await Scan(true));
        _menu.Items.Add("", null, (_, _) => Close());
        using var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/HopeChestCore;component/Assets/HopeChestCore.ico")).Stream;
        _trayIcon = new System.Drawing.Icon(iconStream, new System.Drawing.Size(32, 32));
        _tray = new() { Icon = _trayIcon, Text = "HopeChestCore", ContextMenuStrip = _menu };
        var animation = Application.GetResourceStream(new Uri("pack://application:,,,/HopeChestCore;component/Assets/HopeChestCore.gif")).Stream;
        _brandAnimation = new(BrandAnimation, animation);
        Activated += (_, _) => UpdateBrandAnimation();
        Deactivated += (_, _) => UpdateBrandAnimation();
        IsVisibleChanged += (_, _) => UpdateBrandAnimation();
        _tray.DoubleClick += (_, _) => RestoreWindow();
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                _tray.Visible = true;
                Hide();
            }
            UpdateBrandAnimation();
        };

        ApplyLanguage();
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
            _brandAnimation.Dispose();
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
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = Text("ChooseFolder") };
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
            SetStatus("FolderError", ex.Message);
        }
    }

    private async void ScanNow(object sender, RoutedEventArgs e) => await Scan(true);

    private void ResetStatistics(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, Text("ResetConfirm"), Text("Reset"),
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        try
        {
            _store.ClearHistory(DateTimeOffset.Now);
            _luckPeriods.Clear();
            RefreshHistory(force: true);
            SetStatus("ResetOk");
            ShowIssues([]);
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex)
        {
            MessageBox.Show(this, Text("ResetError", ex.Message),
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
        if (retry || _statusKey is null)
            SetStatus("Scanning");

        try
        {
            if (!Directory.Exists(folder))
            {
                RefreshHistory();
                SetStatus("MissingFolder");
                ShowIssues([]);
            }
            else
            {
                var result = await Task.Run(() => _monitor.Scan(folder, retry, _stop.Token));
                if (_stop.IsCancellationRequested)
                    return;

                RefreshHistory();
                if (result.Files == 0)
                    SetStatus("NoReplays");
                else if (result.Pending > 0)
                    SetStatus("PendingImports", result.Pending);
                else
                    SetStatus("ActiveImport");
                ShowIssues(result.Issues);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex)
        {
            SetStatus("ImportError", ex.Message);
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

    private void ShowIssues(List<ReplayIssue> issues)
    {
        _issues = issues;
        IssuePanel.Visibility = issues.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        IssuePanel.Header = Text("Issues", issues.Count);
        Issues.ItemsSource = issues.Select(issue => $"{issue.File} · {IssueText(issue)}").ToArray();
    }

    private string IssueText(ReplayIssue issue) => issue.MessageKey switch
    {
        "UnsupportedMode" => Text(issue.MessageKey, issue.Detail ?? Text("Unknown")),
        "ReplayReadError" => Text(issue.MessageKey, issue.Detail ?? ""),
        _ => Text(issue.MessageKey)
    };

    private void RefreshHistory(bool force = false)
    {
        var history = _store.ReadHistory();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var allDays = new StatisticsPeriod(Text("AllDays"));
        var selected = (StatisticsPeriod?)Days.SelectedItem ?? allDays;
        var periods = new[] { allDays, new StatisticsPeriod(Text("Today"), IsToday: true) }.Concat(history
            .Select(match => DateOnly.FromDateTime(match.PlayedAt.Date))
            .Distinct().Where(day => day != today)
            .Select(day => new StatisticsPeriod(day.ToString("ddd, " + _culture.DateTimeFormat.ShortDatePattern, _culture), day))).ToArray();
        if (Days.ItemsSource is not StatisticsPeriod[] existingPeriods || !existingPeriods.SequenceEqual(periods))
        {
            // When a date vanishes after a reset, select a period that still exists.
            selected = periods.FirstOrDefault(period => period.Day == selected.Day && period.IsToday == selected.IsToday) ?? allDays;
            _updatingPeriods = true;
            Days.ItemsSource = periods;
            Days.SelectedItem = selected;
            _updatingPeriods = false;
        }

        var day = selected.IsToday ? today : selected.Day;
        Period.Text = selected.IsToday ? $"{selected.Label} · {today.ToString("d", _culture)}" : selected.Label;
        var rows = day is null ? history : history.Where(match => DateOnly.FromDateTime(match.PlayedAt.Date) == day).ToList();
        if (!force && day == _luckDay && History.ItemsSource is List<MatchSummary> existing && existing.SequenceEqual(rows))
            return;

        History.ItemsSource = rows;
        HistoryEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var own = rows.Sum(item => item.OwnChests);
        var team = rows.Sum(item => (long)item.Squad);
        var total = own + team;
        RefreshLuck(day, total);
        var timed = rows.Where(item => item.DurationSeconds is > 0).ToArray();
        var seconds = timed.Sum(item => (long)item.DurationSeconds!.Value);

        TotalChests.Text = total.ToString("N0", _culture);
        OwnChests.Text = own.ToString("N0", _culture);
        Matches.Text = rows.Count.ToString("N0", _culture);
        Squad.Text = team.ToString("N0", _culture);
        ChanceChestCount.Text = Text("TotalCount", total);
        OutfitChance.Text = FormatChance(OutfitOneIn, total);
        PirateShopChance.Text = FormatChance(PirateShopOneIn, total);
        TotalPerMatch.Text = FormatRate(total, rows.Count, "N1");
        OwnPerMatch.Text = FormatRate(own, rows.Count, "N1");
        TotalPerMinute.Text = FormatRate(timed.Sum(item => item.TotalChests) * 60.0, seconds, "N2");
        OwnPerMinute.Text = FormatRate(timed.Sum(item => item.OwnChests) * 60.0, seconds, "N2");
        DurationHint.Text = rows.Count == timed.Length
            ? Text("RateDuration")
            : Text("SomeDurations", timed.Length, rows.Count);

        _chartRows = rows.Take(20).Reverse().ToArray();
        ChartRange.Text = _chartRows.Length switch
        {
            0 => "",
            1 => Text("OneMatch"),
            _ => Text("LastMatches", _chartRows.Length)
        };
        DrawCharts();
    }

    private void UpdateBrandAnimation() =>
        _brandAnimation.SetActive(IsActive && IsVisible && WindowState != WindowState.Minimized);

    private string Text(string key, params object[] arguments) => UiText.Get(_culture, key, arguments);

    private void SetStatus(string key, params object[] arguments)
    {
        _statusKey = key;
        _statusArguments = arguments;
        Status.Text = Text(key, arguments);
    }

    private void ChangeLanguage(object sender, RoutedEventArgs e)
    {
        var culture = UiText.For((string)((RadioButton)sender).Tag);
        if (_culture.Equals(culture))
            return;

        _culture = culture;
        ApplyLanguage();
        try { _store.SetLanguage(culture.Name); }
        catch (Microsoft.Data.Sqlite.SqliteException ex) { SetStatus("LanguageError", ex.Message); }
    }

    private void ApplyLanguage()
    {
        if (_languageResources is not null)
            Resources.MergedDictionaries.Remove(_languageResources);
        _languageResources = UiText.CreateResources(_culture);
        Resources.MergedDictionaries.Add(_languageResources);
        Language = XmlLanguage.GetLanguage(_culture.IetfLanguageTag);
        EnglishButton.IsChecked = _culture.Name == "en-GB";
        GermanButton.IsChecked = _culture.Name == "de-DE";
        _menu.Items[0].Text = Text("Open");
        _menu.Items[1].Text = Text("ScanNow");
        _menu.Items[2].Text = Text("Exit");
        OutfitBaseOdds.Text = Text("BaseOdds", OutfitOneIn);
        PirateShopBaseOdds.Text = Text("BaseOdds", PirateShopOneIn);
        var columns = ((GridView)History.View).Columns;
        string[] headers = ["DateColumn", "PlayerColumn", "ModeColumn", "TotalColumn", "OwnColumn",
            "TeamColumn", "LengthColumn", "TotalRateColumn", "OwnRateColumn", "NoteColumn"];
        for (var index = 0; index < headers.Length; index++)
            columns[index].Header = Text(headers[index]);
        columns[0].DisplayMemberBinding = new Binding(nameof(MatchSummary.PlayedAt))
        {
            StringFormat = "g", ConverterCulture = _culture
        };
        if (_statusKey is not null)
            Status.Text = Text(_statusKey, _statusArguments);
        ShowIssues(_issues);
        RefreshHistory(force: true);
    }

    private string FormatRate(double chests, long duration, string format) =>
        duration > 0 ? (chests / duration).ToString(format, _culture) : "–";

    private string FormatChance(int oneIn, long chests)
    {
        var chance = TreasureCatalog.AtLeastOnce(oneIn, chests);
        if (chance >= 0.9999995)
            return "< 100 %";

        return chance.ToString(chests == 0 ? "P0" : "P4", _culture);
    }
}
