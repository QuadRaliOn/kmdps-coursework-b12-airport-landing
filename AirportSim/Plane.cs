namespace AirportSim;

/// <summary>Стан запиту (літака) в мережі масового обслуговування.</summary>
public enum PlaneState
{
    /// <summary>Очікує посадки в черзі.</summary>
    Waiting,
    /// <summary>Тип 1: сів без очікування, прибуток 2000 грн.</summary>
    LandedNoWait,
    /// <summary>Тип 2: сів після очікування, прибуток unif(1500, 100) грн.</summary>
    LandedAfterWait,
    /// <summary>Відлетів в інший аеропорт після 140 хв очікування.</summary>
    Diverted
}

/// <summary>
/// Запит мережі МО — літак, що потребує посадки. Атрибути генеруються в момент надходження.
/// </summary>
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
    /// <summary>a — момент надходження, хв.</summary>
    public double ArrivalTime { get; }
    /// <summary>θ — поріг очікування для дозаправки, unif(70, 10) хв.</summary>
    public double RefuelThreshold { get; }
    /// <summary>c — вартість дозаправки, unif(1000, 200) грн.</summary>
    public double RefuelCost { get; }
    /// <summary>w_max — максимальний час очікування (140 хв).</summary>
    public double Patience { get; }
    /// <summary>r — прибуток за посадку з очікуванням, unif(1500, 100) грн.</summary>
    public double ProfitAfterWait { get; }

    /// <summary>a + θ — момент події «дозаправка», якщо літак ще в черзі.</summary>
    public double RefuelMoment { get; }
    /// <summary>a + w_max — момент події «відхід в інший аеропорт», якщо літак ще в черзі.</summary>
    public double LeaveMoment { get; }

    public bool Refueled { get; set; }
    public PlaneState State { get; set; } = PlaneState.Waiting;
    public double LandingTime { get; set; } = double.NaN;
}
