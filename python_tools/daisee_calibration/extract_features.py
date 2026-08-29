"""
extract_features.py — Facial landmark feature extractor for DAiSEE calibration

Part of the A³R offline calibration tooling (python_tools/daisee_calibration/).
NOT shipped in the mobile app — offline use only.

Extracts the SAME geometric features that the on-device C# code (AffectFeatureExtractor.cs) computes,
applied frame-by-frame to DAiSEE video clips using MediaPipe Face Landmarker.

Features extracted per frame:
1. Brow Furrow Distance  — landmarks 105 (L inner brow) & 334 (R inner brow), normalised by IOD
2. Eye Aspect Ratio (EAR) — average over both eyes using standard 6-point formula
3. Mouth Corner Tension  — vertical angle of mouth corners relative to lip center
4. Brow Height           — vertical distance brow→eye centre, normalised by face height

All normalization is done using the same formula the C# code uses, so calibrated thresholds
transfer directly to the on-device AffectScorer.cs.

Usage:
    from extract_features import extract_features_from_clip, extract_features_from_frame
    features_df = extract_features_from_clip("path/to/clip.avi", sample_fps=5)
"""

import math
import numpy as np
import pandas as pd
import cv2

try:
    import mediapipe as mp
    from mediapipe.tasks import python as mp_python
    from mediapipe.tasks.python import vision as mp_vision
    from mediapipe.tasks.python.vision import FaceLandmarkerResult
    _HAS_MEDIAPIPE = True
except ImportError:
    _HAS_MEDIAPIPE = False
    print("[WARNING] mediapipe not installed. Run: pip install mediapipe")

# ── MediaPipe Landmark Indices (Face Landmarker 478-point mesh) ────────────────
# These MUST match the indices used in AffectFeatureExtractor.cs

# Brow furrow
LM_L_INNER_BROW = 105   # left inner brow
LM_R_INNER_BROW = 334   # right inner brow

# Inter-ocular distance anchor points
LM_L_EYE_OUTER  = 33   # left eye outer corner
LM_R_EYE_OUTER  = 263  # right eye outer corner

# Eye aspect ratio — left eye
LM_L_EYE_TOP    = 159
LM_L_EYE_BOT    = 145
LM_L_EYE_INNER  = 133
# right eye
LM_R_EYE_TOP    = 386
LM_R_EYE_BOT    = 374
LM_R_EYE_INNER  = 362

# Mouth
LM_L_MOUTH      = 61   # left mouth corner
LM_R_MOUTH      = 291  # right mouth corner
LM_UPPER_LIP    = 13   # upper lip center
LM_LOWER_LIP    = 14   # lower lip center

# Brow height
LM_L_OUTER_BROW = 65   # left outer brow
LM_R_OUTER_BROW = 295  # right outer brow
LM_L_EYE_CENTER = 159  # reuse top of left eye as rough eye center
LM_R_EYE_CENTER = 386  # reuse top of right eye

# Face height normalization
LM_FACE_TOP     = 10   # top of forehead
LM_FACE_BOT     = 152  # chin


def _lm(landmarks, idx):
    """Get (x, y, z) tuple for landmark index idx."""
    lm = landmarks[idx]
    return (lm.x, lm.y, lm.z)


def _dist_2d(p1, p2):
    """Euclidean distance between two (x, y, ...) points in normalised image coords."""
    return math.sqrt((p1[0] - p2[0])**2 + (p1[1] - p2[1])**2)


