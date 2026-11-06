namespace AirportSim;

/// <summary>Накопичувані показники СМО посадкових смуг (обнуляються після перехідного періоду).</summary>
public sealed class RunwayStatistics
{
    public long LandedNoWait;        // N_0 — посадки без очікування
    public long LandedAfterWait;     // N_w — посадки після очікування
    public long Enqueued;            // кількість літаків, що стали в чергу
    public long Refuels;             // N_r — кількість дозаправок
    public long Diverted;            // N_d — відльоти в інший аеропорт
    public long DivertedRefueled;    // з них були дозаправлені
    public long Released;            // кількість звільнень смуг
    public double Income;            // Σ прибутку від посадок, грн
    public double RefuelCost;        // Σ витрат на дозаправку, грн
    public double QueueArea;         // Σ L(t)·Δt
    public double BusyArea;          // Σ B(t)·Δt
    public double WaitSumLanded;     // Σ w для літаків, що сіли після очікування
    public double MaxWaitLanded;     // max w серед літаків, що сіли
    public long QueueExits;          // виходи з черги (посадка або відхід)
    public double QueueTimeSum;      // Σ часу перебування в черзі для QueueExits
    public int MaxQueue;             // L_max
    public double RefuelTimingError; // max |t − (a + θ)| у момент дозаправки (контроль коректності)

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

/// <summary>
/// СМО «посадкові смуги»: m ідентичних пристроїв D1…Dm (t = 35 хв) зі спільною чергою (max = ∞, FIFO).
/// Розширення формалізму для B12:
///  • зміна типу запиту в черзі: при w = θ (θ = unif(70, 10)) літак дозаправляється, витрати c = unif(1000, 200);
///  • маршрут «відхід» із черги з блокуванням B(S, t) = [t − a &lt; 140]: при w = 140 літак іде в інший аеропорт;
///  • тип запиту визначає прибуток: без очікування 2000 грн, з очікуванням unif(1500, 100) грн.
/// Пріоритет одночасних подій: звільнення смуги → дозаправка → відхід; надходження обробляє Create після них.
/// </summary>
public sealed class Runways : Element
{
    private readonly ModelParameters p;
    private readonly FunRand service;
    private readonly double[] channelTnext;     // t_r — момент звільнення r-ї смуги
    private readonly Plane?[] channelPlane;     // K_r — літак на r-й смузі (null — смуга вільна)
    private readonly LinkedList<Plane> queue = new();   // черга очікування посадки, FIFO
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

    /// <summary>Подія «надходження літака в СМО».</summary>
    public override void InAct(Plane plane)
    {
        int r = Array.IndexOf(channelPlane, null);
        if (r >= 0)
        {
            Land(plane, r, waited: false);
        }
        else
        {
            queue.AddLast(plane);           // max = ∞, відмов немає
            Stats.Enqueued++;
            if (queue.Count > Stats.MaxQueue) Stats.MaxQueue = queue.Count;
            Log?.Event(Tcurr, "надходження → черга", plane, this);
        }
        UpdateTnext();
    }

    /// <summary>Обробка всіх подій СМО з моментом t = Tcurr у порядку пріоритету.</summary>
    public override void OutAct()
    {
        // 1) завершення зайнятості смуг: смуга вільна, перший літак із черги сідає
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

        // 2) дозаправка літаків, у яких очікування досягло порогу θ
        if (p.RefuelEnabled && nextRefuel <= Tcurr)
        {
            foreach (Plane plane in queue)
                if (!plane.Refueled && plane.RefuelMoment <= Tcurr)
                    Refuel(plane);
        }

        // 3) відхід в інший аеропорт: маршрут із черги відкривається, коли w = w_max
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

    /// <summary>Tnext = min(t_1…t_m, найближча дозаправка, найближчий відхід).</summary>
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
