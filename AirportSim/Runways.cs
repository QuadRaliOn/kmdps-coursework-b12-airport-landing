namespace AirportSim;

public sealed class RunwayStatistics
{
    public long LandedNoWait;
    public long LandedAfterWait;
    public long Enqueued;
    public long Refuels;
    public long Diverted;
    public long DivertedRefueled;
    public long Released;
    public double Income;
    public double RefuelCost;
    public double QueueArea;
    public double BusyArea;
    public double WaitSumLanded;
    public double MaxWaitLanded;
    public long QueueExits;
    public double QueueTimeSum;
    public int MaxQueue;
    public double RefuelTimingError;

    public void Reset() => this.CopyFrom(new RunwayStatistics());

    private void CopyFrom(RunwayStatistics s)
    {
        LandedNoWait = s.LandedNoWait; LandedAfterWait = s.LandedAfterWait; Enqueued = s.Enqueued;
        Refuels = s.Refuels; Diverted = s.Diverted; DivertedRefueled = s.DivertedRefueled;
        Released = s.Released; Income = s.Income; RefuelCost = s.RefuelCost;
        QueueArea = s.QueueArea; BusyArea = s.BusyArea; WaitSumLanded = s.WaitSumLanded;
        MaxWaitLanded = s.MaxWaitLanded; QueueExits = s.QueueExits; QueueTimeSum = s.QueueTimeSum;
        MaxQueue = s.MaxQueue; RefuelTimingError = s.RefuelTimingError;
    }
}

public sealed class Runways : Element
{
    private readonly ModelParameters p;
    private readonly FunRand service;
    private readonly double[] channelTnext;
    private readonly Plane?[] channelPlane;
    private readonly LinkedList<Plane> queue = new();
    private double nextRefuel = double.PositiveInfinity;
    private double nextLeave = double.PositiveInfinity;

    public Runways(ModelParameters p, RandomStreams streams) : base("СМО")
    {
        this.p = p;
        service = streams.Service;
        channelTnext = Enumerable.Repeat(double.PositiveInfinity, p.Runways).ToArray();
        channelPlane = new Plane?[p.Runways];
    }

    public Element? LandedExit { get; set; }
    public Element? DivertedExit { get; set; }
    public RunwayStatistics Stats { get; } = new();

    public int Channels => channelTnext.Length;
    public int QueueLength => queue.Count;
    public int Busy { get; private set; }

    public override void InAct(Plane plane)
    {
        int r = Array.IndexOf(channelPlane, null);
        if (r >= 0)
        {
            Land(plane, r, waited: false);
        }
        else
        {
            queue.AddLast(plane);
            Stats.Enqueued++;
            if (queue.Count > Stats.MaxQueue) Stats.MaxQueue = queue.Count;
            Log?.Event(Tcurr, "надходження → черга", plane, this);
        }
        UpdateTnext();
    }

    public override void OutAct()
    {
        for (int r = 0; r < channelTnext.Length; r++)
        {
            if (channelTnext[r] > Tcurr) continue;
            Release(r);
            if (queue.First is { } head)
            {
                queue.RemoveFirst();
                Land(head.Value, r, waited: true);
            }
        }

        if (p.RefuelEnabled && nextRefuel <= Tcurr)
        {
            foreach (Plane plane in queue)
                if (!plane.Refueled && plane.RefuelMoment <= Tcurr)
                    Refuel(plane);
        }

        if (nextLeave <= Tcurr)
        {
            LinkedListNode<Plane>? node = queue.First;
            while (node != null)
            {
                LinkedListNode<Plane>? next = node.Next;
                if (node.Value.LeaveMoment <= Tcurr)
                {
                    queue.Remove(node);
                    Divert(node.Value);
                }
                node = next;
            }
        }

        UpdateTnext();
    }

    private void Land(Plane plane, int r, bool waited)
    {
        plane.LandingTime = Tcurr;
        channelPlane[r] = plane;
        channelTnext[r] = Tcurr + service.Sample(p.OccupancyDist, p.LandingOccupancy);
        Busy++;
        if (waited)
        {
            double w = Tcurr - plane.ArrivalTime;
            plane.State = PlaneState.LandedAfterWait;
            Stats.LandedAfterWait++;
            Stats.Income += plane.ProfitAfterWait;
            Stats.WaitSumLanded += w;
            if (w > Stats.MaxWaitLanded) Stats.MaxWaitLanded = w;
            Stats.QueueExits++;
            Stats.QueueTimeSum += w;
            Log?.Event(Tcurr, $"посадка після очікування, w={w:F2}", plane, this, r);
        }
        else
        {
            plane.State = PlaneState.LandedNoWait;
            Stats.LandedNoWait++;
            Stats.Income += p.ProfitNoWait;
            Log?.Event(Tcurr, "надходження → посадка без очікування", plane, this, r);
        }
    }

    private void Release(int r)
    {
        Plane plane = channelPlane[r]!;
        channelPlane[r] = null;
        channelTnext[r] = double.PositiveInfinity;
        Busy--;
        Quantity++;
        Stats.Released++;
        Log?.Event(Tcurr, "звільнення смуги", plane, this, r);
        LandedExit?.InAct(plane);
    }

    private void Refuel(Plane plane)
    {
        plane.Refueled = true;
        Stats.Refuels++;
        Stats.RefuelCost += plane.RefuelCost;
        double err = Math.Abs(Tcurr - plane.RefuelMoment);
        if (err > Stats.RefuelTimingError) Stats.RefuelTimingError = err;
        Log?.Event(Tcurr, $"дозаправка, θ={plane.RefuelThreshold:F2}, c={plane.RefuelCost:F2}", plane, this);
    }

    private void Divert(Plane plane)
    {
        double w = Tcurr - plane.ArrivalTime;
        plane.State = PlaneState.Diverted;
        Stats.Diverted++;
        if (plane.Refueled) Stats.DivertedRefueled++;
        Stats.QueueExits++;
        Stats.QueueTimeSum += w;
        Log?.Event(Tcurr, $"відхід в інший аеропорт, w={w:F2}", plane, this);
        DivertedExit?.InAct(plane);
    }

    private void UpdateTnext()
    {
        double tChannel = double.PositiveInfinity;
        foreach (double t in channelTnext)
            if (t < tChannel) tChannel = t;

        nextRefuel = double.PositiveInfinity;
        nextLeave = double.PositiveInfinity;
        foreach (Plane plane in queue)
        {
            if (p.RefuelEnabled && !plane.Refueled && plane.RefuelMoment < nextRefuel) nextRefuel = plane.RefuelMoment;
            if (plane.LeaveMoment < nextLeave) nextLeave = plane.LeaveMoment;
        }
        Tnext = Math.Min(tChannel, Math.Min(nextRefuel, nextLeave));
    }

    public override void DoStatistics(double delta)
    {
        Stats.QueueArea += queue.Count * delta;
        Stats.BusyArea += Busy * delta;
    }

    public override void ResetStatistics()
    {
        base.ResetStatistics();
        Stats.Reset();
        Stats.MaxQueue = queue.Count;
    }
}
