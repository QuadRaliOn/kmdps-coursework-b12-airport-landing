namespace AirportSim;

public abstract class Element
{
    protected Element(string name) => Name = name;

    public string Name { get; }

    public double Tcurr { get; set; }

    public double Tnext { get; protected set; } = double.PositiveInfinity;

    public long Quantity { get; protected set; }

    public EventLog? Log { get; set; }

    public virtual void InAct(Request request) { }

    public abstract void OutAct();

    public virtual void DoStatistics(double delta) { }

    public virtual void ResetStatistics() => Quantity = 0;
}
