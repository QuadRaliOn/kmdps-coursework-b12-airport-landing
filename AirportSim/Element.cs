namespace AirportSim;

/// <summary>
/// Базовий елемент мережі масового обслуговування (ООП-підхід лекції 1.4:
/// Element → Create / Process / Dispose, Model).
/// </summary>
public abstract class Element
{
    protected Element(string name) => Name = name;

    public string Name { get; }

    /// <summary>Поточний момент модельного часу, хв.</summary>
    public double Tcurr { get; set; }

    /// <summary>Момент найближчої події елемента, хв; +∞, якщо подій не очікується.</summary>
    public double Tnext { get; protected set; } = double.PositiveInfinity;

    /// <summary>Кількість запитів, що вийшли з елемента (після перехідного періоду).</summary>
    public long Quantity { get; protected set; }

    /// <summary>Протокол подій (null — протокол не ведеться).</summary>
    public EventLog? Log { get; set; }

    /// <summary>Надходження запиту в елемент.</summary>
    public virtual void InAct(Plane plane) { }

    /// <summary>Обробка подій елемента, момент яких дорівнює Tcurr.</summary>
    public abstract void OutAct();

    /// <summary>Накопичення статистики на інтервалі Δt між сусідніми подіями.</summary>
    public virtual void DoStatistics(double delta) { }

    /// <summary>Обнулення статистики наприкінці перехідного періоду (стан елемента не змінюється).</summary>
    public virtual void ResetStatistics() => Quantity = 0;
}
