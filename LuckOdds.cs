namespace HopeChestCore;

internal static class LuckOdds
{
    public static double LogDenominator(long chests, long finds, int oneIn) =>
        LogDenominatorForChance(chests, finds, 1.0 / oneIn);

    // Returns -log(P(X >= finds)) for independent draws at a fixed chance per chest.
    public static double LogDenominatorForChance(long chests, long finds, double p)
    {
        if (finds <= 0)
            return 0;
        if (finds > chests)
            return double.PositiveInfinity;
        if (p == 1)
            return 0;
        if (finds == 1)
            return -Math.Log(-double.ExpM1(chests * double.LogP1(-p)));

        var odds = p / (1 - p);
        // Start at the end of the smaller tail nearest the mode. Normalize its first term
        // to 1 so the sum can be evaluated even when the final probability would underflow.
        var upperTail = finds > chests * p;
        var start = upperTail ? finds : finds - 1;
        var sum = 1.0;
        var term = 1.0;
        for (var k = start; upperTail ? k < chests : k > 0; k += upperTail ? 1 : -1)
        {
            term *= upperTail
                ? ((double)(chests - k) / (k + 1) * odds)
                : (k / ((double)(chests - k) + 1) / odds);
            sum += term;
            if (term <= sum * 1e-16)
                break;
        }

        var logTail = Math.Min(0, LogMass(chests, start, p) + Math.Log(sum));
        return upperTail ? -logTail : -Math.Log(-double.ExpM1(logTail));
    }

    private static double LogMass(long n, long k, double p)
    {
        var m = n - k;
        var smaller = Math.Min(k, m);
        if (smaller <= 32)
        {
            var logChoose = 0.0;
            for (var i = 1; i <= smaller; i++)
                logChoose += Math.Log((double)(n - smaller + i) / i);
            return logChoose + k * Math.Log(p) + m * double.LogP1(-p);
        }

        // Stirling expansion for the binomial mass, expressed in deviance form
        // to avoid subtracting large log-factorials close to the mean.
        return StirlingError(n) - StirlingError(k) - StirlingError(m)
            - Deviance(k, n * p) - Deviance(m, n * (1 - p))
            - 0.5 * Math.Log(2 * Math.PI * k * ((double)m / n));
    }

    private static double StirlingError(double x)
    {
        var inverse = 1 / x;
        var inverseSquared = inverse * inverse;
        return inverse * (1.0 / 12 - inverseSquared * (1.0 / 360 - inverseSquared / 1260));
    }

    private static double Deviance(double observed, double expected)
    {
        var relative = (observed - expected) / expected;
        return Math.Abs(relative) < 0.1
            ? expected * ((1 + relative) * double.LogP1(relative) - relative)
            : observed * Math.Log(observed / expected) + expected - observed;
    }
}
