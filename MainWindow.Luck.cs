using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ChestCounter;

namespace HopeChestCore;

public partial class MainWindow
{
    private readonly Dictionary<LuckPeriodKey, LuckPeriod> _luckPeriods = new();
    private LuckPeriod? _luckPeriod;
    private DateOnly? _luckDay;
    private long _luckChests;

    private readonly record struct LuckPeriodKey(DateOnly? Day);
    private sealed record SavedBoost(string Count, string? Multiplier);
    private sealed record SavedPeriod(Dictionary<string, string> Regular, Dictionary<string, SavedBoost[]> Boosts);

    private sealed record LuckPeriod(LuckEntry[] Regular, BoostGroup[] Boosts)
    {
        public IEnumerable<LuckEntry> Entries => Regular.Concat(Boosts.SelectMany(group => group.Rows));
    }

    private sealed record BoostGroup(LuckEntry Reward, ObservableCollection<LuckEntry> Rows);

    private void ChangeView(object sender, RoutedEventArgs e)
    {
        var showLuck = LuckTab.IsChecked == true;
        LuckPane.Visibility = showLuck ? Visibility.Visible : Visibility.Collapsed;
        DashboardPane.Visibility = showLuck ? Visibility.Collapsed : Visibility.Visible;
        if (!showLuck)
            DrawCharts();
    }

