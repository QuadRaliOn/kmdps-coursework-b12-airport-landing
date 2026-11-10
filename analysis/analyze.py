import json
import sys
from pathlib import Path

import numpy as np
import pandas as pd
from scipy import stats

sys.path.insert(0, str(Path(__file__).resolve().parent))
import plots

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[1]
RES = ROOT / "results"

YEAR = 525_600.0
RUNWAY_COST = 3_000_000.0
BETA = 0.95


def ci_half(x, beta=BETA):
    x = np.asarray(x, float)
    n = len(x)
    return stats.t.ppf((1 + beta) / 2, n - 1) * x.std(ddof=1) / np.sqrt(n)

def payback_table(df):
    piv = df.pivot(index="seed", columns="k", values="annualProfit")
    rows = []
    for k in sorted(piv.columns):
        p = piv[k]
        row = {"k": int(k), "m": int(k) + 2, "n": int(p.count()),
               "p_mean": p.mean(), "p_sd": p.std(ddof=1), "p_ci": ci_half(p)}
        if k > 0:
            dp = piv[k] - piv[0]
            T = RUNWAY_COST * k / dp
            Ttot = RUNWAY_COST * k / piv[k]
            row.update({"dp_mean": dp.mean(), "dp_sd": dp.std(ddof=1), "dp_ci": ci_half(dp),
                        "T_mean": T.mean(), "T_sd": T.std(ddof=1), "T_ci": ci_half(T),
                        "T_ratio": RUNWAY_COST * k / dp.mean(),
                        "Ttot": RUNWAY_COST * k / p.mean() if p.mean() > 0 else None})
        rows.append(row)
    return pd.DataFrame(rows), piv

def series_indicators(df):
    g = df.groupby("m")
    out = g.agg(meanQueue=("meanQueue", "mean"), maxQueue=("maxQueue", "max"), util=("utilization", "mean"),
                shareDiverted=("shareDiverted", "mean"), shareRefueled=("shareRefueled", "mean"),
                shareWait=("shareLandedAfterWait", "mean"), wait=("meanWaitLanded", "mean"),
                waitAll=("meanWaitAllLanded", "mean"), arrivals=("arrivals", "mean"),
                landed0=("landedNoWait", "mean"), landedW=("landedAfterWait", "mean"),
                refuels=("refuels", "mean"), diverted=("diverted", "mean"),
                income=("income", "mean"), refuelCost=("refuelCost", "mean"),
                events=("events", "mean"), waitAllPassed=("meanWaitAll", "mean")).reset_index()
    return out

summary = {}

welch = pd.read_csv(RES / "transient_welch.csv", usecols=["m", "seed", "checkpoint", "queueNow"])
welch6 = pd.read_csv(RES / "transient_welch6.csv", usecols=["m", "seed", "checkpoint", "queueNow"])
WIN = 10


def welch_end(e, steady_from):
    v = e.values
    sm = np.empty_like(v, dtype=float)
    for i in range(len(v)):
        h = min(i, WIN, len(v) - 1 - i)
        sm[i] = v[i - h:i + h + 1].mean()
    ma = pd.Series(sm, index=e.index)
    tail = e[e.index >= steady_from]
    steady, noise = tail.mean(), tail.std()
    band = max(0.05 * steady, 2 * noise)
    ok = (ma - steady).abs() <= band
    t_w = next((int(t) for t in ma.index if ok[t]), None)
    return ma, steady, noise, band, t_w


warm = {}
smoothed = {}
curves = {int(m): (g, 10000, int(g.seed.nunique()), 20000) for m, g in welch.groupby("m") if m != 6}
curves[6] = (welch6, 20000, int(welch6.seed.nunique()), int(welch6.checkpoint.max()))
for m in sorted(curves):
    g, steady_from, reps, tmax = curves[m]
    e = g.groupby("checkpoint").queueNow.mean()
    ma, steady, noise, band, t_w = welch_end(e, steady_from)
    warm[m] = {"steady": steady, "noise": noise, "band": band, "t_warm": t_w, "reps": reps, "tmax": tmax,
               "steady_from": steady_from}
    smoothed[m] = ma
