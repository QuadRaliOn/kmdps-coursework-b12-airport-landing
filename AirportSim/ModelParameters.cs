namespace AirportSim;

/// <summary>Закон розподілу числового параметра (позначення методички: d, exp(d), unif(d, s)).</summary>
public enum Dist
{
    /// <summary>Детерміноване значення d.</summary>
    Det,
    /// <summary>exp(d) — експоненціальний закон із середнім d.</summary>
    Exp,
    /// <summary>unif(d, s) — рівномірний закон на інтервалі (d − s; d + s).</summary>
    Unif,
    /// <summary>Подія не відбувається (значення ∞).</summary>
    None
}

/// <summary>
/// Вхідні змінні та параметри моделі посадки літаків (завдання B12).
/// Одиниця модельного часу — 1 хвилина, грошова одиниця — 1 гривня.
/// </summary>
public sealed record ModelParameters
{
    public const double MinutesPerYear = 525_600.0;   // 365 діб · 1440 хв
    public const double RunwayCost = 3_000_000.0;     // вартість будівництва однієї додаткової смуги, грн
    public const int BaseRunways = 2;                 // кількість наявних смуг
    public const int MaxAdditionalRunways = 10;

    /// <summary>Середній інтервал між надходженнями літаків, хв: exp(6) ⇔ 10 літаків/год.</summary>
    public double MeanInterarrival { get; init; } = 6.0;
    public Dist InterarrivalDist { get; init; } = Dist.Exp;

    /// <summary>Кількість посадкових смуг m = 2 + k (фактор експерименту).</summary>
    public int Runways { get; init; } = BaseRunways;

    /// <summary>Час зайнятості смуги після посадки, хв (за умовою детермінований: 35).</summary>
    public double LandingOccupancy { get; init; } = 35.0;
    public Dist OccupancyDist { get; init; } = Dist.Det;

    /// <summary>Поріг очікування, після якого потрібна дозаправка: unif(70, 10) хв.</summary>
    public double RefuelThresholdMean { get; init; } = 70.0;
    public double RefuelThresholdDev { get; init; } = 10.0;
    public bool RefuelEnabled { get; init; } = true;

    /// <summary>Витрати аеропорту на одну дозаправку: unif(1000, 200) грн.</summary>
    public double RefuelCostMean { get; init; } = 1000.0;
    public double RefuelCostDev { get; init; } = 200.0;

    /// <summary>Максимальний час очікування посадки, хв: після 140 хв літак іде в інший аеропорт.</summary>
    public double MaxWait { get; init; } = 140.0;
    /// <summary>Det — рівно MaxWait (умова завдання); Exp — exp(MaxWait) (лише для верифікації); None — без відходу.</summary>
    public Dist PatienceDist { get; init; } = Dist.Det;

    /// <summary>Прибуток за посадку без очікування, грн.</summary>
    public double ProfitNoWait { get; init; } = 2000.0;
    /// <summary>Прибуток за посадку з очікуванням: unif(1500, 100) грн.</summary>
    public double ProfitWaitMean { get; init; } = 1500.0;
    public double ProfitWaitDev { get; init; } = 100.0;

    public int AdditionalRunways => Runways - BaseRunways;

    public override string ToString() =>
        $"m={Runways}, інтервал={InterarrivalDist}({MeanInterarrival}), смуга={OccupancyDist}({LandingOccupancy}), " +
        $"дозаправка={(RefuelEnabled ? $"unif({RefuelThresholdMean},{RefuelThresholdDev})" : "вимкнена")}, " +
        $"відхід={PatienceDist}({MaxWait})";
}
