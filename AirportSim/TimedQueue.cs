namespace AirportSim;

public sealed class TimedQueue : Queue
{
    private readonly bool refuelEnabled;
    private double nextRefuel = double.PositiveInfinity;
    private double nextLeave = double.PositiveInfinity;

    public TimedQueue(string name, bool refuelEnabled, int maxLength = int.MaxValue) : base(name, maxLength)
        => this.refuelEnabled = refuelEnabled;

    public Element? TimeoutExit { get; set; }

    public event Action<Plane>? Refueled;
    public event Action<Plane, double>? TimedOut;

    public long Refuels { get; private set; }
    public long TimeoutExits { get; private set; }
    public long TimeoutExitsRefueled { get; private set; }
    public double RefuelTimingError { get; private set; }

    public override void OutAct()
    {
        if (refuelEnabled && nextRefuel <= Tcurr)
        {
            foreach (Request request in Items)
                if (request is Plane { Refueled: false } plane && plane.RefuelMoment <= Tcurr)
                    Refuel(plane);
        }

        if (nextLeave <= Tcurr)
        {
            LinkedListNode<Request>? node = Items.First;
            while (node != null)
            {
                LinkedListNode<Request>? next = node.Next;
                if (node.Value is Plane plane && plane.LeaveMoment <= Tcurr)
                {
                    Items.Remove(node);
                    Leave(plane);
                }
                node = next;
            }
        }

        UpdateTnext();
    }

    private void Refuel(Plane plane)
    {
        plane.Refueled = true;
        Refuels++;
        double err = Math.Abs(Tcurr - plane.RefuelMoment);
        if (err > RefuelTimingError) RefuelTimingError = err;
        Refueled?.Invoke(plane);
        Log?.Event(Tcurr, $"дозаправка, θ={plane.RefuelThreshold:F2}, c={plane.RefuelCost:F2}", plane);
    }

    private void Leave(Plane plane)
    {
        double w = RecordExit(plane);
        TimeoutExits++;
        if (plane.Refueled) TimeoutExitsRefueled++;
        TimedOut?.Invoke(plane, w);
        Log?.Event(Tcurr, $"відхід в інший аеропорт, w={w:F2}", plane);
        TimeoutExit?.InAct(plane);
    }

    protected override void UpdateTnext()
    {
        nextRefuel = double.PositiveInfinity;
        nextLeave = double.PositiveInfinity;
        foreach (Request request in Items)
        {
            if (request is not Plane plane) continue;
            if (refuelEnabled && !plane.Refueled && plane.RefuelMoment < nextRefuel) nextRefuel = plane.RefuelMoment;
            if (plane.LeaveMoment < nextLeave) nextLeave = plane.LeaveMoment;
        }
        Tnext = Math.Min(nextRefuel, nextLeave);
    }

    public override void ResetStatistics()
    {
        base.ResetStatistics();
        Refuels = TimeoutExits = TimeoutExitsRefueled = 0;
        RefuelTimingError = 0.0;
    }
}
