namespace AirportSim;

/// <summary>Вихідні характеристики одного прогону (формули підрозділу 2.3).</summary>
public sealed record SimulationResult
{
    public int Runways { get; init; }
    public int Seed { get; init; }
    public double Tmod { get; init; }
    public double Twarm { get; init; }
    public double Period { get; init; }

    public long Arrivals { get; init; }
    public long Enqueued { get; init; }
    public long LandedNoWait { get; init; }
    public long LandedAfterWait { get; init; }
    public long Refuels { get; init; }
    public long Diverted { get; init; }
    public long DivertedRefueled { get; init; }
    public long Released { get; init; }
    public double Income { get; init; }
    public double RefuelCost { get; init; }
    public double MeanQueue { get; init; }
    public int MaxQueue { get; init; }
    public double MeanBusy { get; init; }
    public double MeanWaitLanded { get; init; }
    public double MaxWaitLanded { get; init; }
    public double QueueExitRate { get; init; }
    public double MeanTimeInQueue { get; init; }
    public int QueueAtEnd { get; init; }
    public int BusyAtEnd { get; init; }
    public double RefuelTimingError { get; init; }
    public long Events { get; init; }
    public long QueueExits { get; init; }
    public double WaitSumAll { get; init; }

    public long Landed => LandedNoWait + LandedAfterWait;
    public double NetProfit => Income - RefuelCost;
    public double AnnualProfit => NetProfit / Period * ModelParameters.MinutesPerYear;
    public double LandingsPerYear => Landed / Period * ModelParameters.MinutesPerYear;
    public double Utilization => MeanBusy / Runways;
    public double ShareLandedAfterWait => Landed == 0 ? 0 : (double)LandedAfterWait / Landed;
    public double ShareDiverted => Arrivals == 0 ? 0 : (double)Diverted / Arrivals;
    public double ShareRefueled => Arrivals == 0 ? 0 : (double)Refuels / Arrivals;
    public double MeanWaitAllLanded => Landed == 0 ? 0 : MeanWaitLanded * LandedAfterWait / Landed;
    public double LittleQueue => QueueExitRate * MeanTimeInQueue;
    public double ShareEnqueued => Arrivals == 0 ? 0 : (double)Enqueued / Arrivals;
    public double MeanWaitPerArrival => Arrivals == 0 ? 0 : MeanTimeInQueue * QueueExitRate * Period / Arrivals;

    /// <summary>Середнє очікування за всіма літаками, що пройшли через вузол «черга – смуги»
    /// (сіли без очікування, сіли після очікування, відійшли): Σw / (N0 + Nw + Nd).</summary>
    public double MeanWaitAll => LandedNoWait + QueueExits == 0 ? 0 : WaitSumAll / (LandedNoWait + QueueExits);

    public static SimulationResult From(Model model, double tmod)
    {
        Process smo = model.Runways;
        TimedQueue q = model.Queue;
        QueueStatistics qs = q.Stats;
        double period = tmod - model.StatStart;
        return new SimulationResult
        {
            Runways = model.Parameters.Runways,
            Seed = model.Seed,
            Tmod = tmod,
            Twarm = model.StatStart,
            Period = period,
            Arrivals = model.Source.Quantity,
            Enqueued = qs.Enqueued,
            LandedNoWait = smo.StartedDirect,
            LandedAfterWait = smo.StartedFromQueue,
            Refuels = q.Refuels,
            Diverted = q.TimeoutExits,
            DivertedRefueled = q.TimeoutExitsRefueled,
            Released = smo.Quantity,
            Income = model.Accounting.Income,
            RefuelCost = model.Accounting.RefuelCost,
            MeanQueue = qs.Area / period,
            MaxQueue = qs.MaxLength,
            MeanBusy = smo.BusyArea / period,
            MeanWaitLanded = qs.ServedExits == 0 ? 0 : qs.WaitSumServed / qs.ServedExits,
            MaxWaitLanded = qs.MaxWaitServed,
            QueueExitRate = qs.Exits / period,
            MeanTimeInQueue = qs.Exits == 0 ? 0 : qs.WaitSumAll / qs.Exits,
            QueueAtEnd = q.Length,
            BusyAtEnd = smo.Busy,
            RefuelTimingError = q.RefuelTimingError,
            Events = model.EventCount,
            QueueExits = qs.Exits,
            WaitSumAll = qs.WaitSumAll
        };
    }
}
