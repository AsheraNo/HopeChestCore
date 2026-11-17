using System.Text.Json;
using System.Windows;

namespace HopeChestCore;

internal sealed record LuckFact(double OneIn, string En, string De, string ConditionEn, string ConditionDe, string Kind);

internal static class LuckFacts
{
    private sealed record Catalog(LuckFact[] Items);

    private static readonly Lazy<(double Threshold, LuckFact Fact)[]> Facts = new(() =>
    {
        using var stream = Application.GetResourceStream(
            new Uri("pack://application:,,,/HopeChestCore;component/Assets/LuckFacts.json")).Stream;
        var catalog = JsonSerializer.Deserialize<Catalog>(stream, JsonSerializerOptions.Web);
        return (catalog?.Items ?? [])
            .Where(fact => double.IsFinite(fact.OneIn) && fact.OneIn >= 1)
            .OrderByDescending(fact => fact.OneIn)
            .Select(fact => (Math.Log(fact.OneIn), fact))
            .ToArray();
    });

    public static LuckFact? Reached(double logDenominator) =>
        Facts.Value.FirstOrDefault(item => item.Threshold <= logDenominator).Fact;
}
