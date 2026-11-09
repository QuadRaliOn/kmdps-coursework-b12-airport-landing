import json
import sys
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
from scipy import stats

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[1]
RES = ROOT / "results"
FIG = ROOT / "figures"
FIG.mkdir(exist_ok=True)

YEAR = 525_600.0
RUNWAY_COST = 3_000_000.0
BETA = 0.95

plt.rcParams.update({
    "font.family": "Times New Roman",
    "font.size": 12,
    "axes.titlesize": 12,
    "axes.labelsize": 12,
    "legend.fontsize": 10.5,
    "figure.dpi": 100,
    "savefig.dpi": 200,
    "axes.grid": True,
    "grid.alpha": 0.3,
})
COLORS = ["#1f4e79", "#c0504d", "#4f8a3a", "#8064a2", "#e08a1e", "#2a9d8f", "#6b6b6b"]

def save(fig, name):
    fig.tight_layout()
    fig.savefig(FIG / name, bbox_inches="tight")
    plt.close(fig)

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
                events=("events", "mean")).reset_index()
    return out

summary = {}

welch = pd.read_csv(RES / "transient_welch.csv", usecols=["m", "seed", "checkpoint", "queueNow"])
welch6 = pd.read_csv(RES / "transient_welch6.csv", usecols=["m", "seed", "checkpoint", "queueNow"])
WIN = 10                      # півширина вікна ковзного середнього, точок по 50 хв (вікно 1050 хв)


def welch_end(e, steady_from):
    """Метод Велча: усереднення за прогонами, ковзне середнє; кінець перехідного періоду —
    перший момент, коли ковзне середнє входить у смугу steady ± max(5 %, 2σ шуму усередненої кривої)."""
    v = e.values
    sm = np.empty_like(v, dtype=float)
    for i in range(len(v)):             # ковзне середнє Велча: на початку вікно 2i − 1, далі 2w + 1
        h = min(i, WIN, len(v) - 1 - i)
        sm[i] = v[i - h:i + h + 1].mean()
    ma = pd.Series(sm, index=e.index)
    tail = e[e.index >= steady_from]
    steady, noise = tail.mean(), tail.std()
    band = max(0.05 * steady, 2 * noise)
    ok = (ma - steady).abs() <= band
    t_w = next((int(t) for t in ma.index if ok[t]), None)
    return ma, steady, noise, band, t_w


fig, ax = plt.subplots(figsize=(7.2, 4.2))
warm = {}
curves = {int(m): (g, 10000, int(g.seed.nunique()), 20000) for m, g in welch.groupby("m") if m != 6}
curves[6] = (welch6, 20000, int(welch6.seed.nunique()), int(welch6.checkpoint.max()))
for i, m in enumerate(sorted(curves)):
    g, steady_from, reps, tmax = curves[m]
    e = g.groupby("checkpoint").queueNow.mean()
    ma, steady, noise, band, t_w = welch_end(e, steady_from)
    warm[m] = {"steady": steady, "noise": noise, "band": band, "t_warm": t_w, "reps": reps, "tmax": tmax,
               "steady_from": steady_from}
    sel = ma[ma.index <= 20000]
    ax.plot(sel.index, sel.values, color=COLORS[i % len(COLORS)], lw=1.3, label=f"m = {m}")
ax.axvline(3000, color="k", ls="--", lw=1)
ax.text(3200, 14.5, "T$_{warm}$ = 3000 хв", fontsize=12)
ax.set_xlim(0, 20000)
ax.set_xlabel("Модельний час t, хв")
ax.set_ylabel("Середня за прогонами довжина черги, літаків")
ax.legend(loc="center right", ncol=2)
save(fig, "fig_4_1_warmup.png")
summary["warmup"] = {"replications": int(welch.seed.nunique()), "window": 2 * WIN + 1, "by_m": warm, "chosen": 3000}

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

fig, axes = plt.subplots(1, 2, figsize=(7.6, 3.6), sharex=True)
for ax, m in zip(axes, (7, 6)):
    g = cum[cum.m == m]
    for j, (s, gs) in enumerate(g.groupby("seed")):
        ax.plot(gs.t / 1440, gs.p, lw=1.1, color=COLORS[j % len(COLORS)], label=f"прогін {s}")
    end = g[g.checkpoint == g.checkpoint.max()].p.mean()
    ax.set_ylim(end - 12, end + 12)
    ax.set_title(f"m = {m}")
    ax.set_xlabel("Модельний час t, діб")
axes[0].set_ylabel("Накопичене середнє p(t), млн грн/рік")
axes[0].legend(fontsize=9, loc="lower right")
save(fig, "fig_4_2_cumulative.png")

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
                            "cochran_G": G, "cochran_crit": G_crit, "s2_max_k": int(sorted(Tmat)[int(s2.argmax())]),
                            "near": {"F": F_n, "F_crit": stats.f.ppf(0.95, 2, 3 * (n - 1)),
                                     "D_f": SSf_n / 2, "D_r": SSr_n / (3 * (n - 1))}}
