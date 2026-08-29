"""
visualize_calibration.py — Calibration result visualization

Part of the A³R offline calibration tooling (python_tools/daisee_calibration/).

Generates:
  1. A heatmap of F-beta scores over the (T, D) grid
  2. Precision-recall curve for the chosen operating point
  3. Affect score histograms per DAiSEE confusion label (0-3)
  4. Score trajectory plot for a single example clip

Saves all plots to docs/figures/ for inclusion in the paper/report.

Usage:
    python visualize_calibration.py \\
        --results_csv calibration_results_full.csv \\
        --clip_scores_dir clip_scores/ \\
        --example_clip_id <some_clip_id>
"""

import argparse
import json
import os
from pathlib import Path

import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.ticker as mticker

# ── Output directory ──────────────────────────────────────────────────────────
FIGURES_DIR = Path(__file__).parent.parent.parent / "docs" / "figures"


def setup_style():
    """Apply a clean, publication-ready matplotlib style."""
    plt.rcParams.update({
        "font.family":        "DejaVu Sans",
        "font.size":          11,
        "axes.titlesize":     13,
        "axes.labelsize":     11,
        "xtick.labelsize":    9,
        "ytick.labelsize":    9,
        "legend.fontsize":    9,
        "figure.dpi":         150,
        "savefig.dpi":        300,
        "savefig.bbox":       "tight",
        "axes.spines.top":    False,
        "axes.spines.right":  False,
    })


def plot_fbeta_heatmap(results_df: pd.DataFrame, output_path: Path):
    """
    Plot F-beta score as a heatmap over the (T, D) grid.
    """
    thresholds = sorted(results_df["threshold"].unique())
    dwell_times = sorted(results_df["dwell_s"].unique())

    grid = np.zeros((len(dwell_times), len(thresholds)))
    for _, row in results_df.iterrows():
        ti = thresholds.index(row["threshold"])
        di = dwell_times.index(row["dwell_s"])
        grid[di, ti] = row["f_beta"]

    fig, ax = plt.subplots(figsize=(8, 5))
    im = ax.imshow(grid, aspect="auto", cmap="RdYlGn", vmin=0, vmax=1,
                   origin="lower")
    plt.colorbar(im, ax=ax, label="F-beta (β=0.5)")

    ax.set_xticks(range(len(thresholds)))
    ax.set_xticklabels([f"{t:.2f}" for t in thresholds])
    ax.set_yticks(range(len(dwell_times)))
    ax.set_yticklabels([f"{d:.1f}s" for d in dwell_times])
    ax.set_xlabel("Confusion Threshold T")
    ax.set_ylabel("Dwell Time D")
    ax.set_title("F-beta Score Grid (threshold × dwell-time)")

    # Mark the best cell
    best_idx = results_df["f_beta"].idxmax()
    best = results_df.loc[best_idx]
    ti = thresholds.index(best["threshold"])
    di = dwell_times.index(best["dwell_s"])
    ax.add_patch(plt.Rectangle((ti - 0.5, di - 0.5), 1, 1,
                                fill=False, edgecolor="blue", linewidth=2))
    ax.text(ti, di, f"★\n{best['f_beta']:.3f}", ha="center", va="center",
            fontsize=8, color="blue", fontweight="bold")

    plt.tight_layout()
    plt.savefig(output_path)
    plt.close()
    print(f"  Saved: {output_path}")