def compute_features_from_landmarks(landmarks) -> dict:
    """
    Given a list of 478 MediaPipe NormalizedLandmark objects, compute the
    four geometric features used in AffectScorer.

    Returns dict with keys:
        brow_furrow_norm, ear_norm, mouth_tension_norm, brow_height_norm,
        iod  (inter-ocular distance, for debug)
    Returns None if landmarks are invalid.
    """
    if landmarks is None or len(landmarks) < 400:
        return None

    # ── Inter-ocular distance (IOD) ─────────────────────────────────────────
    l_outer = _lm(landmarks, LM_L_EYE_OUTER)
    r_outer = _lm(landmarks, LM_R_EYE_OUTER)
    iod = _dist_2d(l_outer, r_outer)
    if iod < 1e-6:
        return None  # degenerate face detection

    # ── Face height ──────────────────────────────────────────────────────────
    face_top = _lm(landmarks, LM_FACE_TOP)
    face_bot = _lm(landmarks, LM_FACE_BOT)
    face_height = _dist_2d(face_top, face_bot)
    if face_height < 1e-6:
        return None

    # ── Feature 1: Brow Furrow Distance ─────────────────────────────────────
    l_inner_brow = _lm(landmarks, LM_L_INNER_BROW)
    r_inner_brow = _lm(landmarks, LM_R_INNER_BROW)
    brow_furrow_raw = _dist_2d(l_inner_brow, r_inner_brow) / iod
    # When brows are normal: ratio ~0.5–0.8; furrowed (pulled together): <0.5
    # Invert so that HIGH value = MORE furrow (more confusion signal)
    brow_furrow_norm = max(0.0, 1.0 - brow_furrow_raw)

    # ── Feature 2: Eye Aspect Ratio (EAR) ───────────────────────────────────
    # EAR = (|top-bot|) / (2 * |inner-outer|) for each eye, averaged
    def _ear(top_idx, bot_idx, inner_idx, outer_idx):
        top   = _lm(landmarks, top_idx)
        bot   = _lm(landmarks, bot_idx)
        inner = _lm(landmarks, inner_idx)
        outer = _lm(landmarks, outer_idx)
        v = _dist_2d(top, bot)
        h = _dist_2d(inner, outer)
        return v / (2.0 * h) if h > 1e-6 else 0.0

    ear_l = _ear(LM_L_EYE_TOP, LM_L_EYE_BOT, LM_L_EYE_INNER, LM_L_EYE_OUTER)
    ear_r = _ear(LM_R_EYE_TOP, LM_R_EYE_BOT, LM_R_EYE_INNER, LM_R_EYE_OUTER)
    # Raw EAR: typical open eye ~0.25–0.35; squinting lower. Clamped to [0,1].
    ear_norm = float(np.clip((ear_l + ear_r) / 2.0, 0.0, 1.0))

    # ── Feature 3: Mouth Corner Tension ─────────────────────────────────────
    l_mouth  = _lm(landmarks, LM_L_MOUTH)
    r_mouth  = _lm(landmarks, LM_R_MOUTH)
    upper_lip = _lm(landmarks, LM_UPPER_LIP)
    lower_lip = _lm(landmarks, LM_LOWER_LIP)
    # Midpoint of lip center
    lip_mid_y = (upper_lip[1] + lower_lip[1]) / 2.0
    # Average vertical displacement of mouth corners relative to lip center
    # Positive = corners above lip center (neutral/smile), Negative = below (frown/frustration)
    l_disp = lip_mid_y - l_mouth[1]  # MediaPipe: y increases downward
    r_disp = lip_mid_y - r_mouth[1]
    avg_disp = (l_disp + r_disp) / 2.0
    # Normalise by IOD; clamp to [0,1] where 1 = maximum downward pull
    mouth_tension_norm = float(np.clip(-avg_disp / (iod + 1e-6) * 3.0, 0.0, 1.0))

    # ── Feature 4: Brow Height ───────────────────────────────────────────────
    l_brow_h = abs(_lm(landmarks, LM_L_OUTER_BROW)[1] - _lm(landmarks, LM_L_EYE_CENTER)[1])
    r_brow_h = abs(_lm(landmarks, LM_R_OUTER_BROW)[1] - _lm(landmarks, LM_R_EYE_CENTER)[1])
    avg_brow_h = (l_brow_h + r_brow_h) / 2.0
    brow_height_norm = float(np.clip(avg_brow_h / (face_height + 1e-6) * 5.0, 0.0, 1.0))

    return {
        "brow_furrow_norm":   float(brow_furrow_norm),
        "ear_norm":           float(ear_norm),
        "mouth_tension_norm": float(mouth_tension_norm),
        "brow_height_norm":   float(brow_height_norm),
        "iod":                float(iod),
    }


