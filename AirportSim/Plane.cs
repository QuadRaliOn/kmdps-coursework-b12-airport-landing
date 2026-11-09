namespace AirportSim;

public enum PlaneState
{
    Waiting,
    LandedNoWait,
    LandedAfterWait,
    Diverted
}

/// <summary>Запит задачі B12 – літак з атрибутами θ, c, π і максимальним часом очікування.</summary>
public sealed class Plane : Request
{
    public Plane(int id, double arrivalTime, double refuelThreshold, double refuelCost,
                 double patience, double profitAfterWait) : base(id, arrivalTime)
    {
        RefuelThreshold = refuelThreshold;
        RefuelCost = refuelCost;
        Patience = patience;
        ProfitAfterWait = profitAfterWait;
        RefuelMoment = arrivalTime + refuelThreshold;
        LeaveMoment = arrivalTime + patience;
    }

    public double RefuelThreshold { get; }
    public double RefuelCost { get; }
    public double Patience { get; }
    public double ProfitAfterWait { get; }

    public double RefuelMoment { get; }
    public double LeaveMoment { get; }

    public bool Refueled { get; set; }
    public PlaneState State { get; set; } = PlaneState.Waiting;
}
