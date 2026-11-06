using System.Globalization;

namespace AirportSim;

/// <summary>Протокол подій: один рядок на кожну зміну стану моделі.</summary>
public sealed class EventLog
{
    private readonly TextWriter writer;
    private readonly int maxLines;
    private int lines;

    public EventLog(TextWriter writer, int maxLines = int.MaxValue)
    {
        this.writer = writer;
        this.maxLines = maxLines;
        writer.WriteLine("     t, хв | подія                                        | літак |   a, хв | смуга |  L |   B  | N_r | N_d | прибуток, грн");
        writer.WriteLine(new string('-', 124));
    }

    public void Event(double t, string description, Plane plane, Runways runways, int channel = -1)
    {
        if (lines++ >= maxLines) return;
        string ch = channel >= 0 ? $"D{channel + 1}" : "–";
        double net = runways.Stats.Income - runways.Stats.RefuelCost;
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{t,10:F2} | {description,-44} | {plane.Id,5} | {plane.ArrivalTime,7:F2} | {ch,5} | {runways.QueueLength,2} | {runways.Busy,2}/{runways.Channels,-2} | {runways.Stats.Refuels,3} | {runways.Stats.Diverted,3} | {net,12:F2}"));
    }

    public void Note(string text)
    {
        if (lines >= maxLines) return;
        writer.WriteLine(text);
    }
}