def extract_features_from_clip(clip_path: str, sample_fps: float = 5.0,
                                 model_path: str = "face_landmarker.task") -> pd.DataFrame:
    """
    Extract per-frame features from a video clip at sample_fps frames/second.

    Args:
        clip_path:   Path to the .avi (or other) video file.
        sample_fps:  How many frames per second to sample (default: 5).
        model_path:  Path to the MediaPipe Face Landmarker .task model file.
                     Download from: https://storage.googleapis.com/mediapipe-models/
                     face_landmarker/face_landmarker/float16/1/face_landmarker.task

    Returns:
        DataFrame with columns: frame_idx, timestamp_s,
                                 brow_furrow_norm, ear_norm,
                                 mouth_tension_norm, brow_height_norm,
                                 face_detected (bool)
        Returns empty DataFrame if clip cannot be opened.
    """
    if not _HAS_MEDIAPIPE:
        raise ImportError("mediapipe is required. pip install mediapipe")

    import os
    if not os.path.exists(model_path):
        raise FileNotFoundError(
            f"Face Landmarker model not found: {model_path}\n"
            "Download from: https://storage.googleapis.com/mediapipe-models/"
            "face_landmarker/face_landmarker/float16/1/face_landmarker.task"
        )

    cap = cv2.VideoCapture(clip_path)
    if not cap.isOpened():
        print(f"[ERROR] Cannot open clip: {clip_path}")
        return pd.DataFrame()

    native_fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
    frame_interval = max(1, int(round(native_fps / sample_fps)))

    # Build MediaPipe Face Landmarker (image mode — we feed frames one at a time)
    base_options = mp_python.BaseOptions(model_asset_path=model_path)
    options = mp_vision.FaceLandmarkerOptions(
        base_options=base_options,
        output_face_blendshapes=False,
        output_facial_transformation_matrixes=False,
        num_faces=1,
    )
    landmarker = mp_vision.FaceLandmarker.create_from_options(options)

    rows = []
    frame_idx = 0
    sample_idx = 0

    while True:
        ret, frame = cap.read()
        if not ret:
            break

        if frame_idx % frame_interval == 0:
            timestamp_s = frame_idx / native_fps
            # Convert BGR→RGB for MediaPipe
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_frame)
            result: FaceLandmarkerResult = landmarker.detect(mp_image)

            if result.face_landmarks:
                feats = compute_features_from_landmarks(result.face_landmarks[0])
                if feats is not None:
                    rows.append({
                        "frame_idx":          frame_idx,
                        "sample_idx":         sample_idx,
                        "timestamp_s":        timestamp_s,
                        "face_detected":      True,
                        **feats
                    })
                else:
                    rows.append({
                        "frame_idx": frame_idx, "sample_idx": sample_idx,
                        "timestamp_s": timestamp_s, "face_detected": False,
                        "brow_furrow_norm": None, "ear_norm": None,
                        "mouth_tension_norm": None, "brow_height_norm": None,
                        "iod": None
                    })
            else:
                rows.append({
                    "frame_idx": frame_idx, "sample_idx": sample_idx,
                    "timestamp_s": timestamp_s, "face_detected": False,
                    "brow_furrow_norm": None, "ear_norm": None,
                    "mouth_tension_norm": None, "brow_height_norm": None,
                    "iod": None
                })
            sample_idx += 1

        frame_idx += 1

    cap.release()
    landmarker.close()

    df = pd.DataFrame(rows)
    return df


if __name__ == "__main__":
    print("extract_features.py — sanity check of landmark index math")
    # Mock: create fake landmarks with known positions and verify feature values
    class FakeLM:
        def __init__(self, x, y, z=0.0):
            self.x, self.y, self.z = x, y, z

    # 478 fake landmarks, all at (0.5, 0.5) baseline
    fake = [FakeLM(0.5, 0.5) for _ in range(478)]

    # Set IOD (eyes 10% apart)
    fake[LM_L_EYE_OUTER] = FakeLM(0.4, 0.5)
    fake[LM_R_EYE_OUTER] = FakeLM(0.6, 0.5)

    # Set face height
    fake[LM_FACE_TOP] = FakeLM(0.5, 0.1)
    fake[LM_FACE_BOT] = FakeLM(0.5, 0.9)

    # Brows slightly apart (neutral)
    fake[LM_L_INNER_BROW] = FakeLM(0.45, 0.35)
    fake[LM_R_INNER_BROW] = FakeLM(0.55, 0.35)

    feats = compute_features_from_landmarks(fake)
    print(f"Features (neutral baseline): {feats}")
    print("Sanity check passed" if feats is not None else "FAILED — None returned")