plots.plot_warmup(smoothed)
summary["warmup"] = {"replications": int(welch.seed.nunique()), "window": 2 * WIN + 1, "by_m": warm,
                     "chosen": 3000}

cum = pd.read_csv(RES / "transient_cum.csv")
cum["p"] = (cum.income - cum.refuelCost) / cum.t * YEAR / 1e6
cum["q"] = cum.queueArea / cum.t
conv = {}
for m, g in cum.groupby("m"):
    piv = g.pivot(index="checkpoint", columns="seed", values="p")
    rng = piv.max(axis=1) - piv.min(axis=1)
    conv[int(m)] = {}
    for eps in (5.0, 2.0, 1.0):
        ok = rng <= eps
        conv[int(m)][str(eps)] = next((int(t) for t in rng.index if ok[ok.index >= t].all()), None)
    conv[int(m)]["range_end"] = float(rng.iloc[-1])
    conv[int(m)]["p_end_mean"] = float(piv.iloc[-1].mean())
summary["runlength"] = conv

cum6 = pd.read_csv(RES / "transient_cum6.csv")
cum6["p"] = (cum6.income - cum6.refuelCost) / cum6.t * YEAR / 1e6
piv6 = cum6.pivot(index="checkpoint", columns="seed", values="p")
rng6 = piv6.max(axis=1) - piv6.min(axis=1)
long6 = {"tmax": int(piv6.index.max()), "seeds": int(piv6.shape[1])}
for eps in (5.0, 2.0, 1.0):
    ok = rng6 <= eps
    long6[str(eps)] = next((int(t) for t in rng6.index if ok[ok.index >= t].all()), None)
long6["range_by_year"] = {y: float(rng6[rng6.index <= y * YEAR].iloc[-1]) for y in range(1, 6)}
summary["runlength_m6_long"] = long6

plots.plot_cumulative(cum)

pilot = pd.read_csv(RES / "series_pilot.csv")
pt, _ = payback_table(pilot)
eps_T = 0.0005
eps_p = 1.0e6
need = []
for _, r in pt.iterrows():
    row = {"k": int(r.k), "m": int(r.m), "p_sd": r.p_sd, "n_p": 20 * (r.p_sd / eps_p) ** 2}
    if r.k > 0:
        row.update({"T_mean": r.T_mean, "T_sd": r.T_sd, "n_T": 20 * (r.T_sd / eps_T) ** 2})
    need.append(row)
summary["pilot"] = {"n0": int(pilot.seed.nunique()), "eps_T": eps_T, "eps_p": eps_p, "rows": need,
                    "n_T_max": max(x.get("n_T", 0) for x in need), "n_p_max": max(x["n_p"] for x in need)}

main = pd.read_csv(RES / "series_main.csv")
mt, mpiv = payback_table(main)
ind = series_indicators(main)
summary["main"] = {"n": int(main.seed.nunique()), "table": mt.to_dict(orient="records"),
                   "indicators": ind.to_dict(orient="records")}
best = mt[mt.k > 0].sort_values("T_mean").iloc[0]
summary["main"]["best_k"] = int(best.k)
summary["main"]["best_T_years"] = best.T_mean
summary["main"]["best_T_days"] = best.T_mean * 365

Tmat = {int(k): RUNWAY_COST * k / (mpiv[k] - mpiv[0]) for k in mpiv.columns if k > 0}
kstar = int(best.k)
pairs = []
for k2 in sorted(Tmat):
    if k2 == kstar:
        continue
    d = Tmat[k2] - Tmat[kstar]
    t = d.mean() / (d.std(ddof=1) / np.sqrt(len(d)))
    pairs.append({"k": k2, "diff_mean": d.mean(), "diff_sd": d.std(ddof=1), "t": t,
                  "t_crit": stats.t.ppf(0.975, len(d) - 1), "p_value": 2 * stats.t.sf(abs(t), len(d) - 1),
                  "share_reps_better": float((d > 0).mean())})
