namespace AirportSim;

/// <summary>
/// Імітаційна модель мережі МО «посадка літаків»: C → СМО(D1…Dm) → N1, черга СМО → N2.
/// Алгоритм просування часу до найближчої події (лекція 1.3, алгоритм 3.1 методички).
/// </summary>
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

        // Порядок у списку = пріоритет подій, що збіглися в часі:
        // спершу події СМО (звільнення смуги, дозаправка, відхід), потім надходження нового літака.
        elements = new List<Element> { Runways, Source, Landed, OtherAirport };
        foreach (Element e in elements) e.Log = log;
    }

    public ModelParameters Parameters { get; }
    public int Seed { get; }
    public Create Source { get; }
    public Runways Runways { get; }
    public Dispose Landed { get; }
    public Dispose OtherAirport { get; }

    /// <summary>Поточний модельний час t, хв.</summary>
    public double Tcurr { get; private set; }

    /// <summary>Момент початку збору статистики (кінець перехідного періоду), хв.</summary>
    public double StatStart { get; private set; }

    public long EventCount { get; private set; }

    /// <summary>
    /// Імітація на інтервалі [0; tmod]. Статистика збирається на [twarm; tmod].
    /// afterEvent викликається після кожного кроку (для дослідження перехідного періоду).
    /// </summary>
    public SimulationResult Simulate(double tmod, double twarm = 0.0, Action<Model>? afterEvent = null)
    {
        bool warmedUp = twarm <= 0.0;
        while (true)
        {
            // 1) найближча подія
            double tnext = double.PositiveInfinity;
            foreach (Element e in elements)
                if (e.Tnext < tnext) tnext = e.Tnext;

            // кінець перехідного періоду: статистику, зібрану до twarm, відкидаємо
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

            // 2) статистика на [t; tnext], 3) просування часу
            Advance(tnext);

            // 4) зміна стану: усі елементи, подія яких настала в момент t
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
