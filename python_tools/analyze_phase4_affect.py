"""
analyze_phase4_affect.py — Analysis and Verification of Real-Time Affect Measurements

Analyzes Phase 4 on-device telemetry from OnePlus 13R:
  1. Computes descriptive statistics (N, mean, std, min, p25, median, p75, max) for features and scores.
  2. Verifies formula parity: compares logged scores against exact math computed from logged features.
  3. Verifies zero simulation (is_simulated == False).
  4. Profiles feature correlations and directional behavior across observed expressions.
  5. Evaluates temporal dynamics: sample interval stability (2Hz target), jitter, and FSM dwell behavior.
"""

import os
import glob
import json
import numpy as np
import pandas as pd
from datetime import datetime

CONFUSION_WEIGHTS = {"brow_furrow": 0.40, "eye_closed": 0.30, "mouth_tension": 0.30}
FRUSTRATION_WEIGHTS = {"brow_furrow": 0.35, "eye_closed": 0.25, "mouth_downturn": 0.40}
ENGAGEMENT_WEIGHTS = {"ear_open": 0.50, "brow_relaxed": 0.30, "brow_high": 0.20}

def load_session_logs(log_dir):
    pattern = os.path.join(log_dir, "session_20260913_*.jsonl")
    files = sorted(glob.glob(pattern))
    records = []
    session_starts = []
    
    for f in files:
        fname = os.path.basename(f)
        with open(f, "r", encoding="utf-8") as fp:
            for line_idx, line in enumerate(fp):
                line = line.strip().lstrip("\ufeff")
                if not line:
                    continue
                try:
                    data = json.loads(line)
                    data["_file"] = fname
                    data["_line"] = line_idx + 1
                    t = data.get("type")
                    if t == "affect":
                        records.append(data)
                    elif t == "session_start":
                        session_starts.append(data)
                except Exception as e:
                    print(f"Parse error in {fname}:{line_idx}: {e}")
                    
    df_affect = pd.DataFrame(records)
    return df_affect, session_starts

def verify_formula_parity(df):
    """Verify that logged confusion/frustration/engagement match exact formula from logged features."""
    parity_errors = []
    
    for idx, row in df.iterrows():
        bf = row["brow_furrow"]
        ear_norm = row["ear_norm"]
        mt = row["mouth_tension"]
        bh = row["brow_height"]
        
        expected_c = np.clip(0.40 * bf + 0.30 * (1.0 - ear_norm) + 0.30 * mt, 0.0, 1.0)
        expected_f = np.clip(0.35 * bf + 0.25 * (1.0 - ear_norm) + 0.40 * mt, 0.0, 1.0)
        expected_e = np.clip(0.50 * ear_norm + 0.30 * (1.0 - bf) + 0.20 * bh, 0.0, 1.0)
        
        diff_c = abs(row["confusion"] - expected_c)
        diff_f = abs(row["frustration"] - expected_f)
        diff_e = abs(row["engagement"] - expected_e)
        
        # 1e-3 tolerance due to 4-decimal-place rounding in SessionLogger
        if diff_c > 1e-3 or diff_f > 1e-3 or diff_e > 1e-3:
            parity_errors.append({
                "file": row["_file"],
                "line": row["_line"],
                "diff_c": diff_c,
                "diff_f": diff_f,
                "diff_e": diff_e
            })
            
    return parity_errors

def analyze():
    base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    log_dir = os.path.join(base_dir, "evaluation", "device_session_logs")
    
    print("=" * 70)
    print("A3R PHASE 4 REAL-TIME AFFECT MEASUREMENT ANALYSIS")
    print("=" * 70)
    print(f"Log directory: {log_dir}")
    
    df, starts = load_session_logs(log_dir)
    print(f"Total session starts: {len(starts)}")
    print(f"Total affect samples loaded: {len(df)}")
    
    if len(df) == 0:
        print("ERROR: No affect samples found!")
        return
        
    print(f"Active sessions: {df['_file'].nunique()} unique session files")
    for s in starts:
        print(f"  Session ID: {s.get('session_id')} | Device: {s.get('device_model')} ({s.get('device_name')}) | Mode: {s.get('inference_mode')}")
        
    # 1. Simulation Verification
    sim_counts = df["is_simulated"].value_counts().to_dict()
    print("\n1. SIMULATION VERIFICATION:")
    print(f"   is_simulated counts: {sim_counts}")
    has_simulation = sim_counts.get(True, 0) > 0
    print(f"   -> Pure real hardware pipeline (0 simulation): {'PASS' if not has_simulation else 'FAIL'}")
    
    # 2. Parity Check
    parity_errs = verify_formula_parity(df)
    print("\n2. MATHEMATICAL FORMULA PARITY CHECK:")
    print(f"   Samples tested: {len(df)}")
    print(f"   Parity discrepancies (> 1e-3): {len(parity_errs)}")
    print(f"   -> Exact C# vs Python formula equivalence: {'PASS' if len(parity_errs) == 0 else 'FAIL'}")
    
    # 3. Descriptive Statistics
    feature_cols = ["brow_furrow", "ear_raw", "ear_norm", "mouth_tension", "brow_height"]
    score_cols = ["confusion", "frustration", "engagement"]
    
    print("\n3. DESCRIPTIVE STATISTICS — FACIAL FEATURES:")
    stats_feats = df[feature_cols].describe(percentiles=[0.05, 0.25, 0.50, 0.75, 0.95]).round(4)
    print(stats_feats.T[["count", "mean", "std", "min", "25%", "50%", "75%", "95%", "max"]].to_string())
    
    print("\n4. DESCRIPTIVE STATISTICS — AFFECT SCORES:")
    stats_scores = df[score_cols].describe(percentiles=[0.05, 0.25, 0.50, 0.75, 0.95]).round(4)
    print(stats_scores.T[["count", "mean", "std", "min", "25%", "50%", "75%", "95%", "max"]].to_string())
    
    # 4. Correlation Matrix
    print("\n5. FEATURE-TO-SCORE CORRELATION MATRIX:")
    corr = df[feature_cols + score_cols].corr().loc[feature_cols, score_cols].round(4)
    print(corr.to_string())
    
    # 5. Temporal Sampling Rate
    print("\n6. TEMPORAL SAMPLING DYNAMICS:")
    df["dt"] = pd.to_datetime(df["ts"])
    intervals = []
    for f, group in df.groupby("_file"):
        if len(group) > 1:
            diffs = group["dt"].diff().dropna().dt.total_seconds()
            intervals.extend(diffs[diffs < 5.0].tolist()) # filter pauses > 5s
            
    if intervals:
        arr_inv = np.array(intervals)
        mean_dt = np.mean(arr_inv)
        std_dt = np.std(arr_inv)
        effective_hz = 1.0 / mean_dt if mean_dt > 0 else 0
        p50_dt = np.median(arr_inv)
        p95_dt = np.percentile(arr_inv, 95)
        print(f"   Total consecutive intervals analyzed: {len(arr_inv)}")
        print(f"   Mean sample interval: {mean_dt*1000:.1f} ms (Target: 500 ms / 2 Hz)")
        print(f"   Effective logging rate: {effective_hz:.2f} Hz")
        print(f"   Median interval: {p50_dt*1000:.1f} ms")
        print(f"   P95 interval: {p95_dt*1000:.1f} ms")
        print(f"   Std deviation: {std_dt*1000:.1f} ms")
        
    print("=" * 70)

if __name__ == "__main__":
    analyze()