summary["main"]["paired"] = pairs

q = len(Tmat)
n = len(next(iter(Tmat.values())))
Y = np.array([Tmat[k].values for k in sorted(Tmat)])
ybar_j = Y.mean(axis=1)
ybar = Y.mean()
SS_f = n * ((ybar_j - ybar) ** 2).sum()
SS_r = ((Y - ybar_j[:, None]) ** 2).sum()
D_f = SS_f / (q - 1)
D_r = SS_r / (q * (n - 1))
F = D_f / D_r
F_crit = stats.f.ppf(0.95, q - 1, q * (n - 1))
s2 = Y.var(axis=1, ddof=1)
G = s2.max() / s2.sum()
f1, f2 = n - 1, q
G_crit = 1 / (1 + (f2 - 1) / stats.f.ppf(1 - 0.05 / f2, f1, (f2 - 1) * f1))
Yn = np.array([Tmat[k].values for k in (4, 5, 6)])
yj = Yn.mean(axis=1)
SSf_n = n * ((yj - Yn.mean()) ** 2).sum()
SSr_n = ((Yn - yj[:, None]) ** 2).sum()
F_n = (SSf_n / 2) / (SSr_n / (3 * (n - 1)))
summary["main"]["anova"] = {"q": q, "n": n, "SS_f": SS_f, "SS_r": SS_r, "D_f": D_f, "D_r": D_r, "F": F,
                            "F_crit": F_crit, "df1": q - 1, "df2": q * (n - 1),
                            "cochran_G": G, "cochran_crit": G_crit,
                            "s2_max_k": int(sorted(Tmat)[int(s2.argmax())]),
                            "near": {"F": F_n, "F_crit": stats.f.ppf(0.95, 2, 3 * (n - 1)),
                                     "D_f": SSf_n / 2, "D_r": SSr_n / (3 * (n - 1))}}


def cochran(arrs, alpha=0.05):
    s2_ = np.array([np.var(a, ddof=1) for a in arrs])
    q_, n_ = len(arrs), len(arrs[0])
    g = s2_.max() / s2_.sum()
    g_crit = 1 / (1 + (q_ - 1) / stats.f.ppf(1 - alpha / q_, n_ - 1, (q_ - 1) * (n_ - 1)))
    return float(g), float(g_crit)


def anova(arrs):
    Y_ = np.array(arrs)
    q_, n_ = Y_.shape
    yj_ = Y_.mean(axis=1)
    d_f = n_ * ((yj_ - Y_.mean()) ** 2).sum() / (q_ - 1)
    d_r = ((Y_ - yj_[:, None]) ** 2).sum() / (q_ * (n_ - 1))
    return {"q": q_, "df1": q_ - 1, "df2": q_ * (n_ - 1), "D_f": d_f, "D_r": d_r, "F": d_f / d_r,
            "F_crit": stats.f.ppf(0.95, q_ - 1, q_ * (n_ - 1))}


STABLE = list(range(5, 11))
lnT = [np.log(Tmat[k].values) for k in STABLE]
g_ln, gc_ln = cochran(lnT)
g_raw_st, gc_raw_st = cochran([Tmat[k].values for k in STABLE])
summary["main"]["cochran_steps"] = {
    "all_raw": {"G": G, "G_crit": G_crit},
    "stable_raw": {"G": g_raw_st, "G_crit": gc_raw_st},
    "stable_ln": {"G": g_ln, "G_crit": gc_ln},
    "cv_by_k": {int(k): float(Tmat[k].std(ddof=1) / Tmat[k].mean()) for k in sorted(Tmat)},
}
summary["main"]["anova_ln_stable"] = anova(lnT)


