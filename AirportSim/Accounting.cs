namespace AirportSim;

public sealed class Accounting
{
    private readonly ModelParameters p;

    public Accounting(ModelParameters p, Process runways, TimedQueue queue)
    {
        this.p = p;
        runways.ChannelOccupied += OnLanding;
        queue.Refueled += OnRefuel;
        queue.TimedOut += (plane, _) => plane.State = PlaneState.Diverted;
    }

    public double Income { get; private set; }
    public double RefuelCost { get; private set; }

    private void OnLanding(Request request, bool waited)
    {
        var plane = (Plane)request;
        if (waited)
        {
            plane.State = PlaneState.LandedAfterWait;
            Income += plane.ProfitAfterWait;
        }
        else
        {
            plane.State = PlaneState.LandedNoWait;
            Income += p.ProfitNoWait;
        }
    }

    private void OnRefuel(Plane plane) => RefuelCost += plane.RefuelCost;

    public void Reset() => Income = RefuelCost = 0.0;
}
