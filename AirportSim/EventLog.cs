using System.Globalization;

namespace AirportSim;

public sealed class EventLog
{
    private readonly TextWriter writer;
    private readonly int maxLines;
    private int lines;
    private Model? model;

    public EventLog(TextWriter writer, int maxLines = int.MaxValue)
    {
        this.writer = writer;
        this.maxLines = maxLines;
        writer.WriteLine("     t, хв | подія                                        | літак |   a, хв | смуга |  L |   B  | N_r | N_d | прибуток, грн");
        writer.WriteLine(new string('-', 124));
    }

    public void Attach(Model m) => model = m;

    public void Event(double t, string description, Request request, int channel = -1)
    {
        if (lines++ >= maxLines || model is null) return;
        string ch = channel >= 0 ? $"D{channel + 1}" : "–";
        Process smo = model.Runways;
        TimedQueue q = model.Queue;
        double net = model.Accounting.Income - model.Accounting.RefuelCost;
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{t,10:F2} | {description,-44} | {request.Id,5} | {request.ArrivalTime,7:F2} | {ch,5} | {q.Length,2} | {smo.Busy,2}/{smo.Channels,-2} | {q.Refuels,3} | {q.TimeoutExits,3} | {net,12:F2}"));
    }
}