def anova_blocks(arrs):
    Y_ = np.array(arrs)
    q_, n_ = Y_.shape
    g = Y_.mean()
    ss_k = n_ * ((Y_.mean(axis=1) - g) ** 2).sum()
    ss_b = q_ * ((Y_.mean(axis=0) - g) ** 2).sum()
    ss_e = ((Y_ - g) ** 2).sum() - ss_k - ss_b
    df_k, df_b, df_e = q_ - 1, n_ - 1, (q_ - 1) * (n_ - 1)
    ms_k, ms_b, ms_e = ss_k / df_k, ss_b / df_b, ss_e / df_e
    return {"df_k": df_k, "df_b": df_b, "df_e": df_e, "MS_k": ms_k, "MS_b": ms_b, "MS_e": ms_e,
            "F_k": ms_k / ms_e, "F_k_crit": stats.f.ppf(0.95, df_k, df_e),
            "F_b": ms_b / ms_e, "F_b_crit": stats.f.ppf(0.95, df_b, df_e)}


summary["main"]["anova_blocks_ln_stable"] = anova_blocks(lnT)


def chi2_normal(x, alpha=0.05):
    x = np.asarray(x, float)
    n_ = len(x)
    kb = int(round(1 + 3.322 * np.log10(n_)))
    edges = np.linspace(x.min(), x.max(), kb + 1)
    obs, _ = np.histogram(x, edges)
    mu, sd = x.mean(), x.std(ddof=1)
    cdf = stats.norm.cdf(edges, mu, sd)
    cdf[0], cdf[-1] = 0.0, 1.0
    exp_ = n_ * np.diff(cdf)
    o, e_, co, ce = [], [], 0, 0.0
    for a, b in zip(obs, exp_):
        co += a
        ce += b
        if ce >= 5:
            o.append(co); e_.append(ce); co, ce = 0, 0.0
    if ce > 0:
        o[-1] += co; e_[-1] += ce
    o, e_ = np.array(o), np.array(e_)
    chi = float(((o - e_) ** 2 / e_).sum())
    dfree = len(o) - 1 - 2
    return {"n": n_, "mean": mu, "sd": sd, "min": float(x.min()), "max": float(x.max()), "cv": sd / mu,
            "bins": kb, "edges": edges.tolist(), "obs": obs.tolist(), "merged": int(len(o)), "chi2": chi,
            "df": dfree, "crit": float(stats.chi2.ppf(1 - alpha, dfree)),
            "crit01": float(stats.chi2.ppf(0.99, dfree)),
            "p_value": float(stats.chi2.sf(chi, dfree)), "shapiro_p": float(stats.shapiro(x).pvalue),
            "skew": float(stats.skew(x)), "kurt": float(stats.kurtosis(x))}


dist = {"T5_days": chi2_normal(Tmat[kstar].values * 365),
        "p7_mln": chi2_normal(mpiv[kstar].values / 1e6)}
summary["main"]["distribution"] = dist
plots.plot_hist(dist, kstar)
plots.plot_profit(mt)
plots.plot_payback(mt, kstar, best.T_mean)
plots.plot_service(ind)
plots.plot_boxplot(Tmat)

scen = {}
scen_tables = {}
for tag in ("lambda8", "main", "lambda12", "occ30"):
    df = pd.read_csv(RES / f"series_{tag}.csv")
    t, _ = payback_table(df)
    tt = t[t.k > 0]
    b = tt.sort_values("T_mean").iloc[0]
    scen[tag] = {"n": int(df.seed.nunique()), "best_k": int(b.k), "best_T_days": b.T_mean * 365,
                 "table": t.to_dict(orient="records"),
                 "indicators": series_indicators(df).to_dict(orient="records")}
    scen_tables[tag] = (tt, int(b.k))
plots.plot_sensitivity(scen_tables)
summary["scenarios"] = scen

