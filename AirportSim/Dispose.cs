namespace AirportSim;

/// <summary>Вихід із мережі МО (N1 — посадку завершено, N2 — відліт в інший аеропорт).</summary>
public sealed class Dispose : Element
{
    public Dispose(string name) : base(name) { }

    public override void InAct(Plane plane) => Quantity++;

    public override void OutAct() { }
}
