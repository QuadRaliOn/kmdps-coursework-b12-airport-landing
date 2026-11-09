namespace AirportSim;

/// <summary>Універсальна багатоканальна СМО мережі МО: m однакових пристроїв і черга.
/// Подія елемента – вихід запиту з каналу; при надходженні запит займає вільний канал, стає в чергу
/// або, якщо черга заповнена, отримує відмову.
/// Специфіка задачі задається параметрами (кількість каналів, генератор часу обслуговування, черга)
/// і підписками на подію ChannelOccupied.</summary>
public sealed class Process : Element
{
    private readonly double[] channelTnext;
    private readonly Request?[] channelRequest;
    private readonly Func<double> serviceTime;

    public Process(string name, int channels, Queue queue, Func<double> serviceTime) : base(name)
    {
        channelTnext = Enumerable.Repeat(double.PositiveInfinity, channels).ToArray();
        channelRequest = new Request?[channels];
        Queue = queue;
        this.serviceTime = serviceTime;
    }

    public Queue Queue { get; }
    public Element? Next { get; set; }

    /// <summary>Канал зайнято запитом (у момент надходження або виходу іншого запиту з каналу); ознака «чекав у черзі».</summary>
    public event Action<Request, bool>? ChannelOccupied;

    public int Channels => channelTnext.Length;
    public int Busy { get; private set; }
    public long StartedDirect { get; private set; }
    public long StartedFromQueue { get; private set; }
    public long Failures { get; private set; }
    public double BusyArea { get; private set; }

    public override void InAct(Request request)
    {
        int r = Array.IndexOf(channelRequest, null);
        if (r >= 0)
        {
            Start(request, r, waited: false);
        }
        else if (Queue.IsFull)
        {
            Failures++;                                  // відмова: черга заповнена
            Log?.Event(Tcurr, "відмова: черга заповнена", request);
        }
        else
        {
            Queue.Tcurr = Tcurr;
            Queue.InAct(request);
            Log?.Event(Tcurr, "надходження → черга", request);
        }
        UpdateTnext();
    }

    public override void OutAct()
    {
        for (int r = 0; r < channelTnext.Length; r++)
        {
            if (channelTnext[r] > Tcurr) continue;
            Complete(r);
            Queue.Tcurr = Tcurr;
            if (Queue.TakeFirst() is { } next)
                Start(next, r, waited: true);
        }
        UpdateTnext();
    }

    private void Start(Request request, int r, bool waited)
    {
        request.ServiceStart = Tcurr;
        channelRequest[r] = request;
        channelTnext[r] = Tcurr + serviceTime();
        Busy++;
        if (waited) StartedFromQueue++;
        else StartedDirect++;
        ChannelOccupied?.Invoke(request, waited);
        Log?.Event(Tcurr, waited ? $"канал зайнято з черги, w={Tcurr - request.EnqueueTime:F2}"
                                 : "надходження → канал", request, r);
    }

    private void Complete(int r)
    {
        Request request = channelRequest[r]!;
        channelRequest[r] = null;
        channelTnext[r] = double.PositiveInfinity;
        Busy--;
        Quantity++;
        Log?.Event(Tcurr, "вихід з каналу", request, r);
        Next?.InAct(request);
    }

    private void UpdateTnext()
    {
        double t = double.PositiveInfinity;
        foreach (double tc in channelTnext)
            if (tc < t) t = tc;
        Tnext = t;
    }

    public override void DoStatistics(double delta) => BusyArea += Busy * delta;

    public override void ResetStatistics()
    {
        base.ResetStatistics();
        StartedDirect = StartedFromQueue = Failures = 0;
        BusyArea = 0.0;
    }
}