scen_piv = {tag: pd.read_csv(RES / f"series_{tag}.csv").pivot(index="seed", columns="k",
                                                               values="annualProfit")
            for tag in ("lambda8", "main", "lambda12", "occ30")}


def two_level(a, b):
    a, b = np.asarray(a, float), np.asarray(b, float)
    g, gc = cochran([a, b])
    an = anova([a, b])
    d = b - a
    t = d.mean() / (d.std(ddof=1) / np.sqrt(len(d)))
    return {"mean_a": a.mean(), "mean_b": b.mean(), "sd_a": a.std(ddof=1), "sd_b": b.std(ddof=1),
            "cochran_G": g, "cochran_crit": gc, **{f"anova_{k}": v for k, v in an.items()},
            "paired_t": t, "t_crit": stats.t.ppf(0.975, len(d) - 1), "diff_mean": d.mean(),
            "diff_sd": d.std(ddof=1)}


def payback(piv, k):
    return RUNWAY_COST * k / (piv[k] - piv[0])


mod = {"profit_m7_vs_m6_occ30": two_level(scen_piv["main"][5] / 1e6, scen_piv["occ30"][4] / 1e6),
       "profit_anova_blocks": anova_blocks([scen_piv["main"][5].values / 1e6, scen_piv["occ30"][4].values / 1e6]),
       "T_best_vs_occ30_best_days": two_level(payback(scen_piv["main"], 5) * 365,
                                              payback(scen_piv["occ30"], 4) * 365)}
sens = {}
for tag, piv in scen_piv.items():
    Ts = {k: payback(piv, k) * 365 for k in range(1, 11)}
    kb = min(Ts, key=lambda k: Ts[k].mean())
    row = {"best_k": kb, "T": {k: [float(Ts[k].mean()), float(ci_half(Ts[k]))] for k in range(3, 8)}}
    if kb != 5:
        d = Ts[5] - Ts[kb]
        row["k5_vs_best"] = {"diff": float(d.mean()),
                             "t": float(d.mean() / (d.std(ddof=1) / np.sqrt(len(d)))),
                             "share": float((d > 0).mean())}
    sens[tag] = row
grid_ln = [np.log(payback(scen_piv[tag], k).values)
           for tag in ("lambda8", "main", "lambda12") for k in range(3, 8)]
g_grid, gc_grid = cochran(grid_ln)
plan = {"cells": len(grid_ln), "n": len(grid_ln[0]), "cochran_G_lnT": g_grid, "cochran_crit": gc_grid}
summary["modification"] = mod
summary["sensitivity"] = sens
summary["plan_lambda_k"] = plan
print("Модифікація:", {k: {kk: round(float(vv), 5) for kk, vv in v.items()} for k, v in mod.items()})
print("Чутливість:", sens)


def pfe22(levels_lambda=(9.8, 10.2), levels_td=(34.3, 35.7), k=5, alpha=0.05):
    rows, cells, extra = [], [], {}
    for i, lam in enumerate(levels_lambda):
        for j, td in enumerate(levels_td):
            piv = pd.read_csv(RES / f"series_pfe_l{lam}_t{td}.csv").pivot(index="seed", columns="k",
                                                                         values="annualProfit")
            cells.append(np.log(payback(piv, k).values))
            x1, x2 = 2 * i - 1, 2 * j - 1
            rows.append([1, x1, x2, x1 * x2])
            extra[f"{lam}/{td}"] = {kk: float(payback(piv, kk).mean() * 365) for kk in (3, 4, 5)}
    X = np.array(rows, float)
    N, n_ = len(cells), len(cells[0])
    ybar_ = np.array([c.mean() for c in cells])
    s2_rep = float(np.mean([c.var(ddof=1) for c in cells]))
    b = X.T @ ybar_ / N
    s_b = np.sqrt(s2_rep / (N * n_))
    g, gc = cochran(cells)
    return {"lambda": list(levels_lambda), "tD": list(levels_td), "k": k, "n": n_, "cochran_G": g,
            "cochran_crit": gc, "ybar": ybar_.tolist(), "T_days": (np.exp(ybar_) * 365).tolist(),
            "b": b.tolist(), "s_b": float(s_b), "t": (np.abs(b) / s_b).tolist(),
            "t_crit": float(stats.t.ppf(1 - alpha / 2, N * (n_ - 1))), "df": N * (n_ - 1),
            "s2_rep": s2_rep, "T_by_cell": extra}