def plot_precision_recall(results_df: pd.DataFrame, output_path: Path):
    """
    Plot precision vs recall for all (T, D) combinations.
    """
    fig, ax = plt.subplots(figsize=(6, 5))

    # Color-code by threshold
    thresholds = sorted(results_df["threshold"].unique())
    cmap = plt.cm.plasma
    colors = {t: cmap(i / max(len(thresholds) - 1, 1))
              for i, t in enumerate(thresholds)}

    for T in thresholds:
        subset = results_df[results_df["threshold"] == T].sort_values("recall")
        ax.plot(subset["recall"], subset["precision"],
                "o-", color=colors[T], label=f"T={T:.2f}", markersize=4, linewidth=1.5)

    # Mark best operating point
    best = results_df.loc[results_df["f_beta"].idxmax()]
    ax.plot(best["recall"], best["precision"],
            "*", color="blue", markersize=14, zorder=5, label="Best (F-β)")

    ax.set_xlim(0, 1.05)
    ax.set_ylim(0, 1.05)
    ax.set_xlabel("Recall")
    ax.set_ylabel("Precision")
    ax.set_title("Precision-Recall by Threshold (all dwell times)")
    ax.legend(bbox_to_anchor=(1.02, 1), loc="upper left", fontsize=8)
    ax.grid(True, alpha=0.3)

    plt.tight_layout()
    plt.savefig(output_path)
    plt.close()
    print(f"  Saved: {output_path}")


def plot_score_histograms(clip_scores_dir: Path, output_path: Path):
    """
    Plot histogram of mean confusion scores per DAiSEE ground-truth label (0-3).
    """
    label_means = {0: [], 1: [], 2: [], 3: []}

    csv_files = list(clip_scores_dir.glob("*.csv"))
    if not csv_files:
        print(f"  [SKIP] No CSV files in {clip_scores_dir}")
        return

    for csv_file in csv_files:
        try:
            df = pd.read_csv(csv_file)
            if "confusion_label" not in df.columns or "confusion_score" not in df.columns:
                continue
            label = int(df["confusion_label"].iloc[0])
            mean_score = df["confusion_score"].dropna().mean()
            if label in label_means:
                label_means[label].append(mean_score)
        except Exception:
            pass

    fig, axes = plt.subplots(1, 4, figsize=(14, 4), sharey=False)
    colors = ["#2196F3", "#FF9800", "#F44336", "#9C27B0"]
    label_names = ["0 — Not confused", "1 — Slightly", "2 — Moderately", "3 — Very confused"]

    for label, (ax, color, name) in enumerate(zip(axes, colors, label_names)):
        scores = label_means[label]
        if scores:
            ax.hist(scores, bins=20, color=color, alpha=0.75, edgecolor="white")
            ax.axvline(np.mean(scores), color="black", linestyle="--",
                       linewidth=1.5, label=f"μ={np.mean(scores):.3f}")
            ax.legend(fontsize=8)
        ax.set_title(name, fontsize=10)
        ax.set_xlabel("Mean Confusion Score")
        ax.set_ylabel("# Clips" if label == 0 else "")

    fig.suptitle("Distribution of Mean Confusion Scores by DAiSEE Label", y=1.02)
    plt.tight_layout()
    plt.savefig(output_path)
    plt.close()
    print(f"  Saved: {output_path}")


def plot_example_trajectory(clip_scores_dir: Path, clip_id: str,
                             threshold: float, dwell_s: float,
                             output_path: Path):
    """
    Plot affect score trajectories for a single example clip, with FSM firing marked.
    """
    csv_file = clip_scores_dir / f"{clip_id}.csv"
    if not csv_file.exists():
        # Use first available clip
        csv_files = list(clip_scores_dir.glob("*.csv"))
        if not csv_files:
            print(f"  [SKIP] No clips found for trajectory plot")
            return
        csv_file = csv_files[0]
        clip_id = csv_file.stem

    df = pd.read_csv(csv_file)

    fig, axes = plt.subplots(3, 1, figsize=(12, 7), sharex=True)
    t = df["timestamp_s"].values

    for ax, col, color, label in zip(
        axes,
        ["confusion_score", "frustration_score", "engagement_score"],
        ["#F44336", "#FF9800", "#4CAF50"],
        ["Confusion", "Frustration", "Engagement"]
    ):
        valid = df[col].notna()
        ax.plot(t[valid], df[col].values[valid], color=color, linewidth=1.5, label=label)
        ax.axhline(threshold, color="gray", linestyle="--", linewidth=1, alpha=0.7,
                   label=f"T={threshold}")
        ax.set_ylabel(label)
        ax.set_ylim(0, 1.1)
        ax.legend(loc="upper right", fontsize=8)

    axes[-1].set_xlabel("Time (s)")
    gt_label = int(df.get("confusion_label", pd.Series([0])).iloc[0])
    fig.suptitle(f"Affect Trajectory — Clip: {clip_id} (GT confusion label: {gt_label})")
    plt.tight_layout()
    plt.savefig(output_path)
    plt.close()
    print(f"  Saved: {output_path}")


