using System;
using UnityEngine;

namespace A3R.Affect
{
    /// <summary>
    /// AffectFeatureExtractor — Module 2 (sub-component) of the A³R system.
    ///
    /// Computes geometric facial features from 478-point MediaPipe Face Landmarker output.
    /// Uses the EXACT same formulas as extract_features.py in python_tools/daisee_calibration/
    /// to ensure calibrated thresholds transfer correctly from offline Python to on-device C#.
    ///
    /// Features:
    ///   1. Brow Furrow Distance (normalised) — high when brows pulled together
    ///   2. Eye Aspect Ratio (EAR) — low when squinting
    ///   3. Mouth Corner Tension — high when mouth corners pulled down
    ///   4. Brow Height (normalised) — low when brows lowered toward eyes
    ///
    /// Landmark indices match those defined in PROJECT_PLAN.md Section 6.
    /// </summary>
    public static class AffectFeatureExtractor
    {
        // ── MediaPipe Face Landmarker Indices ────────────────────────────────────
        // MUST match extract_features.py. Do not change without updating both files.

        // Brow furrow
        private const int LM_L_INNER_BROW = 105;
        private const int LM_R_INNER_BROW = 334;

        // Inter-ocular distance (IOD) anchor
        private const int LM_L_EYE_OUTER  = 33;
        private const int LM_R_EYE_OUTER  = 263;

        // Left eye EAR
        private const int LM_L_EYE_TOP    = 159;
        private const int LM_L_EYE_BOT    = 145;
        private const int LM_L_EYE_INNER  = 133;

        // Right eye EAR
        private const int LM_R_EYE_TOP    = 386;
        private const int LM_R_EYE_BOT    = 374;
        private const int LM_R_EYE_INNER  = 362;

        // Mouth
        private const int LM_L_MOUTH      = 61;
        private const int LM_R_MOUTH      = 291;
        private const int LM_UPPER_LIP    = 13;
        private const int LM_LOWER_LIP    = 14;

        // Brow height
        private const int LM_L_OUTER_BROW = 65;
        private const int LM_R_OUTER_BROW = 295;
        private const int LM_L_EYE_CENTER = 159;  // reuse EYE_TOP as eye center approximation
        private const int LM_R_EYE_CENTER = 386;

        // Face height normalisation
        private const int LM_FACE_TOP     = 10;
        private const int LM_FACE_BOT     = 152;

        // ── Public Struct ────────────────────────────────────────────────────────
        /// <summary>Computed geometric features for a single frame.</summary>
        public struct FaceFeatures
        {
            /// <summary>1 = brows fully furrowed; 0 = brows normally apart</summary>
            public float BrowFurrowNorm;

            /// <summary>Eye Aspect Ratio: ~0.25-0.35 open eye; lower = squinting</summary>
            public float EarNorm;

            /// <summary>Mouth corner tension: 1 = corners pulled down; 0 = neutral</summary>
            public float MouthTensionNorm;

            /// <summary>Brow height relative to eye: 1 = brows raised; 0 = brows low</summary>
            public float BrowHeightNorm;

            /// <summary>Inter-ocular distance in normalised image coordinates (debug)</summary>
            public float IOD;

            /// <summary>True if all features were computed successfully</summary>
            public bool Valid;
        }

        // ── Main Entry Point ─────────────────────────────────────────────────────
        /// <summary>
        /// Compute features from a flat array of landmark positions.
        /// The array must have at least 478 elements (3 floats each: x, y, z in normalised coords).
        ///
        /// In practice, FaceLandmarkBridge.cs provides this array from the MediaPipe output.
        /// </summary>
        /// <param name="landmarks">Flat array: [lm0_x, lm0_y, lm0_z, lm1_x, lm1_y, lm1_z, ...]</param>
        public static FaceFeatures Extract(float[] landmarks)
        {
            var result = new FaceFeatures { Valid = false };
            if (landmarks == null || landmarks.Length < 478 * 3)
                return result;

            // ── IOD ──────────────────────────────────────────────────────────────
            Vector2 lOuter = LM(landmarks, LM_L_EYE_OUTER);
            Vector2 rOuter = LM(landmarks, LM_R_EYE_OUTER);
            float iod = Vector2.Distance(lOuter, rOuter);
            if (iod < 1e-6f) return result;

            // ── Face height ──────────────────────────────────────────────────────
            float faceHeight = Mathf.Abs(LM(landmarks, LM_FACE_BOT).y - LM(landmarks, LM_FACE_TOP).y);
            if (faceHeight < 1e-6f) return result;

            // ── Feature 1: Brow Furrow Distance ─────────────────────────────────
            Vector2 lInnerBrow = LM(landmarks, LM_L_INNER_BROW);
            Vector2 rInnerBrow = LM(landmarks, LM_R_INNER_BROW);
            float browFurrowRaw = Vector2.Distance(lInnerBrow, rInnerBrow) / iod;
            float browFurrowNorm = Mathf.Clamp01(1.0f - browFurrowRaw);

            // ── Feature 2: Eye Aspect Ratio (EAR) ───────────────────────────────
            float earL = EAR(landmarks, LM_L_EYE_TOP, LM_L_EYE_BOT, LM_L_EYE_INNER, LM_L_EYE_OUTER);
            float earR = EAR(landmarks, LM_R_EYE_TOP, LM_R_EYE_BOT, LM_R_EYE_INNER, LM_R_EYE_OUTER);
            float earNorm = (earL + earR) * 0.5f;

            // ── Feature 3: Mouth Corner Tension ─────────────────────────────────
            float lipMidY = (LM(landmarks, LM_UPPER_LIP).y + LM(landmarks, LM_LOWER_LIP).y) * 0.5f;
            float lDisp   = lipMidY - LM(landmarks, LM_L_MOUTH).y;
            float rDisp   = lipMidY - LM(landmarks, LM_R_MOUTH).y;
            float avgDisp = (lDisp + rDisp) * 0.5f;
            float mouthTensionNorm = Mathf.Clamp01(-avgDisp / (iod + 1e-6f) * 3.0f);

            // ── Feature 4: Brow Height ────────────────────────────────────────────
            float lBrowH = Mathf.Abs(LM(landmarks, LM_L_OUTER_BROW).y - LM(landmarks, LM_L_EYE_CENTER).y);
            float rBrowH = Mathf.Abs(LM(landmarks, LM_R_OUTER_BROW).y - LM(landmarks, LM_R_EYE_CENTER).y);
            float browHeightNorm = Mathf.Clamp01(((lBrowH + rBrowH) * 0.5f) / (faceHeight + 1e-6f) * 5.0f);

            result.BrowFurrowNorm   = browFurrowNorm;
            result.EarNorm          = earNorm;
            result.MouthTensionNorm = mouthTensionNorm;
            result.BrowHeightNorm   = browHeightNorm;
            result.IOD              = iod;
            result.Valid            = true;

            return result;
        }

        // ── Private Helpers ──────────────────────────────────────────────────────
        /// <summary>Read 2D position (x,y) of landmark at index from flat float array.</summary>
        private static Vector2 LM(float[] lms, int idx)
        {
            int i = idx * 3;
            return new Vector2(lms[i], lms[i + 1]);
        }

        /// <summary>Eye Aspect Ratio for one eye: |top-bot| / (2 * |inner-outer|)</summary>
        private static float EAR(float[] lms, int top, int bot, int inner, int outer)
        {
            float v = Vector2.Distance(LM(lms, top), LM(lms, bot));
            float h = Vector2.Distance(LM(lms, inner), LM(lms, outer));
            return h > 1e-6f ? v / (2.0f * h) : 0f;
        }
    }
}
