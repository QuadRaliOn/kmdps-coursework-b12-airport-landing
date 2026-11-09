namespace AirportSim;

/// <summary>Запит мережі МО: момент надходження, момент постановки в чергу, номер.</summary>
public class Request
{
    public Request(int id, double arrivalTime)
    {
        Id = id;
        ArrivalTime = arrivalTime;
    }

    public int Id { get; }
    public double ArrivalTime { get; }
    public double EnqueueTime { get; set; } = double.NaN;
    public double ServiceStart { get; set; } = double.NaN;
}
