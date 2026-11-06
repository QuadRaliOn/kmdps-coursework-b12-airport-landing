namespace AirportSim;

public enum PlaneState
{
    Waiting,
    LandedNoWait,
    LandedAfterWait,
    Diverted
}

public sealed class Plane
{
    public Plane(int id, double arrivalTime, double refuelThreshold, double refuelCost,
                 double patience, double profitAfterWait)
    {
        Id = id;
        ArrivalTime = arrivalTime;
        RefuelThreshold = refuelThreshold;
        RefuelCost = refuelCost;
        Patience = patience;
        ProfitAfterWait = profitAfterWait;
        RefuelMoment = arrivalTime + refuelThreshold;
        LeaveMoment = arrivalTime + patience;
    }

    public int Id { get; }
    public double ArrivalTime { get; }
    public double RefuelThreshold { get; }
    public double RefuelCost { get; }
    public double Patience { get; }
    public double ProfitAfterWait { get; }

    public double RefuelMoment { get; }
    public double LeaveMoment { get; }

    public bool Refueled { get; set; }
    public PlaneState State { get; set; } = PlaneState.Waiting;
    public double LandingTime { get; set; } = double.NaN;
}