def main():
    parser = argparse.ArgumentParser(description="A3R calibration visualization")
    parser.add_argument("--results_csv", default=None,
                        help="Path to calibration results CSV (output of calibrate_thresholds.py with --save_csv flag)")
    parser.add_argument("--clip_scores_dir", default=None,
                        help="Directory containing per-clip scored CSVs")
    parser.add_argument("--example_clip_id", default=None,
                        help="Clip ID to use for trajectory plot")
    parser.add_argument("--calibration_result", default="calibration_result.json",
                        help="Path to calibration_result.json")
    args = parser.parse_args()

    setup_style()
    FIGURES_DIR.mkdir(parents=True, exist_ok=True)
    print(f"Saving figures to: {FIGURES_DIR}")

    # Load calibration result for threshold values
    T = 0.60   # default
    D = 3.0
    if Path(args.calibration_result).exists():
        with open(args.calibration_result) as f:
            cal = json.load(f)
        T = cal.get("T_confusion", T)
        D = cal.get("D_seconds", D)
        print(f"Loaded calibration: T={T}, D={D}")

    # 1. F-beta heatmap (requires results CSV)
    if args.results_csv and Path(args.results_csv).exists():
        results_df = pd.read_csv(args.results_csv)
        plot_fbeta_heatmap(results_df, FIGURES_DIR / "fbeta_heatmap.png")
        plot_precision_recall(results_df, FIGURES_DIR / "precision_recall.png")
    else:
        print("[INFO] No results CSV provided — generating synthetic demo plots")
        # Generate synthetic results for demonstration
        rows = []
        for T_val in [0.40, 0.45, 0.50, 0.55, 0.60, 0.65, 0.70, 0.75]:
            for D_val in [1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 5.0]:
                prec = 0.6 + 0.3 * (T_val - 0.40) + 0.05 * np.random.randn()
                rec  = 0.9 - 0.4 * (T_val - 0.40) - 0.1 * (D_val - 1.5) / 3.5 + 0.05 * np.random.randn()
                prec, rec = np.clip(prec, 0.01, 1), np.clip(rec, 0.01, 1)
                fb = (1 + 0.25) * prec * rec / (0.25 * prec + rec + 1e-9)
                rows.append({"threshold": T_val, "dwell_s": D_val,
                             "precision": round(prec, 4), "recall": round(rec, 4),
                             "f_beta": round(fb, 4)})
        demo_df = pd.DataFrame(rows)
        plot_fbeta_heatmap(demo_df, FIGURES_DIR / "fbeta_heatmap_demo.png")
        plot_precision_recall(demo_df, FIGURES_DIR / "precision_recall_demo.png")

    # 2. Score histograms
    clip_dir = Path(args.clip_scores_dir) if args.clip_scores_dir else Path(__file__).parent / "clip_scores"
    if clip_dir.exists():
        plot_score_histograms(clip_dir, FIGURES_DIR / "score_histograms.png")
        plot_example_trajectory(clip_dir, args.example_clip_id or "",
                                T, D, FIGURES_DIR / "example_trajectory.png")
    else:
        print(f"[INFO] clip_scores/ not found at {clip_dir} — skipping histogram/trajectory plots")

    print("\nDone. All figures saved to docs/figures/")


if __name__ == "__main__":
    main()
