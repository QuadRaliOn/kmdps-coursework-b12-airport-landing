namespace AirportSim;

/// <summary>Економічні показники аеропорту: дохід від посадок за типом запиту і витрати на дозаправку.
/// Підписується на події елементів мережі, тож код СМО і черги не містить економіки задачі.</summary>
public sealed class Accounting
{
    private readonly ModelParameters p;

    public Accounting(ModelParameters p, Process runways, TimedQueue queue)
    {
        this.p = p;
        runways.ServiceStarted += OnLanding;
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
            Income += plane.ProfitAfterWait;          // тип запиту 2
        }
        else
        {
            plane.State = PlaneState.LandedNoWait;
            Income += p.ProfitNoWait;                 // тип запиту 1
        }
    }

    private void OnRefuel(Plane plane) => RefuelCost += plane.RefuelCost;

    public void Reset() => Income = RefuelCost = 0.0;
}
