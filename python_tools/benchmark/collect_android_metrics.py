"""
collect_android_metrics.py — Phase 7 Android Performance & Stability Benchmark Harness

Orchestrates on-device performance benchmarks across Runs A through F on the physical OnePlus 13R:
  Run A: Baseline / Startup (30s)
  Run B: Front Camera + MediaPipe Face Tracking (60s)
  Run C: Sustained Face Tracking & Affect Inference (120s)
  Run D: Rear AR Tracking with Marker (60s)
  Run E: Combined Pipeline (Front Face + Rear AR, 60s)
  Run F: Sustained 180s Stability & Thermal Soak (180s)

Gathers:
  - System logs via logcat: [A3R_BENCHMARK], [A3R_DIAG], [A3R_DIAG_AFFECT], [AffectPredictor], [A3R_AR_DIAG]
  - Memory breakdown via 'dumpsys meminfo' (PSS Total, Native Heap, Dalvik Heap, Graphics)
  - CPU utilization via 'top -b -n 1'
  - Rendering performance via 'dumpsys gfxinfo' (jank count, frame time percentiles)
  - Thermal / battery state via 'dumpsys battery' and 'dumpsys thermalservice'
"""

import os
import sys
import time
import json
import re
import subprocess
from pathlib import Path
from typing import Dict, List, Any, Optional
import numpy as np
import pandas as pd

ADB_DEFAULT_PATH = r"C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
DEVICE_SERIAL = "97741778"
PACKAGE_NAME = "com.a3rproject.affectivear"
ACTIVITY_NAME = "com.unity3d.player.UnityPlayerActivity"


