import os
import json
import datetime
import numpy as np
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from python_tools.prediction.predict_affect import TemporalAffectPredictor, TimedAffectSample

def run_evaluation():
    eval_dir = REPO_ROOT / "evaluation" / "phase6_prediction"
    eval_dir.mkdir(parents=True, exist_ok=True)
    jsonl_log_path = str(eval_dir / "phase6_prediction_log.jsonl")
    json_summary_path = str(eval_dir / "prediction_metrics_summary.json")

    predictor = TemporalAffectPredictor(window_capacity=5, min_samples=3)
    jsonl_records = []
    
    stats = {
        "confusion_ramp": {"total": 40, "tp": 0, "fp": 0, "fn": 0, "lead_times": []},
        "frustration_ramp": {"total": 40, "tp": 0, "fp": 0, "fn": 0, "lead_times": []},
        "polarity_hesitation": {"total": 20, "tp": 0, "fp": 0, "fn": 0, "lead_times": []},
        "neutral_control": {"total": 30, "tn": 0, "fp": 0},
        "spike_noise": {"total": 20, "tn": 0, "fp": 0},
        "tracking_loss": {"total": 20, "triggered": 0, "recovered": 0},
        "recovery_sequences": {"total": 10, "recovered": 0, "fp": 0}
    }

    sim_session_time = 0.0

    def log_record(evt, session_time):
        rec = {
            "record_type": "PREDICTION_EVENT",
            "timestamp_iso": datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "session_time_seconds": round(session_time, 3),
            "predicted_behavior": evt.behavior,
            "evidence_score": round(evt.evidence_score, 4),
            "trend_slope": round(evt.trend_slope, 4),
            "lead_time_seconds": round(evt.lead_time_s, 3) if evt.lead_time_s is not None else None,
            "is_simulated": True,
            "is_technical_disruption": evt.is_technical_disruption,
            "prediction_state": "PREDICTED" if not evt.is_technical_disruption else "TRACKING_LOST",
            "step_id": evt.step_id,
            "confusion_score": round(evt.confusion, 4),
            "frustration_score": round(evt.frustration, 4)
        }
        jsonl_records.append(rec)
        return rec

    # 1. Confusion Ramp Sequences (40 runs)
    for i in range(40):
        predictor.reset()
        slope = 0.06 + (i % 5) * 0.02
        start_c = 0.20 + (i % 3) * 0.05
        step_id = "step_02_resistor"
        pred_record_idx = None
        event_handled = False
        
        for step in range(25):
            t = step * 0.5
            sim_session_time += 0.5
            val = min(1.0, start_c + slope * t)
            
            s = TimedAffectSample(
                timestamp=t,
                confusion=val,
                frustration=0.10,
                engagement=0.80 - 0.3 * val,
                brow_furrow=val,
                ear=0.30 - 0.10 * val,
                mouth_tension=0.15,
                brow_height=0.40,
                step_id=step_id,
                is_simulated=True
            )
            evt = predictor.process_sample(s)
            if evt and pred_record_idx is None:
                rec = log_record(evt, sim_session_time)
                pred_record_idx = len(jsonl_records) - 1

            # When ground truth detection threshold (0.60) is reached:
            if val >= 0.60 and not event_handled:
                event_handled = True
                lt = predictor.notify_actual_event_fired("ConfusionStruggle", t)
                if lt is not None and lt > 0:
                    stats["confusion_ramp"]["tp"] += 1
                    stats["confusion_ramp"]["lead_times"].append(lt)
                    if pred_record_idx is not None:
                        jsonl_records[pred_record_idx]["lead_time_seconds"] = round(lt, 3)
                else:
                    stats["confusion_ramp"]["fn"] += 1
                break

        if not event_handled:
            if pred_record_idx is not None:
                stats["confusion_ramp"]["fp"] += 1
            else:
                stats["confusion_ramp"]["fn"] += 1

    # 2. Frustration Ramp Sequences (40 runs)
    for i in range(40):
        predictor.reset()
        slope = 0.06 + (i % 4) * 0.02
        start_f = 0.20 + (i % 3) * 0.05
        step_id = "step_04_wire"
        pred_record_idx = None
        event_handled = False
        
        for step in range(25):
            t = step * 0.5
            sim_session_time += 0.5
            val = min(1.0, start_f + slope * t)
            
            s = TimedAffectSample(
                timestamp=t,
                confusion=0.15,
                frustration=val,
                engagement=0.60,
                brow_furrow=0.20,
                ear=0.28,
                mouth_tension=val,
                brow_height=0.40,
                step_id=step_id,
                is_simulated=True
            )
            evt = predictor.process_sample(s)
            if evt and pred_record_idx is None:
                rec = log_record(evt, sim_session_time)
                pred_record_idx = len(jsonl_records) - 1

            # When ground truth detection threshold (0.65) is reached:
            if val >= 0.65 and not event_handled:
                event_handled = True
                lt = predictor.notify_actual_event_fired("FrustrationDisengage", t)
                if lt is not None and lt > 0:
                    stats["frustration_ramp"]["tp"] += 1
                    stats["frustration_ramp"]["lead_times"].append(lt)
                    if pred_record_idx is not None:
                        jsonl_records[pred_record_idx]["lead_time_seconds"] = round(lt, 3)
                else:
                    stats["frustration_ramp"]["fn"] += 1
                break

        if not event_handled:
            if pred_record_idx is not None:
                stats["frustration_ramp"]["fp"] += 1
            else:
                stats["frustration_ramp"]["fn"] += 1

    # 3. Polarity Hesitation Sequences (20 runs)
    for i in range(20):
        predictor.reset()
        step_id = "step_03_led"
        pred_record_idx = None
        event_handled = False
        
        for step in range(16):
            t = step * 0.5
            sim_session_time += 0.5
            # Step 3 LED micro-squint precursor: brow velocity >= 0.08, ear velocity <= -0.04
            brow = 0.20 + 0.09 * (step * 0.5)
            ear = 0.32 - 0.05 * (step * 0.5)
            c_val = 0.36 + 0.02 * (step * 0.5)
            
            s = TimedAffectSample(
                timestamp=t,
                confusion=c_val,
                frustration=0.15,
                engagement=0.55,
                brow_furrow=brow,
                ear=ear,
                mouth_tension=0.20,
                brow_height=0.40,
                step_id=step_id,
                is_simulated=True
            )
            evt = predictor.process_sample(s)
            if evt and pred_record_idx is None:
                rec = log_record(evt, sim_session_time)
                pred_record_idx = len(jsonl_records) - 1

            # Actual polarity hesitation behavior occurs at t = 3.5s
            if t >= 3.5 and not event_handled:
                event_handled = True
                lt = predictor.notify_actual_event_fired("PolarityHesitation", t)
                if lt is not None and lt > 0:
                    stats["polarity_hesitation"]["tp"] += 1
                    stats["polarity_hesitation"]["lead_times"].append(lt)
                    if pred_record_idx is not None:
                        jsonl_records[pred_record_idx]["lead_time_seconds"] = round(lt, 3)
                else:
                    stats["polarity_hesitation"]["fn"] += 1
                break

        if not event_handled:
            if pred_record_idx is not None:
                stats["polarity_hesitation"]["fp"] += 1
            else:
                stats["polarity_hesitation"]["fn"] += 1

    # 4. Neutral Control Sequences (30 runs)
    for i in range(30):
        predictor.reset()
        step_id = "step_01_breadboard"
        fp_detected = False
        
        for step in range(20):
            t = step * 0.5
            sim_session_time += 0.5
            s = TimedAffectSample(
                timestamp=t,
                confusion=0.10 + 0.04 * np.random.uniform(-1, 1),
                frustration=0.08 + 0.03 * np.random.uniform(-1, 1),
                engagement=0.85 + 0.05 * np.random.uniform(-1, 1),
                brow_furrow=0.08,
                ear=0.32,
                mouth_tension=0.05,
                brow_height=0.50,
                step_id=step_id,
                is_simulated=True
            )
            evt = predictor.process_sample(s)
            if evt:
                fp_detected = True
                log_record(evt, sim_session_time)

        if not fp_detected:
            stats["neutral_control"]["tn"] += 1
        else:
            stats["neutral_control"]["fp"] += 1

    # 5. Transient Spike Noise Sequences (20 runs)
    for i in range(20):
        predictor.reset()
        step_id = "step_02_resistor"
        fp_detected = False
        
        for step in range(20):
            t = step * 0.5
            sim_session_time += 0.5
            is_spike = (step == 8)
            s = TimedAffectSample(
                timestamp=t,
                confusion=0.52 if is_spike else 0.15,
                frustration=0.10,
                engagement=0.75,
                brow_furrow=0.55 if is_spike else 0.10,
                ear=0.20 if is_spike else 0.32,
                mouth_tension=0.10,
                brow_height=0.45,
                step_id=step_id,
                is_simulated=True
            )
            evt = predictor.process_sample(s)
            if evt:
                fp_detected = True
                log_record(evt, sim_session_time)

        if not fp_detected:
            stats["spike_noise"]["tn"] += 1
        else:
            stats["spike_noise"]["fp"] += 1

    # 6. Tracking Loss Disruption Sequences (20 runs)
    for i in range(20):
        predictor.reset()
        step_id = "step_03_led"
        disruption_triggered = False
        recovered = False
        
        for step in range(5):
            t = step * 0.5
            sim_session_time += 0.5
            s = TimedAffectSample(
                timestamp=t,
                confusion=0.2, frustration=0.1, engagement=0.7,
                brow_furrow=0.1, ear=0.3, mouth_tension=0.1, brow_height=0.4,
                step_id=step_id, is_simulated=True, face_detected=True
            )
            predictor.process_sample(s)

        for step in range(5, 11):
            t = step * 0.5
            sim_session_time += 0.5
            s = TimedAffectSample(
                timestamp=t,
                confusion=0.0, frustration=0.0, engagement=0.0,
                brow_furrow=0.0, ear=0.0, mouth_tension=0.0, brow_height=0.0,
                step_id=step_id, is_simulated=True, face_detected=False
            )
            evt = predictor.process_sample(s)
            if evt and evt.behavior == "TrackingLossDisruption":
                disruption_triggered = True
                log_record(evt, sim_session_time)

        for step in range(11, 15):
            t = step * 0.5
            sim_session_time += 0.5
            s = TimedAffectSample(
                timestamp=t,
                confusion=0.2, frustration=0.1, engagement=0.7,
                brow_furrow=0.1, ear=0.3, mouth_tension=0.1, brow_height=0.4,
                step_id=step_id, is_simulated=True, face_detected=True
            )
            predictor.process_sample(s)
            if predictor.state == "IDLE":
                recovered = True

        if disruption_triggered:
            stats["tracking_loss"]["triggered"] += 1
        if recovered:
            stats["tracking_loss"]["recovered"] += 1

    # 7. Recovery Sequences (10 runs)
    for i in range(10):
        predictor.reset()
        step_id = "step_02_resistor"
        
        for step in range(5):
            t = step * 0.5
            sim_session_time += 0.5
            c = 0.36 + step * 0.03
            s = TimedAffectSample(
                timestamp=t,
                confusion=c, frustration=0.1, engagement=0.6,
                brow_furrow=c, ear=0.28, mouth_tension=0.1, brow_height=0.4,
                step_id=step_id, is_simulated=True
            )
            predictor.process_sample(s)

        for step in range(5, 11):
            t = step * 0.5
            sim_session_time += 0.5
            s = TimedAffectSample(
                timestamp=t,
                confusion=0.15, frustration=0.08, engagement=0.85,
                brow_furrow=0.10, ear=0.33, mouth_tension=0.05, brow_height=0.5,
                step_id=step_id, is_simulated=True
            )
            predictor.process_sample(s)

        if predictor.state == "RECOVERY":
            stats["recovery_sequences"]["recovered"] += 1

    with open(jsonl_log_path, "w", encoding="utf-8") as f:
        for rec in jsonl_records:
            f.write(json.dumps(rec) + "\n")

    all_lead_times = (
        stats["confusion_ramp"]["lead_times"]
        + stats["frustration_ramp"]["lead_times"]
        + stats["polarity_hesitation"]["lead_times"]
    )

    total_pred_tp = stats["confusion_ramp"]["tp"] + stats["frustration_ramp"]["tp"] + stats["polarity_hesitation"]["tp"]
    total_pred_fp = stats["confusion_ramp"]["fp"] + stats["frustration_ramp"]["fp"] + stats["neutral_control"]["fp"] + stats["spike_noise"]["fp"]
    total_pred_fn = stats["confusion_ramp"]["fn"] + stats["frustration_ramp"]["fn"] + stats["polarity_hesitation"]["fn"]
    total_pred_tn = stats["neutral_control"]["tn"] + stats["spike_noise"]["tn"]

    precision = total_pred_tp / (total_pred_tp + total_pred_fp) if (total_pred_tp + total_pred_fp) > 0 else 0.0
    recall = total_pred_tp / (total_pred_tp + total_pred_fn) if (total_pred_tp + total_pred_fn) > 0 else 0.0
    f1 = 2 * precision * recall / (precision + recall) if (precision + recall) > 0 else 0.0

    summary = {
        "evaluation_timestamp": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "total_test_sequences": 180,
        "total_jsonl_records": len(jsonl_records),
        "contingency_matrix": {
            "true_positives": total_pred_tp,
            "false_positives": total_pred_fp,
            "false_negatives": total_pred_fn,
            "true_negatives": total_pred_tn
        },
        "performance_metrics": {
            "precision": round(precision, 4),
            "recall": round(recall, 4),
            "f1_score": round(f1, 4),
            "specificity": round(total_pred_tn / (total_pred_tn + total_pred_fp), 4) if (total_pred_tn + total_pred_fp) > 0 else 0.0
        },
        "lead_time_statistics_seconds": {
            "mean_lead_time": round(float(np.mean(all_lead_times)), 3) if all_lead_times else 0.0,
            "median_lead_time": round(float(np.median(all_lead_times)), 3) if all_lead_times else 0.0,
            "std_lead_time": round(float(np.std(all_lead_times)), 3) if all_lead_times else 0.0,
            "min_lead_time": round(float(np.min(all_lead_times)), 3) if all_lead_times else 0.0,
            "max_lead_time": round(float(np.max(all_lead_times)), 3) if all_lead_times else 0.0,
            "sample_count": len(all_lead_times)
        },
        "breakdown_by_category": {
            "confusion_struggle": {
                "total": stats["confusion_ramp"]["total"],
                "tp": stats["confusion_ramp"]["tp"],
                "mean_lead_time": round(float(np.mean(stats["confusion_ramp"]["lead_times"])), 3) if stats["confusion_ramp"]["lead_times"] else 0.0
            },
            "frustration_disengage": {
                "total": stats["frustration_ramp"]["total"],
                "tp": stats["frustration_ramp"]["tp"],
                "mean_lead_time": round(float(np.mean(stats["frustration_ramp"]["lead_times"])), 3) if stats["frustration_ramp"]["lead_times"] else 0.0
            },
            "polarity_hesitation": {
                "total": stats["polarity_hesitation"]["total"],
                "tp": stats["polarity_hesitation"]["tp"],
                "mean_lead_time": round(float(np.mean(stats["polarity_hesitation"]["lead_times"])), 3) if stats["polarity_hesitation"]["lead_times"] else 0.0
            },
            "neutral_negative_control": {
                "total": stats["neutral_control"]["total"],
                "false_alarms": stats["neutral_control"]["fp"],
                "true_negatives": stats["neutral_control"]["tn"]
            },
            "spike_rejection": {
                "total": stats["spike_noise"]["total"],
                "false_alarms": stats["spike_noise"]["fp"],
                "spikes_rejected": stats["spike_noise"]["tn"]
            },
            "tracking_loss_disruption": {
                "total": stats["tracking_loss"]["total"],
                "triggered": stats["tracking_loss"]["triggered"],
                "recovered": stats["tracking_loss"]["recovered"]
            },
            "recovery_reset": {
                "total": stats["recovery_sequences"]["total"],
                "recovered": stats["recovery_sequences"]["recovered"]
            }
        }
    }

    with open(json_summary_path, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)

    print(json.dumps(summary, indent=2))

if __name__ == "__main__":
    run_evaluation()
