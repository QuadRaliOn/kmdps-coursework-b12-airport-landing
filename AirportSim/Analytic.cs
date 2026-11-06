namespace AirportSim;

/// <summary>Аналітичні формули теорії масового обслуговування для верифікації алгоритму.</summary>
public static class Analytic
{
    /// <param name="Lq">середня довжина черги</param>
    /// <param name="Wq">середній час очікування (усіх запитів, що надійшли), хв</param>
    /// <param name="PWait">імовірність того, що запит стане в чергу</param>
    /// <param name="Utilization">середнє завантаження пристрою</param>
    /// <param name="PAbandon">імовірність відходу з черги</param>
    /// <param name="Throughput">інтенсивність обслуговування, 1/хв</param>
    public sealed record QueueMetrics(double Lq, double Wq, double PWait, double Utilization,
                                      double PAbandon, double Throughput);

    /// <summary>M/M/m з необмеженою чергою (формула Ерланга C). λ, μ — у 1/хв.</summary>
    public static QueueMetrics ErlangC(int m, double lambda, double mu)
    {
        double a = lambda / mu;
        double rho = a / m;
        if (rho >= 1.0) throw new ArgumentException("ρ ≥ 1: стаціонарного режиму немає");
        double term = 1.0, sum = 0.0;            // term = a^n / n!
        for (int n = 0; n < m; n++)
        {
            if (n > 0) term *= a / n;
            sum += term;
        }
        double termM = term * a / m;             // a^m / m!
        double tail = termM / (1.0 - rho);
        double p0 = 1.0 / (sum + tail);
        double pWait = tail * p0;
        double lq = pWait * rho / (1.0 - rho);
        return new QueueMetrics(lq, lq / lambda, pWait, rho, 0.0, lambda);
    }

    /// <summary>
    /// M/M/m+M (модель Ерланга A): експоненціальне терпіння із середнім 1/θ.
    /// Процес загибелі–розмноження: λ_n = λ, μ_n = min(n, m)·μ + max(n − m, 0)·θ.
    /// </summary>
    public static QueueMetrics ErlangA(int m, double lambda, double mu, double theta)
    {
        var w = new List<double> { 1.0 };
        double cur = 1.0;
        for (int n = 1; n < 1_000_000; n++)
        {
            double death = Math.Min(n, m) * mu + Math.Max(n - m, 0) * theta;
            cur *= lambda / death;
            w.Add(cur);
            if (n > m && cur < 1e-20) break;   // хвіст розподілу вже несуттєвий
        }
        double z = w.Sum();
        double lq = 0, busy = 0, pWait = 0;
        for (int n = 0; n < w.Count; n++)
        {
            double pn = w[n] / z;
            lq += Math.Max(n - m, 0) * pn;
            busy += Math.Min(n, m) * pn;
            if (n >= m) pWait += pn;             // PASTA: запит, що надійшов, бачить стаціонарний стан
        }
        double pAbandon = theta * lq / lambda;
        return new QueueMetrics(lq, lq / lambda, pWait, busy / m, pAbandon, busy * mu);
    }
}
