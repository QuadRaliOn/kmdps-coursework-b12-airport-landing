using System.Globalization;
using System.Text;

namespace AirportSim;

/// <summary>
/// Точка входу. Приклади:
///   dotnet run -c Release -- run --m 6 --tmod 525600 --seed 1
///   dotnet run -c Release -- run --m 2 --tmod 300 --trace 60
///   dotnet run -c Release -- verify
///   dotnet run -c Release -- series --tag main --reps 30 --twarm 3000 --tstat 525600
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        string command = args.Length > 0 ? args[0] : "run";
        var opt = ParseOptions(args.Skip(1).ToArray());
        string outDir = Get(opt, "out", "results");

        switch (command)
        {
            case "run":
                RunSingle(opt);
                break;
            case "trace":
                Experiments.Trace(outDir);
                Console.WriteLine($"Протоколи подій збережено в {outDir}");
                break;
            case "verify":
                Experiments.Verify(outDir);
                break;
            case "transient":
                string tag = Get(opt, "tag", "cum");
                int[] ms = Get(opt, "ms", "2,4,6,7,8").Split(',').Select(int.Parse).ToArray();
                Experiments.Transient(outDir, tag, ms, GetInt(opt, "seeds", 5),
                                      GetDouble(opt, "tmax", ModelParameters.MinutesPerYear), GetDouble(opt, "step", 1000));
                Console.WriteLine($"Дані перехідного періоду збережено в {outDir}/transient_{tag}.csv");
                break;
            case "series":
                Experiments.Series(outDir, Get(opt, "tag", "main"), ParametersFrom(opt), GetInt(opt, "reps", 20),
                                   GetInt(opt, "seedbase", 1000), GetDouble(opt, "twarm", 3000),
                                   GetDouble(opt, "tstat", ModelParameters.MinutesPerYear),
                                   GetInt(opt, "kmin", 0), GetInt(opt, "kmax", 10));
                break;
            default:
                Console.WriteLine("Команди: run | trace | verify | transient | series");
                return 1;
        }
        return 0;
    }

    private static void RunSingle(Dictionary<string, string> opt)
    {
        ModelParameters p = ParametersFrom(opt);
        int seed = GetInt(opt, "seed", 1);
        double tmod = GetDouble(opt, "tmod", ModelParameters.MinutesPerYear);
        double twarm = GetDouble(opt, "twarm", 0);
        int trace = GetInt(opt, "trace", 0);
        Console.WriteLine("Параметри моделі:");
        Console.WriteLine($"  смуг m = {p.Runways}; інтервал надходження {p.InterarrivalDist}({p.MeanInterarrival}) хв; зайнятість смуги {p.OccupancyDist}({p.LandingOccupancy}) хв");
        Console.WriteLine($"  дозаправка: {(p.RefuelEnabled ? $"після unif({p.RefuelThresholdMean},{p.RefuelThresholdDev}) хв очікування" : "вимкнена")}; відхід: {p.PatienceDist}({p.MaxWait}) хв");
        Console.WriteLine($"  зерно {seed}; Tmod = {tmod} хв; Twarm = {twarm} хв");
        EventLog? log = trace > 0 ? new EventLog(Console.Out, trace) : null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        SimulationResult r = Experiments.Run(p, seed, tmod, twarm, log);
        sw.Stop();

        Console.WriteLine();
        Console.WriteLine($"Час збору статистики T = {r.Period} хв ({r.Period / 1440:F1} діб); подій: {r.Events}; час обчислення {sw.ElapsedMilliseconds} мс");
        Console.WriteLine($"Надійшло літаків:                  {r.Arrivals}");
        Console.WriteLine($"Сіли без очікування (2000 грн):    {r.LandedNoWait}");
        Console.WriteLine($"Сіли після очікування (1500±100):  {r.LandedAfterWait}");
        Console.WriteLine($"Дозаправок (1000±200 грн):         {r.Refuels}");
        Console.WriteLine($"Відлетіли в інший аеропорт:        {r.Diverted} ({r.ShareDiverted:P2})");
        Console.WriteLine($"У черзі / на смугах наприкінці:    {r.QueueAtEnd} / {r.BusyAtEnd}");
        Console.WriteLine($"Середня довжина черги:             {r.MeanQueue:F4} (Літтл: {r.LittleQueue:F4}), max {r.MaxQueue}");
        Console.WriteLine($"Середнє очікування тих, хто чекав: {r.MeanWaitLanded:F2} хв, max {r.MaxWaitLanded:F2} хв");
        Console.WriteLine($"Середнє завантаження смуги:        {r.Utilization:F4}");
        Console.WriteLine($"Дохід / витрати на дозаправку:     {r.Income:F2} / {r.RefuelCost:F2} грн");
        Console.WriteLine($"Прибуток за період:                {r.NetProfit:F2} грн");
        Console.WriteLine($"Річний прибуток p:                 {r.AnnualProfit:F2} грн/рік");
    }

    private static ModelParameters ParametersFrom(Dictionary<string, string> opt) => new()
    {
        Runways = GetInt(opt, "m", ModelParameters.BaseRunways),
        MeanInterarrival = GetDouble(opt, "interarrival", 6.0),
        LandingOccupancy = GetDouble(opt, "occupancy", 35.0),
        MaxWait = GetDouble(opt, "maxwait", 140.0),
        RefuelThresholdMean = GetDouble(opt, "threshold", 70.0),
        RefuelEnabled = !opt.ContainsKey("norefuel"),
        PatienceDist = opt.ContainsKey("noleave") ? Dist.None : Dist.Det,
        OccupancyDist = opt.ContainsKey("expservice") ? Dist.Exp : Dist.Det,
        InterarrivalDist = opt.ContainsKey("detarrivals") ? Dist.Det : Dist.Exp
    };

    private static Dictionary<string, string> ParseOptions(string[] a)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].StartsWith("--")) continue;
            string key = a[i][2..];
            string value = i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[++i] : "true";
            d[key] = value;
        }
        return d;
    }

    private static string Get(Dictionary<string, string> o, string k, string def) => o.TryGetValue(k, out var v) ? v : def;
    private static int GetInt(Dictionary<string, string> o, string k, int def) => o.TryGetValue(k, out var v) ? int.Parse(v, CultureInfo.InvariantCulture) : def;
    private static double GetDouble(Dictionary<string, string> o, string k, double def) => o.TryGetValue(k, out var v) ? double.Parse(v, CultureInfo.InvariantCulture) : def;
}