summary["main"]["kruskal"] = dict(zip(("H", "p_value"), stats.kruskal(*[Tmat[k].values for k in sorted(Tmat)])))


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


def chi2_normal(x, alpha=0.05):
    """Критерій χ² Пірсона: рівні інтервали за Стерджесом, об’єднання інтервалів з очікуваною частотою < 5."""
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
            "df": dfree, "crit": float(stats.chi2.ppf(1 - alpha, dfree)), "crit01": float(stats.chi2.ppf(0.99, dfree)),
            "p_value": float(stats.chi2.sf(chi, dfree)), "shapiro_p": float(stats.shapiro(x).pvalue),
            "skew": float(stats.skew(x)), "kurt": float(stats.kurtosis(x))}


dist = {"T5_days": chi2_normal(Tmat[kstar].values * 365),
        "p7_mln": chi2_normal(mpiv[kstar].values / 1e6)}
summary["main"]["distribution"] = dist
fig, axes = plt.subplots(1, 2, figsize=(7.8, 3.6))
for ax, key, xlabel in ((axes[0], "T5_days", f"Час окупності T$_l$({kstar}), діб"),
                        (axes[1], "p7_mln", f"Річний прибуток p$_l$({kstar + 2}), млн грн/рік")):
    d_ = dist[key]
    edges = np.array(d_["edges"])
    ax.hist(edges[:-1], bins=edges, weights=d_["obs"], color=COLORS[0], alpha=0.75, edgecolor="white",
            label="частоти (n = %d)" % d_["n"])
    xs = np.linspace(edges[0], edges[-1], 200)
    width = edges[1] - edges[0]
    ax.plot(xs, d_["n"] * width * stats.norm.pdf(xs, d_["mean"], d_["sd"]), color=COLORS[1], lw=1.6,
            label="нормальний закон")
    ax.set_xlabel(xlabel)
    ax.set_ylabel("Частота")
    ax.legend(fontsize=9, loc="upper left" if key == "p7_mln" else "upper right", framealpha=0.95)
    ax.set_ylim(0, max(d_["obs"]) * 1.35)
save(fig, "fig_4_7_hist.png")

fig, ax = plt.subplots(figsize=(7.0, 4.0))
ax.errorbar(mt.m, mt.p_mean / 1e6, yerr=mt.p_ci / 1e6, fmt="o-", color=COLORS[0], capsize=3, lw=1.5)
ax.axhline(0, color="k", lw=0.8)
ax.set_xlabel("Кількість посадкових смуг m = 2 + k, шт.")
ax.set_ylabel("Річний прибуток p(m), млн грн/рік")
ax.set_xticks(range(2, 13))
save(fig, "fig_4_3_profit.png")

tk = mt[mt.k > 0]
fig, ax = plt.subplots(figsize=(7.0, 4.2))
ax.errorbar(tk.k, tk.T_mean * 365, yerr=tk.T_ci * 365, fmt="o-", color=COLORS[1], capsize=3, lw=1.5)
ax.plot([kstar], [best.T_mean * 365], "o", ms=12, mfc="none", mec="k", mew=1.5)
ax.annotate(f"k* = {kstar}: T = {best.T_mean * 365:.2f} діб", (kstar, best.T_mean * 365),
            xytext=(kstar + 1.15, best.T_mean * 365 + 0.6), fontsize=11,
            arrowprops=dict(arrowstyle="->", lw=0.8))
ax.set_xlabel("Кількість додаткових смуг k, шт.")
ax.set_ylabel("Час окупності T(k), діб")
ax.set_xticks(range(1, 11))
sec = ax.secondary_yaxis("right", functions=(lambda d: d / 365, lambda y: y * 365))
sec.set_ylabel("Час окупності T(k), років")
save(fig, "fig_4_4_payback.png")

fig, axes = plt.subplots(1, 2, figsize=(7.8, 3.6))
ax = axes[0]
ax.plot(ind.m, ind.shareDiverted * 100, "o-", color=COLORS[1], label="відлетіли в інший аеропорт")
ax.plot(ind.m, ind.shareRefueled * 100, "s-", color=COLORS[4], label="потребували дозаправки")
ax.plot(ind.m, ind.shareWait * 100, "^-", color=COLORS[0], label="посадки з очікуванням")
ax.set_xlabel("Кількість смуг m, шт.")
ax.set_ylabel("Частка літаків, %")
ax.set_xticks(range(2, 13))
ax.legend(fontsize=9)
ax = axes[1]
l1 = ax.plot(ind.m, ind.meanQueue, "o-", color=COLORS[0], label="середня довжина черги")
ax.set_xlabel("Кількість смуг m, шт.")
ax.set_ylabel("Середня довжина черги, літаків")
ax.set_xticks(range(2, 13))
ax2 = ax.twinx()
l2 = ax2.plot(ind.m, ind.util, "s--", color=COLORS[2], label="завантаження смуги")
ax2.set_ylabel("Середнє завантаження смуги ρ")
ax2.set_ylim(0, 1.05)
ax2.grid(False)
ax.legend(l1 + l2, [l.get_label() for l in l1 + l2], fontsize=9, loc="upper right")
save(fig, "fig_4_5_service.png")

