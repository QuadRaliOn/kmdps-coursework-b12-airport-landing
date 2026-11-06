using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AirportSim;

public static class Experiments
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() }
    };

    public static SimulationResult Run(ModelParameters p, int seed, double tmod, double twarm = 0.0, EventLog? log = null)
        => new Model(p, seed, log).Simulate(tmod, twarm);

    public static SimulationResult[] Replicate(ModelParameters p, IEnumerable<int> seeds, double tmod, double twarm)
        => seeds.AsParallel().AsOrdered().Select(s => Run(p, s, tmod, twarm)).ToArray();

    public static void Trace(string outDir)
    {
        Directory.CreateDirectory(outDir);

        using (var w = new StreamWriter(Path.Combine(outDir, "trace_random_m2.txt"), false, new UTF8Encoding(false)))
        {
            var p = new ModelParameters { Runways = 2 };
            w.WriteLine($"Протокол подій. {p}. Зерно 1, Tmod = 300 хв.");
            var r = Run(p, 1, 300.0, 0.0, new EventLog(w));
            w.WriteLine();
            w.WriteLine($"Підсумок: надійшло {r.Arrivals}, сіли без очікування {r.LandedNoWait}, після очікування {r.LandedAfterWait}, " +
                        $"дозаправок {r.Refuels}, відльотів {r.Diverted}, у черзі наприкінці {r.QueueAtEnd}.");
        }

        using (var w = new StreamWriter(Path.Combine(outDir, "trace_deterministic_m2.txt"), false, new UTF8Encoding(false)))
        {
            var p = DeterministicCheckParameters();
            w.WriteLine($"Протокол подій (детерміновані вхідні дані для ручної перевірки). {p}. Tmod = 420 хв.");
            var r = Run(p, 1, 420.0, 0.0, new EventLog(w));
            w.WriteLine();
            w.WriteLine($"Підсумок: надійшло {r.Arrivals}, сіли без очікування {r.LandedNoWait}, після очікування {r.LandedAfterWait}, " +
                        $"дозаправок {r.Refuels}, відльотів {r.Diverted}, у черзі наприкінці {r.QueueAtEnd}, " +
                        $"прибуток {r.NetProfit.ToString("F2", Inv)} грн.");
        }
    }

    public static ModelParameters DeterministicCheckParameters() => new()
    {
        Runways = 2,
        InterarrivalDist = Dist.Det,
        RefuelThresholdDev = 0.0,
        RefuelCostDev = 0.0,
        ProfitWaitDev = 0.0
    };

    public static void Verify(string outDir)
    {
        Directory.CreateDirectory(outDir);
        const double lambda = 1.0 / 6.0, mu = 1.0 / 35.0;
        var report = new Dictionary<string, object>();

        var v1 = new List<object>();
        foreach (int m in new[] { 6, 7, 8, 10 })
        {
            var p = new ModelParameters
            {
                Runways = m, OccupancyDist = Dist.Exp, PatienceDist = Dist.None, RefuelEnabled = false
            };
            var runs = Replicate(p, Enumerable.Range(1, 10), 2_050_000, 50_000);
            var a = Analytic.ErlangC(m, lambda, mu);
            v1.Add(new
            {
                m,
                reps = runs.Length,
                tmod = 2_050_000,
                twarm = 50_000,
                sim = Summary(runs),
                analytic = a
            });
        }
        report["mmm_erlangC"] = v1;

        var v2 = new List<object>();
        foreach (int m in new[] { 2, 4, 6, 8 })
        {
            var p = new ModelParameters
            {
                Runways = m, OccupancyDist = Dist.Exp, PatienceDist = Dist.Exp, RefuelEnabled = false
            };
            var runs = Replicate(p, Enumerable.Range(1, 10), 1_010_000, 10_000);
            var a = Analytic.ErlangA(m, lambda, mu, 1.0 / 140.0);
            v2.Add(new { m, reps = runs.Length, tmod = 1_010_000, twarm = 10_000, sim = Summary(runs), analytic = a });
        }
        report["mmm_erlangA"] = v2;

        var cases = new List<(string label, ModelParameters p)>
        {
            ("базовий: m = 2", new ModelParameters()),
            ("m = 3", new ModelParameters { Runways = 3 }),
            ("m = 4", new ModelParameters { Runways = 4 }),
            ("m = 5", new ModelParameters { Runways = 5 }),
            ("m = 6", new ModelParameters { Runways = 6 }),
            ("m = 7", new ModelParameters { Runways = 7 }),
            ("m = 12", new ModelParameters { Runways = 12 }),
            ("m = 2, інтервал exp(12)", new ModelParameters { MeanInterarrival = 12 }),
            ("m = 2, інтервал exp(30)", new ModelParameters { MeanInterarrival = 30 }),
            ("m = 2, смуга 20 хв", new ModelParameters { LandingOccupancy = 20 }),
            ("m = 2, w_max = 200 хв", new ModelParameters { MaxWait = 200 }),
            ("m = 2, інтервал 6 (детерм.)", new ModelParameters { InterarrivalDist = Dist.Det }),
            ("m = 6, θ = unif(100, 10)", new ModelParameters { Runways = 6, RefuelThresholdMean = 100 }),
            ("m = 6, без дозаправки", new ModelParameters { Runways = 6, RefuelEnabled = false }),
            ("m = 6, без відходу", new ModelParameters { Runways = 6, PatienceDist = Dist.None }),
        };
        var v3 = new List<object>();
        foreach (var (label, p) in cases)
        {
            var r = Run(p, 1, ModelParameters.MinutesPerYear, 0.0);
            long balance = r.Arrivals - r.Landed - r.Diverted - r.QueueAtEnd;
            v3.Add(new
            {
                label,
                parameters = p,
                result = r,
                conservation = balance,
                capacityPerYear = p.Runways * ModelParameters.MinutesPerYear / p.LandingOccupancy,
                demandPerYear = ModelParameters.MinutesPerYear / p.MeanInterarrival
            });
        }
        report["table"] = v3;

        var det = Run(DeterministicCheckParameters(), 1, 420.0);
        report["deterministic"] = det;

        File.WriteAllText(Path.Combine(outDir, "verification.json"), JsonSerializer.Serialize(report, JsonOptions));
        PrintVerification(v1, v2);
    }

    private static object Summary(SimulationResult[] runs) => new
    {
        Lq = runs.Average(r => r.MeanQueue),
        LqSd = StdDev(runs.Select(r => r.MeanQueue)),
        Wq = runs.Average(r => r.MeanWaitPerArrival),
        PWait = runs.Average(r => r.ShareEnqueued),
        Utilization = runs.Average(r => r.Utilization),
        PAbandon = runs.Average(r => r.ShareDiverted),
        Throughput = runs.Average(r => r.Landed / r.Period),
        Little = runs.Average(r => r.LittleQueue)
    };

    private static void PrintVerification(List<object> v1, List<object> v2)
    {
        Console.WriteLine("Верифікація збережена у verification.json");
        Console.WriteLine(JsonSerializer.Serialize(new { v1, v2 }, JsonOptions));
    }

    public static void Transient(string outDir, string tag, int[] runwayCounts, int seeds, double tmax, double step)
    {
        Directory.CreateDirectory(outDir);
        var rows = new System.Collections.Concurrent.ConcurrentBag<(int m, int seed, string line)>();
        var jobs = from m in runwayCounts from s in Enumerable.Range(1, seeds) select (m, s);
        jobs.AsParallel().ForAll(job =>
        {
            var model = new Model(new ModelParameters { Runways = job.m }, job.s);
            var sb = new StringBuilder();
            double next = step;
            model.Simulate(tmax, 0.0, md =>
            {
                while (md.Tcurr >= next && next <= tmax)
                {
                    RunwayStatistics st = md.Runways.Stats;
                    sb.AppendLine(string.Join(",",
                        job.m, job.s, F(next), F(md.Tcurr), F(st.QueueArea), F(st.BusyArea), F(st.Income),
                        F(st.RefuelCost), md.Source.Quantity, st.LandedNoWait + st.LandedAfterWait, st.Diverted,
                        st.Refuels, md.Runways.QueueLength));
                    next += step;
                }
            });
            rows.Add((job.m, job.s, sb.ToString()));
        });
        using var w = new StreamWriter(Path.Combine(outDir, $"transient_{tag}.csv"), false, new UTF8Encoding(false));
        w.WriteLine("m,seed,checkpoint,t,queueArea,busyArea,income,refuelCost,arrivals,landed,diverted,refuels,queueNow");
        foreach (var r in rows.OrderBy(r => r.m).ThenBy(r => r.seed)) w.Write(r.line);
    }

    public static List<SimulationResult> Series(string outDir, string tag, ModelParameters baseParams, int reps,
                                                int seedBase, double twarm, double tstat, int kmin = 0, int kmax = 10)
    {
        Directory.CreateDirectory(outDir);
        var jobs = (from k in Enumerable.Range(kmin, kmax - kmin + 1)
                    from r in Enumerable.Range(1, reps)
                    select (k, seed: seedBase + r)).ToList();
        var results = jobs.AsParallel().AsOrdered()
            .Select(j => Run(baseParams with { Runways = ModelParameters.BaseRunways + j.k }, j.seed, twarm + tstat, twarm))
            .ToList();

        string path = Path.Combine(outDir, $"series_{tag}.csv");
        using (var w = new StreamWriter(path, false, new UTF8Encoding(false)))
        {
            w.WriteLine("tag,k,m,seed,interarrival,occupancy,twarm,period,arrivals,landedNoWait,landedAfterWait,refuels,diverted," +
                        "divertedRefueled,income,refuelCost,netProfit,annualProfit,meanQueue,maxQueue,meanBusy,utilization," +
                        "meanWaitLanded,meanWaitAllLanded,maxWaitLanded,shareDiverted,shareRefueled,shareLandedAfterWait,events");
            foreach (var r in results)
                w.WriteLine(string.Join(",", tag, r.Runways - ModelParameters.BaseRunways, r.Runways, r.Seed,
                    F(baseParams.MeanInterarrival), F(baseParams.LandingOccupancy), F(r.Twarm), F(r.Period), r.Arrivals,
                    r.LandedNoWait, r.LandedAfterWait, r.Refuels, r.Diverted, r.DivertedRefueled, F(r.Income),
                    F(r.RefuelCost), F(r.NetProfit), F(r.AnnualProfit), F(r.MeanQueue), r.MaxQueue, F(r.MeanBusy),
                    F(r.Utilization), F(r.MeanWaitLanded), F(r.MeanWaitAllLanded), F(r.MaxWaitLanded),
                    F(r.ShareDiverted), F(r.ShareRefueled), F(r.ShareLandedAfterWait), r.Events));
        }
        PrintPayback(results, tag);
        return results;
    }

    public static void PrintPayback(List<SimulationResult> results, string tag)
    {
        var bySeed = results.GroupBy(r => r.Seed).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Runways));
        Console.WriteLine();
        Console.WriteLine($"Серія «{tag}»: {bySeed.Count} реплікацій на кожне k");
        Console.WriteLine("  k |  m |  p(m), млн грн/рік | Δp(k), млн грн/рік |  T(k), років |  T(k), діб");
        Console.WriteLine(new string('-', 84));
        foreach (int m in results.Select(r => r.Runways).Distinct().OrderBy(x => x))
        {
            int k = m - ModelParameters.BaseRunways;
            double pm = bySeed.Values.Average(d => d[m].AnnualProfit);
            string dp = "", T = "", Td = "";
            if (k > 0 && bySeed.Values.All(d => d.ContainsKey(ModelParameters.BaseRunways)))
            {
                var t = bySeed.Values.Select(d => ModelParameters.RunwayCost * k /
                                                   (d[m].AnnualProfit - d[ModelParameters.BaseRunways].AnnualProfit)).ToList();
                double dpm = bySeed.Values.Average(d => d[m].AnnualProfit - d[ModelParameters.BaseRunways].AnnualProfit);
                dp = (dpm / 1e6).ToString("F3", Inv);
                T = t.Average().ToString("F5", Inv);
                Td = (t.Average() * 365).ToString("F2", Inv);
            }
            Console.WriteLine($"{k,3} | {m,2} | {(pm / 1e6).ToString("F3", Inv),18} | {dp,18} | {T,12} | {Td,10}");
        }
    }

    private static string F(double x) => x.ToString("R", Inv);

    private static double StdDev(IEnumerable<double> xs)
    {
        var a = xs.ToArray();
        double mean = a.Average();
        return Math.Sqrt(a.Sum(x => (x - mean) * (x - mean)) / (a.Length - 1));
    }
}
