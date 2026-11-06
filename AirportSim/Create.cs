namespace AirportSim;

/// <summary>
/// Елемент «надходження запитів ззовні» C: t = exp(6) хв.
/// У момент надходження літак отримує всі випадкові атрибути (θ, c, r, w_max).
/// </summary>
public sealed class Create : Element
{
    private readonly ModelParameters p;
    private readonly FunRand arrivals;
    private readonly FunRand attributes;
    private int nextId = 1;

    public Create(ModelParameters p, RandomStreams streams) : base("C")
    {
        this.p = p;
        arrivals = streams.Arrivals;
        attributes = streams.Attributes;
        Tnext = arrivals.Sample(p.InterarrivalDist, p.MeanInterarrival);   // перше надходження
    }

    /// <summary>Наступний елемент маршруту (СМО посадкових смуг).</summary>
    public Element? Next { get; set; }

    public override void OutAct()
    {
        Quantity++;
        Plane plane = NewPlane();
        Tnext = Tcurr + arrivals.Sample(p.InterarrivalDist, p.MeanInterarrival);   // t0 := t + g0
        Next?.InAct(plane);
    }

    private Plane NewPlane()
    {
        double threshold = attributes.Unif(p.RefuelThresholdMean, p.RefuelThresholdDev);
        double refuelCost = attributes.Unif(p.RefuelCostMean, p.RefuelCostDev);
        double profit = attributes.Unif(p.ProfitWaitMean, p.ProfitWaitDev);
        double patience = p.PatienceDist switch
        {
            Dist.Det => p.MaxWait,
            Dist.Exp => attributes.Exp(p.MaxWait),
            _ => double.PositiveInfinity
        };
        return new Plane(nextId++, Tcurr, threshold, refuelCost, patience, profit);
    }
}
