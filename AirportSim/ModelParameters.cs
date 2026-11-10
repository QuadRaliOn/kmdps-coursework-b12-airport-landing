namespace AirportSim;

public enum Dist
{
    Det,
    Exp,
    Unif,
    None
}

public sealed record ModelParameters
{
    public const double MinutesPerYear = 525_600.0;
    public const double RunwayCost = 3_000_000.0;
    public const int BaseRunways = 2;
    public const int MaxAdditionalRunways = 10;

    public double MeanInterarrival { get; init; } = 6.0;
    public Dist InterarrivalDist { get; init; } = Dist.Exp;

    public int Runways { get; init; } = BaseRunways;

    public double LandingOccupancy { get; init; } = 35.0;
    public Dist OccupancyDist { get; init; } = Dist.Det;

    public double RefuelThresholdMean { get; init; } = 70.0;
    public double RefuelThresholdDev { get; init; } = 10.0;
    public bool RefuelEnabled { get; init; } = true;

    public double RefuelCostMean { get; init; } = 1000.0;
    public double RefuelCostDev { get; init; } = 200.0;

    public double MaxWait { get; init; } = 140.0;
    public int MaxQueueLength { get; init; } = int.MaxValue;
    public Dist PatienceDist { get; init; } = Dist.Det;

    public double ProfitNoWait { get; init; } = 2000.0;
    public double ProfitWaitMean { get; init; } = 1500.0;
    public double ProfitWaitDev { get; init; } = 100.0;

    public int AdditionalRunways => Runways - BaseRunways;

    public override string ToString()
    {
        string refuel = RefuelEnabled ? $"unif({RefuelThresholdMean},{RefuelThresholdDev})" : "вимкнена";
        return $"m={Runways}, інтервал={InterarrivalDist}({MeanInterarrival}), " +
               $"смуга={OccupancyDist}({LandingOccupancy}), " +
               $"дозаправка={refuel}, відхід={PatienceDist}({MaxWait})";
    }
}
