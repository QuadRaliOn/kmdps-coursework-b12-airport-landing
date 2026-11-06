namespace AirportSim;

public sealed class Model
{
    private readonly List<Element> elements;

    public Model(ModelParameters parameters, int seed, EventLog? log = null)
    {
        Parameters = parameters;
        Seed = seed;
        var streams = new RandomStreams(seed);
        Source = new Create(parameters, streams);
        Runways = new Runways(parameters, streams);
        Landed = new Dispose("N1");
        OtherAirport = new Dispose("N2");

        Source.Next = Runways;
        Runways.LandedExit = Landed;
        Runways.DivertedExit = OtherAirport;

        elements = new List<Element> { Runways, Source, Landed, OtherAirport };
        foreach (Element e in elements) e.Log = log;
    }

    public ModelParameters Parameters { get; }
    public int Seed { get; }
    public Create Source { get; }
    public Runways Runways { get; }
    public Dispose Landed { get; }
    public Dispose OtherAirport { get; }

    public double Tcurr { get; private set; }

    public double StatStart { get; private set; }

    public long EventCount { get; private set; }

    public SimulationResult Simulate(double tmod, double twarm = 0.0, Action<Model>? afterEvent = null)
    {
        bool warmedUp = twarm <= 0.0;
        while (true)
        {
            double tnext = double.PositiveInfinity;
            foreach (Element e in elements)
                if (e.Tnext < tnext) tnext = e.Tnext;

            if (!warmedUp && tnext >= twarm)
            {
                Advance(Math.Min(twarm, tmod));
                foreach (Element e in elements) e.ResetStatistics();
                StatStart = twarm;
                warmedUp = true;
            }

            if (tnext > tmod)
            {
                Advance(tmod);
                break;
            }

            Advance(tnext);

            foreach (Element e in elements)
                if (e.Tnext == Tcurr) e.OutAct();

            EventCount++;
            afterEvent?.Invoke(this);
        }
        return SimulationResult.From(this, tmod);
    }

    private void Advance(double t)
    {
        double delta = t - Tcurr;
        if (delta > 0)
            foreach (Element e in elements) e.DoStatistics(delta);
        Tcurr = t;
        foreach (Element e in elements) e.Tcurr = t;
    }
}