fig, ax = plt.subplots(figsize=(6.6, 3.8))
data = [Tmat[k].values * 365 for k in (3, 4, 5, 6, 7)]
ax.boxplot(data, tick_labels=["3", "4", "5", "6", "7"], widths=0.5)
ax.set_xlabel("Кількість додаткових смуг k, шт.")
ax.set_ylabel("Час окупності в реплікації T$_l$(k), діб")
save(fig, "fig_4_6_boxplot.png")

scen = {}
fig, ax = plt.subplots(figsize=(7.0, 4.2))
labels = {"lambda8": "λ = 8 літаків/год", "main": "λ = 10 літаків/год (умова)", "lambda12": "λ = 12 літаків/год",
          "occ30": "λ = 10, зайнятість смуги 30 хв"}
styles = {"lambda8": ("s--", COLORS[2]), "main": ("o-", COLORS[1]), "lambda12": ("^--", COLORS[3]),
          "occ30": ("d:", COLORS[0])}
for tag in ("lambda8", "main", "lambda12", "occ30"):
    df = pd.read_csv(RES / f"series_{tag}.csv")
    t, _ = payback_table(df)
    tt = t[t.k > 0]
    b = tt.sort_values("T_mean").iloc[0]
    scen[tag] = {"n": int(df.seed.nunique()), "best_k": int(b.k), "best_T_days": b.T_mean * 365,
                 "table": t.to_dict(orient="records"), "indicators": series_indicators(df).to_dict(orient="records")}
    fmt, col = styles[tag]
    ax.plot(tt.k, tt.T_mean * 365, fmt, color=col, label=f"{labels[tag]}: k* = {int(b.k)}", lw=1.4)
ax.set_xlabel("Кількість додаткових смуг k, шт.")
ax.set_ylabel("Час окупності T(k), діб")
ax.set_xticks(range(1, 11))
ax.legend(fontsize=9.5)
save(fig, "fig_5_1_sensitivity.png")
summary["scenarios"] = scen

ver = json.loads((RES / "verification.json").read_text(encoding="utf-8"))
summary["verification"] = ver
fig, axes = plt.subplots(1, 2, figsize=(7.8, 3.5))
v1 = ver["mmm_erlangC"]
ax = axes[0]
ms = [r["m"] for r in v1]
x = np.arange(len(ms))
ax.bar(x - 0.18, [r["sim"]["Lq"] for r in v1], 0.36, color=COLORS[0], label="імітація")
ax.bar(x + 0.18, [r["analytic"]["Lq"] for r in v1], 0.36, color=COLORS[4], label="формула Ерланга C")
ax.set_yscale("log")
ax.set_xticks(x, [str(m) for m in ms])
ax.set_xlabel("Кількість пристроїв m, шт.")
ax.set_ylabel("Середня довжина черги, літаків")
ax.set_title("M/M/m")
ax.legend(fontsize=9)
v2 = ver["mmm_erlangA"]
ax = axes[1]
ms = [r["m"] for r in v2]
x = np.arange(len(ms))
ax.bar(x - 0.18, [r["sim"]["PAbandon"] * 100 for r in v2], 0.36, color=COLORS[0], label="імітація")
ax.bar(x + 0.18, [r["analytic"]["PAbandon"] * 100 for r in v2], 0.36, color=COLORS[4], label="модель Ерланга A")
ax.set_xticks(x, [str(m) for m in ms])
ax.set_xlabel("Кількість пристроїв m, шт.")
ax.set_ylabel("Частка відходів з черги, %")
ax.set_title("M/M/m+M")
ax.legend(fontsize=9)
save(fig, "fig_3_4_verification.png")

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
    print("  k=%d: ΔT=%.6f, t=%.2f, p=%.3g, частка=%.2f" % (p_["k"], p_["diff_mean"], p_["t"], p_["p_value"], p_["share_reps_better"]))
print("ANOVA:", {k: v for k, v in summary["main"]["anova"].items() if k != "near"}, summary["main"]["anova"]["near"])
print("Kruskal:", summary["main"]["kruskal"])
print("Cochran:", summary["main"]["cochran_steps"])
print("ANOVA ln:", summary["main"]["anova_ln_stable"])
print("Dist:", {k: {kk: v[kk] for kk in ("mean", "sd", "chi2", "df", "crit", "p_value", "shapiro_p", "obs")} for k, v in dist.items()})
print("Warm:", {m: v["t_warm"] for m, v in warm.items()})
print("Long m6:", long6)
for tag, s in scen.items():
    print(tag, "k* =", s["best_k"], "T =", round(s["best_T_days"], 2), "діб")
