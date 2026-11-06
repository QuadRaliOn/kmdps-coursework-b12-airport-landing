namespace AirportSim;

public sealed class FunRand
{
    private readonly Random rnd;

    public FunRand(int seed) => rnd = new Random(seed);

    private double U01()
    {
        double u;
        do { u = rnd.NextDouble(); } while (u == 0.0);
        return u;
    }

    public double Exp(double mean) => -mean * Math.Log(U01());

    public double Unif(double mean, double dev) => mean - dev + 2.0 * dev * rnd.NextDouble();

    public double Sample(Dist dist, double mean, double dev = 0.0) => dist switch
    {
        Dist.Det => mean,
        Dist.Exp => Exp(mean),
        Dist.Unif => Unif(mean, dev),
        _ => double.PositiveInfinity
    };
}

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
