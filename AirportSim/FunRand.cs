namespace AirportSim;

/// <summary>
/// Генератор випадкових величин (метод оберненої функції, лекція 1.2).
/// Кожен екземпляр — окремий потік випадкових чисел із власним зерном.
/// </summary>
public sealed class FunRand
{
    private readonly Random rnd;

    public FunRand(int seed) => rnd = new Random(seed);

    /// <summary>Рівномірне число з (0; 1), нуль виключено, щоб не отримати ln(0).</summary>
    private double U01()
    {
        double u;
        do { u = rnd.NextDouble(); } while (u == 0.0);
        return u;
    }

    /// <summary>exp(d): x = −d · ln(u).</summary>
    public double Exp(double mean) => -mean * Math.Log(U01());

    /// <summary>unif(d, s): x = (d − s) + 2s · u, тобто рівномірно на (d − s; d + s).</summary>
    public double Unif(double mean, double dev) => mean - dev + 2.0 * dev * rnd.NextDouble();

    public double Sample(Dist dist, double mean, double dev = 0.0) => dist switch
    {
        Dist.Det => mean,
        Dist.Exp => Exp(mean),
        Dist.Unif => Unif(mean, dev),
        _ => double.PositiveInfinity
    };
}

/// <summary>
/// Незалежні потоки випадкових чисел однієї реплікації.
/// Літак отримує всі свої атрибути в момент надходження, тому при однаковому зерні
/// послідовність літаків однакова для будь-якої кількості смуг (метод загальних випадкових чисел).
/// </summary>
public sealed class RandomStreams
{
    public RandomStreams(int seed)
    {
        Arrivals = new FunRand(unchecked(seed * 1_000_003 + 11));
        Attributes = new FunRand(unchecked(seed * 1_000_003 + 7_919));
        Service = new FunRand(unchecked(seed * 1_000_003 + 104_729));
    }

    public FunRand Arrivals { get; }
    public FunRand Attributes { get; }
    public FunRand Service { get; }
}
