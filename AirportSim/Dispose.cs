namespace AirportSim;

public sealed class Dispose : Element
{
    public Dispose(string name) : base(name) { }

    public override void InAct(Request request) => Quantity++;

    public override void OutAct() { }
}