class AdbClient:
    def __init__(self, adb_path: str = ADB_DEFAULT_PATH, serial: str = DEVICE_SERIAL):
        self.adb_path = adb_path
        self.serial = serial

    def run_cmd(self, args: List[str], timeout: float = 15.0) -> str:
        cmd = [self.adb_path, "-s", self.serial] + args
        try:
            res = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, timeout=timeout, encoding="utf-8", errors="replace")
            return res.stdout
        except subprocess.TimeoutExpired:
            return ""
        except Exception as e:
            return f"ERROR: {str(e)}"

    def shell(self, cmd_str: str, timeout: float = 15.0) -> str:
        return self.run_cmd(["shell", cmd_str], timeout=timeout)

    def get_pid(self) -> Optional[int]:
        out = self.shell(f"pidof {PACKAGE_NAME}").strip()
        if out and out.isdigit():
            return int(out)
        parts = out.split()
        if parts and parts[0].isdigit():
            return int(parts[0])
        return None

    def get_device_info(self) -> Dict[str, str]:
        model = self.shell("getprop ro.product.model").strip()
        board = self.shell("getprop ro.board.platform").strip()
        android_ver = self.shell("getprop ro.build.version.release").strip()
        sdk = self.shell("getprop ro.build.version.sdk").strip()
        abi = self.shell("getprop ro.product.cpu.abi").strip()
        mem_kb = self.shell("cat /proc/meminfo | grep MemTotal").strip()
        return {
            "model": model,
            "board": board,
            "android_version": android_ver,
            "api_level": sdk,
            "abi": abi,
            "mem_total": mem_kb,
            "serial": self.serial
        }

    def get_battery_and_thermal(self) -> Dict[str, Any]:
        battery_out = self.shell("dumpsys battery")
        thermal_out = self.shell("dumpsys thermalservice")

        level = None
        temp_c = None
        for line in battery_out.splitlines():
            line = line.strip()
            if line.startswith("level:"):
                level = int(line.split(":")[1].strip())
            elif line.startswith("temperature:"):
                raw_temp = int(line.split(":")[1].strip())
                temp_c = raw_temp / 10.0

        thermal_status = "0 (NORMAL)"
        for line in thermal_out.splitlines():
            if "Current Thermal Status:" in line or "thermal status:" in line.lower():
                thermal_status = line.strip()
                break

        return {
            "battery_level": level,
            "battery_temp_c": temp_c,
            "thermal_status": thermal_status
        }

    def get_meminfo(self) -> Dict[str, float]:
        out = self.shell(f"dumpsys meminfo {PACKAGE_NAME}")
        pss_total = 0.0
        native_pss = 0.0
        dalvik_pss = 0.0
        graphics_pss = 0.0

        for line in out.splitlines():
            line_s = line.strip()
            if "TOTAL PSS:" in line_s or line_s.startswith("TOTAL:"):
                parts = line_s.split()
                try:
                    for p in parts:
                        if p.isdigit():
                            pss_total = float(p) / 1024.0
                            break
                except Exception:
                    pass
            elif "Native Heap" in line_s:
                parts = line_s.split()
                try:
                    for p in parts[2:]:
                        if p.isdigit():
                            native_pss = float(p) / 1024.0
                            break
                except Exception:
                    pass
            elif "Dalvik Heap" in line_s:
                parts = line_s.split()
                try:
                    for p in parts[2:]:
                        if p.isdigit():
                            dalvik_pss = float(p) / 1024.0
                            break
                except Exception:
                    pass
            elif "Graphics" in line_s:
                parts = line_s.split()
                try:
                    for p in parts[1:]:
                        if p.isdigit():
                            graphics_pss = float(p) / 1024.0
                            break
                except Exception:
                    pass

        return {
            "pss_total_mb": pss_total,
            "native_heap_mb": native_pss,
            "dalvik_heap_mb": dalvik_pss,
            "graphics_mb": graphics_pss
        }

    def get_cpu_info(self, pid: int) -> Dict[str, float]:
        out = self.shell(f"top -b -n 1 -p {pid}")
        cpu_pct = 0.0
        for line in out.splitlines():
            if str(pid) in line and PACKAGE_NAME in line:
                parts = line.split()
                if len(parts) >= 9:
                    try:
                        cpu_pct = float(parts[8])
                    except Exception:
                        pass
        return {"app_cpu_percent": cpu_pct}

    def get_gfxinfo(self, reset: bool = False) -> Dict[str, Any]:
        if reset:
            self.shell(f"dumpsys gfxinfo {PACKAGE_NAME} reset")
            return {}
        out = self.shell(f"dumpsys gfxinfo {PACKAGE_NAME}")
        total_frames = 0
        janky_frames = 0
        p50 = 0.0
        p90 = 0.0
        p95 = 0.0
        p99 = 0.0

        for line in out.splitlines():
            line_s = line.strip()
            if "Total frames rendered:" in line_s:
                try:
                    total_frames = int(line_s.split(":")[1].strip())
                except Exception:
                    pass
            elif "Janky frames:" in line_s:
                try:
                    m = re.search(r"Janky frames:\s+(\d+)\s+\(([\d\.]+)%\)", line_s)
                    if m:
                        janky_frames = int(m.group(1))
                except Exception:
                    pass
            elif "50th percentile:" in line_s:
                try:
                    p50 = float(line_s.split(":")[1].replace("ms", "").strip())
                except Exception:
                    pass
            elif "90th percentile:" in line_s:
                try:
                    p90 = float(line_s.split(":")[1].replace("ms", "").strip())
                except Exception:
                    pass
            elif "95th percentile:" in line_s:
                try:
                    p95 = float(line_s.split(":")[1].replace("ms", "").strip())
                except Exception:
                    pass
            elif "99th percentile:" in line_s:
                try:
                    p99 = float(line_s.split(":")[1].replace("ms", "").strip())
                except Exception:
                    pass

        jank_rate = (janky_frames / total_frames * 100.0) if total_frames > 0 else 0.0
        return {
            "total_frames": total_frames,
            "janky_frames": janky_frames,
            "jank_rate_pct": jank_rate,
            "frame_time_p50_ms": p50,
            "frame_time_p90_ms": p90,
            "frame_time_p95_ms": p95,
            "frame_time_p99_ms": p99
        }

    def clear_logcat(self):
        self.shell("logcat -c")

    def ensure_awake_and_focused(self):
        self.shell("svc power stayon true")
        self.shell("input keyevent 224")
        self.shell("input keyevent 82")
        self.shell("wm dismiss-keyguard")
        self.shell("input swipe 500 1500 500 500")
        self.shell("input keyevent 4")
        self.shell("settings put global stay_on_while_plugged_in 7")
        self.shell("settings put system screen_off_timeout 1800000")
        self.shell(f"am start -n {PACKAGE_NAME}/{ACTIVITY_NAME}")

    def dump_logcat(self, pid: Optional[int] = None) -> List[str]:
        out = self.shell("logcat -d -v time", timeout=30.0)
        return out.splitlines()


