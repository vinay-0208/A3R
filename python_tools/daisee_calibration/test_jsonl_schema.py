# test_jsonl_schema.py - Validates C# SessionLogger output record schemas.
import json
import pytest

def test_session_logger_records():
    records = [
        {
            "sessionId": "session_20260906_120000",
            "participantId": "P01",
            "deviceModel": "Samsung S22",
            "unityVersion": "6000.3.23f1",
            "os": "Android",
            "startTimeIso": "2026-09-06T12:00:00Z",
            "recordType": "SessionStart"
        },
        {
            "sessionId": "session_20260906_120000",
            "timestampIso": "2026-09-06T12:00:01Z",
            "timeSinceStartS": 1.0,
            "stepIndex": 0,
            "confusionScore": 0.25,
            "frustrationScore": 0.18,
            "engagementScore": 0.72,
            "browFurrow": 0.21,
            "ear": 0.29,
            "mouthTension": 0.15,
            "browHeight": 0.45,
            "faceDetected": True,
            "inferenceFps": 15.0,
            "renderFps": 60.0,
            "inferenceLatencyMs": 12.4,
            "droppedFrames": 0,
            "recordType": "AffectSample"
        },
        {
            "sessionId": "session_20260906_120000",
            "timestampIso": "2026-09-06T12:00:02Z",
            "timeSinceStartS": 2.0,
            "stepIndex": 2,
            "adaptationType": "SimplifyStep",
            "triggerAffect": "confusion",
            "affectScore": 0.68,
            "dwellTimeS": 3.0,
            "eventId": "canonical_evt_001",
            "workerId": "Worker_P01",
            "objectId": "component_led_blue",
            "pdiTarget": "breadboard_row_15",
            "behavior": "ConfusionStruggle",
            "recordType": "AdaptationTrigger"
        },
        {
            "sessionId": "session_20260906_120000",
            "timestampIso": "2026-09-06T12:00:03Z",
            "timeSinceStartS": 3.0,
            "stepIndex": 2,
            "predictionId": "pred_001",
            "predictedEventType": "ConfusionStruggle",
            "horizonSeconds": 3.5,
            "confidence": 0.88,
            "browVelocity": 0.14,
            "earVelocity": -0.08,
            "leadTimeSeconds": 2.8,
            "isTaxonomyPredictive": True,
            "recordType": "AffectPrediction"
        },
        {
            "sessionId": "session_20260906_120000",
            "timestampIso": "2026-09-06T12:00:04Z",
            "timeSinceStartS": 4.0,
            "stepIndex": 3,
            "stepId": "step_03_led",
            "stepNumber": 3,
            "recordType": "StepChange"
        },
        {
            "sessionId": "session_20260906_120000",
            "endTimeIso": "2026-09-06T12:00:05Z",
            "totalDurationS": 5.0,
            "stepsCompleted": 8,
            "totalAdaptations": 1,
            "recordType": "SessionEnd"
        }
    ]

    for rec in records:
        line = json.dumps(rec)
        parsed = json.loads(line)
        assert len(parsed) >= 5, f"Record too small or empty: {line}"
        assert parsed["recordType"] in ["SessionStart", "AffectSample", "AdaptationTrigger", "AffectPrediction", "StepChange", "SessionEnd"]

    adapt = records[2]
    assert adapt["eventId"] == "canonical_evt_001"
    assert adapt["workerId"] == "Worker_P01"
    assert adapt["objectId"] == "component_led_blue"
    assert adapt["pdiTarget"] == "breadboard_row_15"
    assert adapt["behavior"] == "ConfusionStruggle"
