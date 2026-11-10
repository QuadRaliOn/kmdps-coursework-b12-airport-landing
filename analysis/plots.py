from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import numpy as np
from scipy import stats

ROOT = Path(__file__).resolve().parents[1]
FIG = ROOT / "figures"
FIG.mkdir(exist_ok=True)

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


def plot_warmup(curves):
    fig, ax = plt.subplots(figsize=(7.2, 4.2))
    for i, m in enumerate(sorted(curves)):
        ma = curves[m]
        sel = ma[ma.index <= 20000]
        ax.plot(sel.index, sel.values, color=COLORS[i % len(COLORS)], lw=1.3, label=f"m = {m}")
    ax.axvline(3000, color="k", ls="--", lw=1)
    ax.text(3200, 14.5, "T$_{warm}$ = 3000 хв", fontsize=12)
    ax.set_xlim(0, 20000)
    ax.set_xlabel("Модельний час t, хв")
    ax.set_ylabel("Середня за прогонами довжина черги, літаків")
    ax.legend(loc="center right", ncol=2)
    save(fig, "fig_4_1_warmup.png")


def plot_cumulative(cum):
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


def plot_hist(dist, kstar):
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


def plot_profit(mt):
    fig, ax = plt.subplots(figsize=(7.0, 4.0))
    ax.errorbar(mt.m, mt.p_mean / 1e6, yerr=mt.p_ci / 1e6, fmt="o-", color=COLORS[0], capsize=3, lw=1.5)
    ax.axhline(0, color="k", lw=0.8)
    ax.set_xlabel("Кількість посадкових смуг m = 2 + k, шт.")
    ax.set_ylabel("Річний прибуток p(m), млн грн/рік")
    ax.set_xticks(range(2, 13))
    save(fig, "fig_4_3_profit.png")


def plot_payback(mt, kstar, best_T):
    tk = mt[mt.k > 0]
    fig, ax = plt.subplots(figsize=(7.0, 4.2))
    ax.errorbar(tk.k, tk.T_mean * 365, yerr=tk.T_ci * 365, fmt="o-", color=COLORS[1], capsize=3, lw=1.5)
    ax.plot([kstar], [best_T * 365], "o", ms=12, mfc="none", mec="k", mew=1.5)
    ax.annotate(f"k* = {kstar}: T = {best_T * 365:.2f} діб", (kstar, best_T * 365),
                xytext=(kstar + 1.15, best_T * 365 + 0.6), fontsize=11,
                arrowprops=dict(arrowstyle="->", lw=0.8))
    ax.set_xlabel("Кількість додаткових смуг k, шт.")
    ax.set_ylabel("Час окупності T(k), діб")
    ax.set_xticks(range(1, 11))
    sec = ax.secondary_yaxis("right", functions=(lambda d: d / 365, lambda y: y * 365))
    sec.set_ylabel("Час окупності T(k), років")
    save(fig, "fig_4_4_payback.png")


def plot_service(ind):
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


def plot_boxplot(Tmat):
    fig, ax = plt.subplots(figsize=(6.6, 3.8))
    data = [Tmat[k].values * 365 for k in (3, 4, 5, 6, 7)]
    ax.boxplot(data, tick_labels=["3", "4", "5", "6", "7"], widths=0.5)
    ax.set_xlabel("Кількість додаткових смуг k, шт.")
    ax.set_ylabel("Час окупності в реплікації T$_l$(k), діб")
    save(fig, "fig_4_6_boxplot.png")


def plot_sensitivity(scen_tables):
    labels = {"lambda8": "λ = 8 літаків/год", "main": "λ = 10 літаків/год (умова)",
              "lambda12": "λ = 12 літаків/год", "occ30": "λ = 10, зайнятість смуги 30 хв"}
    styles = {"lambda8": ("s--", COLORS[2]), "main": ("o-", COLORS[1]), "lambda12": ("^--", COLORS[3]),
              "occ30": ("d:", COLORS[0])}
    fig, ax = plt.subplots(figsize=(7.0, 4.2))
    for tag, (tt, best_k) in scen_tables.items():
        fmt, col = styles[tag]
        ax.plot(tt.k, tt.T_mean * 365, fmt, color=col, label=f"{labels[tag]}: k* = {best_k}", lw=1.4)
    ax.set_xlabel("Кількість додаткових смуг k, шт.")
    ax.set_ylabel("Час окупності T(k), діб")
    ax.set_xticks(range(1, 11))
    ax.legend(fontsize=9.5)
    save(fig, "fig_5_1_sensitivity.png")


def plot_verification(ver):
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
    ax.bar(x + 0.18, [r["analytic"]["PAbandon"] * 100 for r in v2], 0.36, color=COLORS[4],
           label="модель Ерланга A")
    ax.set_xticks(x, [str(m) for m in ms])
    ax.set_xlabel("Кількість пристроїв m, шт.")
    ax.set_ylabel("Частка відходів з черги, %")
    ax.set_title("M/M/m+M")
    ax.legend(fontsize=9)
    save(fig, "fig_3_4_verification.png")