def parse_benchmark_logs(lines: List[str]) -> Dict[str, Any]:
    benchmark_samples = []
    diag_samples = []
    affect_samples = []
    prediction_samples = []
    ar_diag_samples = []

    bm_pat = re.compile(r"\[A3R_BENCHMARK\]\s+t=([\d\.]+)s\s+\|\s+RenderFps=([\d\.]+)\s+\|\s+CamFps=([\d\.]+)\s+\|\s+MpFps=([\d\.]+)\s+\|\s+MpLat=([\d\.]+)ms\s+\|\s+ScorerMeanLat=([\d\.]+)ms\s+\|\s+ScorerP95Lat=([\d\.]+)ms\s+\|\s+PredLat=([\d\.]+)ms\s+\|\s+DropInf=(\d+)\s+\|\s+DropScore=(\d+)\s+\|\s+Face=(\w+)\s+\|\s+HeapMB=([\d\.]+)")
    diag_pat = re.compile(r"\[A3R_DIAG\]\s+CamFps=([\d\.]+)\s+\|\s+MpFps=([\d\.]+)\s+\|\s+LastLat=([\d\.]+)ms\s+\|\s+Dropped=(\d+)\s+\|\s+Mode=(\w+)")
    affect_pat = re.compile(r"\[A3R_DIAG_AFFECT\]\s+C=([\d\.]+)\s+\|\s+F=([\d\.]+)\s+\|\s+E=([\d\.]+)")
    pred_pat = re.compile(r"\[AffectPredictor\]\s+PREDICTION fired:\s+(\w+)\s+at\s+t=([\d\.]+)s\s+\(evidence=([\d\.]+),\s+slope=([-\d\.]+)/s\)")
    ar_pat = re.compile(r"\[A3R_AR_DIAG\]\s+Target:\s*(\w+)\s*\|\s*Event:\s*(\w+)\s*\|\s*State:\s*(\w+)")

    for line in lines:
        bm_m = bm_pat.search(line)
        if bm_m:
            benchmark_samples.append({
                "t": float(bm_m.group(1)),
                "render_fps": float(bm_m.group(2)),
                "cam_fps": float(bm_m.group(3)),
                "mp_fps": float(bm_m.group(4)),
                "mp_lat_ms": float(bm_m.group(5)),
                "scorer_mean_lat_ms": float(bm_m.group(6)),
                "scorer_p95_lat_ms": float(bm_m.group(7)),
                "pred_lat_ms": float(bm_m.group(8)),
                "drop_inf": int(bm_m.group(9)),
                "drop_score": int(bm_m.group(10)),
                "face_detected": bm_m.group(11).lower() == "true",
                "heap_mb": float(bm_m.group(12))
            })

        diag_m = diag_pat.search(line)
        if diag_m:
            diag_samples.append({
                "cam_fps": float(diag_m.group(1)),
                "mp_fps": float(diag_m.group(2)),
                "mp_lat_ms": float(diag_m.group(3)),
                "dropped": int(diag_m.group(4)),
                "mode": diag_m.group(5)
            })

        aff_m = affect_pat.search(line)
        if aff_m:
            affect_samples.append({
                "c": float(aff_m.group(1)),
                "f": float(aff_m.group(2)),
                "e": float(aff_m.group(3))
            })

        pred_m = pred_pat.search(line)
        if pred_m:
            prediction_samples.append({
                "behavior": pred_m.group(1),
                "timestamp": float(pred_m.group(2)),
                "evidence": float(pred_m.group(3)),
                "slope": float(pred_m.group(4))
            })

        ar_m = ar_pat.search(line)
        if ar_m:
            ar_diag_samples.append({
                "target": ar_m.group(1),
                "event": ar_m.group(2),
                "state": ar_m.group(3)
            })

    return {
        "benchmark_samples": benchmark_samples,
        "diag_samples": diag_samples,
        "affect_samples": affect_samples,
        "prediction_samples": prediction_samples,
        "ar_diag_samples": ar_diag_samples
    }


