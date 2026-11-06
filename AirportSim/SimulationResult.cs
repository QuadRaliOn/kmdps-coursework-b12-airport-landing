namespace AirportSim;

/// <summary>Вихідні характеристики одного прогону моделі (на інтервалі збору статистики).</summary>
public sealed record SimulationResult
{
    public int Runways { get; init; }
    public int Seed { get; init; }
    public double Tmod { get; init; }
    public double Twarm { get; init; }
    /// <summary>T = Tmod − Twarm — тривалість збору статистики, хв.</summary>
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

    public long Landed => LandedNoWait + LandedAfterWait;
    /// <summary>Прибуток за період: Σ прибутку від посадок − Σ витрат на дозаправку, грн.</summary>
    public double NetProfit => Income - RefuelCost;
    /// <summary>p — річний прибуток, грн/рік.</summary>
    public double AnnualProfit => NetProfit / Period * ModelParameters.MinutesPerYear;
    public double LandingsPerYear => Landed / Period * ModelParameters.MinutesPerYear;
    /// <summary>ρ — середнє завантаження смуги.</summary>
    public double Utilization => MeanBusy / Runways;
    /// <summary>Частка посадок, що відбулись з очікуванням.</summary>
    public double ShareLandedAfterWait => Landed == 0 ? 0 : (double)LandedAfterWait / Landed;
    /// <summary>Частка літаків, що пішли в інший аеропорт.</summary>
    public double ShareDiverted => Arrivals == 0 ? 0 : (double)Diverted / Arrivals;
    /// <summary>Частка літаків, які потребували дозаправки.</summary>
    public double ShareRefueled => Arrivals == 0 ? 0 : (double)Refuels / Arrivals;
    /// <summary>Середнє очікування посадки серед усіх літаків, що сіли (включно з w = 0), хв.</summary>
    public double MeanWaitAllLanded => Landed == 0 ? 0 : MeanWaitLanded * LandedAfterWait / Landed;
    /// <summary>Перевірка за формулою Літтла: λ_q · W_q ≈ L̄.</summary>
    public double LittleQueue => QueueExitRate * MeanTimeInQueue;
    /// <summary>Частка літаків, що стали в чергу (оцінка P_w).</summary>
    public double ShareEnqueued => Arrivals == 0 ? 0 : (double)Enqueued / Arrivals;
    /// <summary>Середній час у черзі в розрахунку на один літак, що надійшов (оцінка W_q), хв.</summary>
    public double MeanWaitPerArrival => Arrivals == 0 ? 0 : MeanTimeInQueue * QueueExitRate * Period / Arrivals;

    public static SimulationResult From(Model model, double tmod)
    {
        RunwayStatistics s = model.Runways.Stats;
        double period = tmod - model.StatStart;
        return new SimulationResult
        {
            Runways = model.Parameters.Runways,
            Seed = model.Seed,
            Tmod = tmod,
            Twarm = model.StatStart,
            Period = period,
            Arrivals = model.Source.Quantity,
            Enqueued = s.Enqueued,
            LandedNoWait = s.LandedNoWait,
            LandedAfterWait = s.LandedAfterWait,
            Refuels = s.Refuels,
            Diverted = s.Diverted,
            DivertedRefueled = s.DivertedRefueled,
            Released = s.Released,
            Income = s.Income,
            RefuelCost = s.RefuelCost,
            MeanQueue = s.QueueArea / period,
            MaxQueue = s.MaxQueue,
            MeanBusy = s.BusyArea / period,
            MeanWaitLanded = s.LandedAfterWait == 0 ? 0 : s.WaitSumLanded / s.LandedAfterWait,
            MaxWaitLanded = s.MaxWaitLanded,
            QueueExitRate = s.QueueExits / period,
            MeanTimeInQueue = s.QueueExits == 0 ? 0 : s.QueueTimeSum / s.QueueExits,
            QueueAtEnd = model.Runways.QueueLength,
            BusyAtEnd = model.Runways.Busy,
            RefuelTimingError = s.RefuelTimingError,
            Events = model.EventCount
        };
    }
}