    private void AddLuckFind(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is LuckEntry entry && TryCount(entry.Count, out var count) && count < long.MaxValue)
            entry.Count = (count + 1).ToString(CultureInfo.InvariantCulture);
    }

    private void AddBoost(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is BoostGroup group)
        {
            group.Rows.Add(new LuckEntry(group.Reward.Odds, LuckChanged, isBoosted: true));
            LuckChanged();
        }
    }

    private void RemoveBoost(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).DataContext is LuckEntry entry && _luckPeriod is { } period)
        {
            period.Boosts.First(group => group.Rows.Contains(entry)).Rows.Remove(entry);
            LuckChanged();
        }
    }

    private string CurrentLuckKey => _luckDay?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "all";

    private void LuckChanged()
    {
        RefreshLuckValues();
        if (_luckPeriod is not { } period)
            return;

        var saved = new SavedPeriod(
            period.Regular.ToDictionary(entry => entry.Odds.Kind, entry => entry.Count),
            period.Boosts.ToDictionary(group => group.Reward.Odds.Kind, group => group.Rows.Select(entry => entry.SaveBoost()).ToArray()));
        try { _store.SetLuck(CurrentLuckKey, JsonSerializer.Serialize(saved)); }
        catch (Microsoft.Data.Sqlite.SqliteException ex) { SetStatus("LuckSaveFailed", ex.Message); }
    }

    private LuckPeriod CreateLuckPeriod()
    {
        var json = _store.GetLuck(CurrentLuckKey);
        var saved = json is null ? null : JsonSerializer.Deserialize<SavedPeriod>(json);
        var regular = TreasureCatalog.Odds.Select(odds => new LuckEntry(odds, LuckChanged,
            count: saved?.Regular.GetValueOrDefault(odds.Kind) ?? "")).ToArray();
        var examples = new Dictionary<string, double>
        {
            [TreasureCatalog.GoldOutfit] = 7.5,
            [TreasureCatalog.PirateShop] = 1.5,
            [TreasureCatalog.VehicleDecal] = 10
        };
        var boostGroups = regular.Where(entry => examples.ContainsKey(entry.Odds.Kind))
            .Select(entry =>
            {
                var rows = saved?.Boosts.GetValueOrDefault(entry.Odds.Kind);
                var example = examples[entry.Odds.Kind];
                if (rows is null)
                    return new BoostGroup(entry, [new LuckEntry(entry.Odds, LuckChanged, isBoosted: true, example: example)]);

                return new BoostGroup(entry, new ObservableCollection<LuckEntry>(rows.Select(row =>
                    new LuckEntry(entry.Odds, LuckChanged, isBoosted: true, example: example, count: row.Count, multiplier: row.Multiplier))));
            })
            .ToArray();
        return new LuckPeriod(regular, boostGroups);
    }

    private void RefreshLuck(DateOnly? day, long chests)
    {
        _luckDay = day;
        _luckChests = chests;
        var key = new LuckPeriodKey(day);
        if (!_luckPeriods.TryGetValue(key, out var period))
            _luckPeriods[key] = period = CreateLuckPeriod();

        if (!ReferenceEquals(_luckPeriod, period))
        {
            _luckPeriod = period;
            LuckEntries.ItemsSource = period.Regular;
            TreasureEntries.ItemsSource = period.Boosts;
        }

        RefreshLuckValues();
    }

    private void RefreshLuckValues()
    {
        if (_luckPeriod is not { } period)
            return;

        var allEntries = period.Entries.ToArray();
        foreach (var entry in allEntries)
            entry.Update(_luckChests, _culture);

        var invalid = allEntries.Any(entry => entry.HasError);
        var tooManyTogether = allEntries.GroupBy(entry => entry.Odds.Kind).Any(group =>
            group.Aggregate((Int128)0, (sum, entry) => sum + entry.FindCount) > _luckChests);
        var included = allEntries.Where(entry => entry.LogDenominator.HasValue).ToArray();
        LuckSummaryValue.Text = invalid || tooManyTogether || included.Length == 0 ? "—"
            : FormatOdds(included.Sum(entry => entry.LogDenominator!.Value), _culture);
        LuckSummaryHint.Text = invalid ? Text("InvalidCombinedFinds")
            : tooManyTogether ? Text("TooManyCombinedFinds")
            : included.Length == 0 ? Text("NoCombinedFinds") : "";
    }

    private static bool TryCount(string text, out long count)
    {
        count = 0;
        return string.IsNullOrWhiteSpace(text)
            || long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out count);
    }

    private static string FormatOdds(double logDenominator, CultureInfo culture)
    {
        var logTen = Math.Log(10);
        var exponent = Math.Floor(logDenominator / logTen);
        if (exponent >= 9)
        {
            var mantissa = Math.Exp(logDenominator - exponent * logTen);
            var display = $"{mantissa.ToString("0.##", culture)} × 10^{exponent.ToString("N0", culture)}";
            return UiText.Get(culture, "RoundedLuck", display);
        }

        var denominator = Math.Exp(logDenominator);
        var format = denominator < 100 ? "0.##" : "N0";
        return UiText.Get(culture, "RoundedLuck", denominator.ToString(format, culture));
    }

    private sealed class LuckEntry(TreasureOdds odds, Action refresh, bool isBoosted = false, double? example = null,
        string count = "", string? multiplier = null) : INotifyPropertyChanged
    {
        private string _count = count;
        private string? _multiplier = multiplier;
        private string _example = "";
        private BitmapImage? _icon;
        public TreasureOdds Odds { get; } = odds;
        public BitmapImage Icon => _icon ??= new(new Uri($"pack://application:,,,/HopeChestCore;component/Assets/Rewards/{Odds.Kind}.png"));
        public string Label { get; private set; } = "";
        public string BaseOdds { get; private set; } = "";
        public string Result { get; private set; } = "";
        public bool HasError { get; private set; }
        public long FindCount { get; private set; }
        public double? LogDenominator { get; private set; }

        public string Count
        {
            get => _count;
            set
            {
                if (_count == value)
                    return;
                _count = value;
                refresh();
            }
        }

        public string Multiplier
        {
            get => _multiplier ?? _example;
            set
            {
                if (value == Multiplier)
                    return;
                _multiplier = value;
                refresh();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public SavedBoost SaveBoost() => new(Count, _multiplier ?? _example);

        public void Update(long chests, CultureInfo culture)
        {
            _example = example?.ToString("0.##", culture) ?? "";
            Label = UiText.Get(culture, Odds.Kind);
            BaseOdds = UiText.Get(culture, "BaseOdds", Odds.OneIn);
            LogDenominator = null;
            var canParse = TryCount(Count, out var count);
            FindCount = count;
            var multiplier = 1.0;
            var validMultiplier = !isBoosted || count == 0 ||
                (double.TryParse(Multiplier.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out multiplier)
                    && multiplier >= 1 && multiplier <= Odds.OneIn);
            var error = !canParse ? UiText.Get(culture, "LuckInput")
                : count > chests ? UiText.Get(culture, "TooManyFinds")
                : !validMultiplier ? UiText.Get(culture, "InvalidMultiplier", Odds.OneIn)
                : "";
            HasError = error.Length != 0;
            if (!HasError && count > 0)
            {
                LogDenominator = isBoosted
                    ? LuckOdds.LogDenominatorForChance(chests, count, multiplier / Odds.OneIn)
                    : LuckOdds.LogDenominator(chests, count, Odds.OneIn);
            }

            Result = HasError ? error : LogDenominator is { } log ? FormatOdds(log, culture) : "—";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }
}