def compute_summary_stats(values: List[float]) -> Dict[str, float]:
    if not values:
        return {"mean": 0.0, "median": 0.0, "p95": 0.0, "min": 0.0, "max": 0.0, "std": 0.0}
    arr = np.array(values)
    return {
        "mean": float(np.mean(arr)),
        "median": float(np.median(arr)),
        "p95": float(np.percentile(arr, 95)),
        "min": float(np.min(arr)),
        "max": float(np.max(arr)),
        "std": float(np.std(arr))
    }


def run_benchmark_trial(
    adb: AdbClient,
    run_id: str,
    run_name: str,
    duration_sec: int,
    output_dir: Path
) -> Dict[str, Any]:
    print(f"\n=======================================================")
    print(f"STARTING BENCHMARK: Run {run_id} — {run_name} ({duration_sec}s)")
    print(f"=======================================================")

    # Setup run environment
    cold_start_ms = 0.0
    if run_id == "A":
        print(f"Stopping {PACKAGE_NAME} for cold-start measurement...")
        adb.shell(f"am force-stop {PACKAGE_NAME}")
        time.sleep(1.5)
        adb.clear_logcat()
        print("Launching app with am start -W...")
        launch_out = adb.shell(f"am start -W -n {PACKAGE_NAME}/{ACTIVITY_NAME}")
        for l in launch_out.splitlines():
            if "TotalTime:" in l:
                try:
                    cold_start_ms = float(l.split(":")[1].strip())
                except Exception:
                    pass
        time.sleep(2.0)
        pid = adb.get_pid()
    else:
        adb.ensure_awake_and_focused()
        time.sleep(1.0)
        adb.clear_logcat()
        pid = adb.get_pid()
        if not pid:
            print(f"App not running. Launching {PACKAGE_NAME}...")
            adb.shell(f"am start -n {PACKAGE_NAME}/{ACTIVITY_NAME}")
            time.sleep(4.0)
            pid = adb.get_pid()

    print(f"Target PID: {pid} (Cold start time: {cold_start_ms}ms)")

    # Initial device status
    init_batt = adb.get_battery_and_thermal()
    init_mem = adb.get_meminfo()
    adb.get_gfxinfo(reset=True)

    # Polling loops
    snapshots = []
    start_time = time.time()
    next_poll = start_time
    last_keepalive = start_time

    while time.time() - start_time < duration_sec:
        cur_t = time.time()
        elapsed = cur_t - start_time
        if cur_t - last_keepalive >= 8.0:
            adb.shell("input keyevent 224")
            adb.shell("input tap 50 50")
            last_keepalive = cur_t
        if cur_t >= next_poll:
            cur_pid = adb.get_pid()
            if cur_pid:
                pid = cur_pid
            mem = adb.get_meminfo()
            cpu = adb.get_cpu_info(pid) if pid else {"app_cpu_percent": 0.0}
            batt = adb.get_battery_and_thermal()
            snapshots.append({
                "elapsed_sec": round(elapsed, 1),
                "timestamp": round(cur_t, 2),
                "mem": mem,
                "cpu": cpu,
                "batt": batt
            })
            print(f"[{elapsed:5.1f}s / {duration_sec}s] PSS={mem['pss_total_mb']:.1f}MB | Native={mem['native_heap_mb']:.1f}MB | CPU={cpu['app_cpu_percent']:.1f}% | BattTemp={batt['battery_temp_c']}°C")
            next_poll = cur_t + 2.0  # poll every 2 seconds

        time.sleep(0.5)

    final_gfx = adb.get_gfxinfo(reset=False)
    final_batt = adb.get_battery_and_thermal()
    log_lines = adb.dump_logcat(pid)
    parsed_logs = parse_benchmark_logs(log_lines)

    # Process metrics
    bm_samples = parsed_logs["benchmark_samples"]
    render_fps_stats = compute_summary_stats([s["render_fps"] for s in bm_samples])
    cam_fps_stats = compute_summary_stats([s["cam_fps"] for s in bm_samples])
    mp_fps_stats = compute_summary_stats([s["mp_fps"] for s in bm_samples])
    mp_lat_stats = compute_summary_stats([s["mp_lat_ms"] for s in bm_samples if s["mp_lat_ms"] > 0])
    scorer_lat_stats = compute_summary_stats([s["scorer_mean_lat_ms"] for s in bm_samples if s["scorer_mean_lat_ms"] > 0])
    pred_lat_stats = compute_summary_stats([s["pred_lat_ms"] for s in bm_samples if s["pred_lat_ms"] > 0])
    heap_stats = compute_summary_stats([s["heap_mb"] for s in bm_samples])

    pss_stats = compute_summary_stats([s["mem"]["pss_total_mb"] for s in snapshots])
    native_stats = compute_summary_stats([s["mem"]["native_heap_mb"] for s in snapshots])
    cpu_stats = compute_summary_stats([s["cpu"]["app_cpu_percent"] for s in snapshots if s["cpu"]["app_cpu_percent"] > 0])

    face_detected_ratio = (
        sum(1 for s in bm_samples if s["face_detected"]) / len(bm_samples)
        if bm_samples else 0.0
    )

    result = {
        "run_id": run_id,
        "run_name": run_name,
        "duration_sec": duration_sec,
        "pid": pid,
        "cold_start_ms": cold_start_ms,
        "initial_battery": init_batt,
        "final_battery": final_batt,
        "battery_temp_delta_c": round((final_batt["battery_temp_c"] or 0) - (init_batt["battery_temp_c"] or 0), 2),
        "gfxinfo": final_gfx,
        "stats": {
            "render_fps": render_fps_stats,
            "camera_fps": cam_fps_stats,
            "mediapipe_fps": mp_fps_stats,
            "mediapipe_lat_ms": mp_lat_stats,
            "scorer_lat_ms": scorer_lat_stats,
            "prediction_lat_ms": pred_lat_stats,
            "heap_mb": heap_stats,
            "pss_mb": pss_stats,
            "native_heap_mb": native_stats,
            "app_cpu_percent": cpu_stats,
            "face_detected_ratio": round(face_detected_ratio, 4),
            "predictions_count": len(parsed_logs["prediction_samples"]),
            "ar_events_count": len(parsed_logs["ar_diag_samples"])
        },
        "snapshots": snapshots,
        "parsed_logs": parsed_logs
    }

    # Save to disk
    run_file = output_dir / f"run_{run_id.lower()}.json"
    with open(run_file, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=2)
    print(f"Saved run data to: {run_file}")

    return result