def best_near_lambda(levels_lambda=(9.8, 10.2)):
    out = {}
    for lam in levels_lambda:
        piv = pd.read_csv(RES / f"series_pfe_l{lam}.csv").pivot(index="seed", columns="k", values="annualProfit")
        Ts = {kk: payback(piv, kk) * 365 for kk in range(1, 7)}
        kb = min(Ts, key=lambda kk: Ts[kk].mean())
        d = Ts[5] - Ts[kb]
        out[str(lam)] = {"best_k": kb, "T": {kk: float(Ts[kk].mean()) for kk in range(3, 7)},
                         "k5_minus_best": float(d.mean()),
                         "t": float(d.mean() / (d.std(ddof=1) / np.sqrt(len(d)))) if kb != 5 else 0.0}
    return out


summary["pfe22"] = pfe22()
summary["best_near_lambda"] = best_near_lambda()
summary["main"]["T_total_profit"] = {int(kk): float((RUNWAY_COST * kk / mpiv[kk]).mean()) for kk in (2, 4, 5, 6)}
print("ПФЕ 2²:", summary["pfe22"])
print("λ ± 2 %:", summary["best_near_lambda"])
print("T за повним прибутком:", summary["main"]["T_total_profit"])

ver = json.loads((RES / "verification.json").read_text(encoding="utf-8"))
summary["verification"] = ver
plots.plot_verification(ver)


def clean(o):
    if isinstance(o, dict):
        return {str(k): clean(v) for k, v in o.items()}
    if isinstance(o, (list, tuple)):
        return [clean(v) for v in o]
    if isinstance(o, (np.floating,)):
        return float(o)
    if isinstance(o, (np.integer,)):
        return int(o)
    if isinstance(o, float) and (np.isnan(o) or np.isinf(o)):
        return None
    return o

(RES / "summary.json").write_text(json.dumps(clean(summary), ensure_ascii=False, indent=1), encoding="utf-8")

print("Перехідний період:", {m: v["t_warm"] for m, v in warm.items()})
print("Збіжність (ε = 1 млн):", {m: v["1.0"] for m, v in conv.items()})
print("Пілот: n_T_max = %.1f, n_p_max = %.1f" % (summary["pilot"]["n_T_max"], summary["pilot"]["n_p_max"]))
print(mt[["k", "m", "p_mean", "p_ci", "dp_mean", "T_mean", "T_ci", "T_ratio", "Ttot"]].to_string())
print("k* =", kstar, "T* =", best.T_mean, "років =", best.T_mean * 365, "діб")
for p_ in pairs:
    print("  k=%d: ΔT=%.6f, t=%.2f, p=%.3g, частка=%.2f"
          % (p_["k"], p_["diff_mean"], p_["t"], p_["p_value"], p_["share_reps_better"]))
print("ANOVA:", {k: v for k, v in summary["main"]["anova"].items() if k != "near"},
      summary["main"]["anova"]["near"])
print("Cochran:", summary["main"]["cochran_steps"])
print("ANOVA ln:", summary["main"]["anova_ln_stable"])
print("Dist:", {k: {kk: v[kk] for kk in ("mean", "sd", "chi2", "df", "crit", "p_value", "shapiro_p", "obs")}
                for k, v in dist.items()})
print("Warm:", {m: v["t_warm"] for m, v in warm.items()})
print("Long m6:", long6)
for tag, s in scen.items():
    print(tag, "k* =", s["best_k"], "T =", round(s["best_T_days"], 2), "діб")
