namespace AirportSim;

public sealed class QueueStatistics
{
    public long Enqueued;
    public long Exits;
    public long ServedExits;
    public double Area;
    public double WaitSumAll;
    public double WaitSumServed;
    public double MaxWaitServed;
    public int MaxLength;

    public void Reset(int currentLength)
    {
        Enqueued = Exits = ServedExits = 0;
        Area = WaitSumAll = WaitSumServed = MaxWaitServed = 0.0;
        MaxLength = currentLength;
    }
}

/// <summary>Черга мережі МО з дисципліною FIFO і максимальною довжиною MaxLength (за її досягнення
/// СМО відмовляє новому запиту). Власних подій не має: запит виходить з неї лише тоді,
/// коли канал СМО звільнився (метод TakeFirst). Quantity – кількість запитів, що вийшли з черги.</summary>
public class Queue : Element
{
    protected readonly LinkedList<Request> Items = new();

    public Queue(string name, int maxLength = int.MaxValue) : base(name) => MaxLength = maxLength;

    public int MaxLength { get; }
    public int Length => Items.Count;
    public bool IsFull => Items.Count >= MaxLength;
    public QueueStatistics Stats { get; } = new();

    public override void InAct(Request request)
    {
        request.EnqueueTime = Tcurr;
        Items.AddLast(request);
        Stats.Enqueued++;
        if (Items.Count > Stats.MaxLength) Stats.MaxLength = Items.Count;
        UpdateTnext();
    }

    public Request? TakeFirst()
    {
        if (Items.First is not { } node) return null;
        Items.RemoveFirst();
        Request request = node.Value;
        double w = RecordExit(request);
        Stats.ServedExits++;
        Stats.WaitSumServed += w;
        if (w > Stats.MaxWaitServed) Stats.MaxWaitServed = w;
        UpdateTnext();
        return request;
    }

    protected double RecordExit(Request request)
    {
        double w = Tcurr - request.EnqueueTime;
        Quantity++;
        Stats.Exits++;
        Stats.WaitSumAll += w;
        return w;
    }

    public override void OutAct() { }

    protected virtual void UpdateTnext() { }

    public override void DoStatistics(double delta) => Stats.Area += Items.Count * delta;

    public override void ResetStatistics()
    {
        base.ResetStatistics();
        Stats.Reset(Items.Count);
    }
}