def main():
    import argparse
    parser = argparse.ArgumentParser(description="Phase 7 Android Performance Benchmark Harness")
    parser.add_argument("--run", type=str, default="all", choices=["all", "A", "B", "C", "D", "E", "F"], help="Run ID to execute")
    parser.add_argument("--outdir", type=str, default="evaluation/phase7_mobile_performance", help="Output directory")
    args = parser.parse_args()

    adb = AdbClient()
    dev_info = adb.get_device_info()
    print("Detected Target Device:")
    for k, v in dev_info.items():
        print(f"  {k}: {v}")

    out_path = Path(args.outdir)
    out_path.mkdir(parents=True, exist_ok=True)

    # Save device info
    with open(out_path / "device_info.json", "w", encoding="utf-8") as f:
        json.dump(dev_info, f, indent=2)

    protocols = [
        ("A", "Baseline / Startup", 30),
        ("B", "Front Camera + MediaPipe Face Tracking", 60),
        ("C", "Sustained Face Tracking & Affect Inference", 120),
        ("D", "Rear AR Tracking with Marker", 60),
        ("E", "Combined Pipeline (Front Face + Rear AR)", 60),
        ("F", "Sustained 180s Stability & Thermal Soak", 180),
    ]

    selected = protocols if args.run == "all" else [p for p in protocols if p[0] == args.run]

    all_results = {}
    for r_id, r_name, r_dur in selected:
        res = run_benchmark_trial(adb, r_id, r_name, r_dur, out_path)
        all_results[r_id] = res

    # Save overall summary
    summary_file = out_path / "runs_summary.json"
    with open(summary_file, "w", encoding="utf-8") as f:
        json.dump(all_results, f, indent=2)
    print(f"\nAll benchmark runs complete. Summary written to {summary_file}")


if __name__ == "__main__":
    main()

