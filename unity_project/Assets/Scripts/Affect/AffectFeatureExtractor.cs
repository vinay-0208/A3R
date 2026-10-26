using System;
using UnityEngine;

namespace A3R.Affect
{
    /// <summary>
    /// AffectFeatureExtractor
    /// </summary>
    public static class AffectFeatureExtractor
    {
        private const int LM_L_INNER_BROW = 105;
        private const int LM_R_INNER_BROW = 334;
        private const int LM_L_EYE_OUTER  = 33;
        private const int LM_R_EYE_OUTER  = 263;
        private const int LM_L_EYE_TOP    = 159;
        private const int LM_L_EYE_BOT    = 145;
        private const int LM_L_EYE_INNER  = 133;
        private const int LM_R_EYE_TOP    = 386;
        private const int LM_R_EYE_BOT    = 374;
        private const int LM_R_EYE_INNER  = 362;
        private const int LM_L_MOUTH      = 61;
        private const int LM_R_MOUTH      = 291;
        private const int LM_UPPER_LIP    = 13;
        private const int LM_LOWER_LIP    = 14;
        private const int LM_L_OUTER_BROW = 65;
        private const int LM_R_OUTER_BROW = 295;
        private const int LM_L_EYE_CENTER = 159;
        private const int LM_R_EYE_CENTER = 386;
        private const int LM_FACE_TOP     = 10;
        private const int LM_FACE_BOT     = 152;
        private const int LM_NOSE_TIP     = 1;

        public const float EAR_MIN = 0.04f;
        public const float EAR_MAX = 0.16f;

        // Pose Gate Bounds (Yaw is empirically supported; Pitch is a conservative experiment)
        private const float POSE_YAW_MIN = 0.90f;
        private const float POSE_YAW_MAX = 1.15f;
        private const float POSE_PITCH_MIN = 0.85f;
        private const float POSE_PITCH_MAX = 1.45f;

        public struct FaceFeatures
        {
            public float BrowFurrowRaw;
            public float BrowFurrowNorm;
            public float EarRaw;
            public float EarNorm;
            public float MouthTensionRaw;
            public float MouthTensionNorm;
            public float BrowHeightNorm;
            public float IOD;
            
            // Diagnostics
            public float YawRatio;
            public float PitchRatio;
            public bool PoseGateActive;

            public bool Valid;
        }

        public static FaceFeatures Extract(float[] landmarks)
        {
            var result = new FaceFeatures { Valid = false };
            if (landmarks == null || landmarks.Length < 478 * 3)
                return result;

            Vector3 lOuter = LM(landmarks, LM_L_EYE_OUTER);
            Vector3 rOuter = LM(landmarks, LM_R_EYE_OUTER);
            float iod = Vector3.Distance(lOuter, rOuter);
            result.IOD = iod;
            if (iod < 1e-6f) return result;

            float faceHeight = Mathf.Abs(LM(landmarks, LM_FACE_BOT).y - LM(landmarks, LM_FACE_TOP).y);
            if (faceHeight < 1e-6f) return result;

            Vector3 lInnerBrow = LM(landmarks, LM_L_INNER_BROW);
            Vector3 rInnerBrow = LM(landmarks, LM_R_INNER_BROW);
            float browFurrowRaw = Vector3.Distance(lInnerBrow, rInnerBrow) / iod;
            float browFurrowNorm = Mathf.Clamp01((0.98f - browFurrowRaw) / (0.98f - 0.90f));

            float earL = EAR(landmarks, LM_L_EYE_TOP, LM_L_EYE_BOT, LM_L_EYE_INNER, LM_L_EYE_OUTER);
            float earR = EAR(landmarks, LM_R_EYE_TOP, LM_R_EYE_BOT, LM_R_EYE_INNER, LM_R_EYE_OUTER);
            float earRaw = (earL + earR) * 0.5f;
            float earNorm = Mathf.Clamp01((earRaw - EAR_MIN) / (EAR_MAX - EAR_MIN));

            float lipMidY = (LM(landmarks, LM_UPPER_LIP).y + LM(landmarks, LM_LOWER_LIP).y) * 0.5f;
            float lDisp   = lipMidY - LM(landmarks, LM_L_MOUTH).y;
            float rDisp   = lipMidY - LM(landmarks, LM_R_MOUTH).y;
            float avgDisp = (lDisp + rDisp) * 0.5f;
            float mouthTensionNorm = Mathf.Clamp01((0.000f - avgDisp) / 0.008f);

            float lBrowH = Mathf.Abs(LM(landmarks, LM_L_OUTER_BROW).y - LM(landmarks, LM_L_EYE_CENTER).y);
            float rBrowH = Mathf.Abs(LM(landmarks, LM_R_OUTER_BROW).y - LM(landmarks, LM_R_EYE_CENTER).y);
            float browHeightNorm = Mathf.Clamp01(((lBrowH + rBrowH) * 0.5f) / (faceHeight + 1e-6f) * 5.0f);

            // DIAGNOSTICS & GATING
            Vector3 nose = LM(landmarks, LM_NOSE_TIP);
            float yawRatio = Vector3.Distance(nose, lOuter) / Vector3.Distance(nose, rOuter);
            float pitchRatio = Vector3.Distance(nose, LM(landmarks, LM_FACE_TOP)) / Vector3.Distance(nose, LM(landmarks, LM_FACE_BOT));

            result.YawRatio = yawRatio;
            result.PitchRatio = pitchRatio;

            bool poseValid = (yawRatio >= POSE_YAW_MIN && yawRatio <= POSE_YAW_MAX) &&
                             (pitchRatio >= POSE_PITCH_MIN && pitchRatio <= POSE_PITCH_MAX);
            result.PoseGateActive = !poseValid;

            if (!poseValid)
            {
                // Force features to neutral bounds to suppress false expressions
                browFurrowNorm = 0.0f;
                earNorm = 1.0f;
                // MouthTensionNorm remains unchanged
            }

            result.BrowFurrowRaw    = browFurrowRaw;
            result.BrowFurrowNorm   = browFurrowNorm;
            result.EarRaw           = earRaw;
            result.EarNorm          = earNorm;
            result.MouthTensionRaw  = avgDisp;
            result.MouthTensionNorm = mouthTensionNorm;
            result.BrowHeightNorm   = browHeightNorm;

            result.Valid = true;
            return result;
        }

        private static Vector3 LM(float[] lms, int idx)
        {
            int i = idx * 3;
            return new Vector3(lms[i], lms[i + 1], lms[i + 2]);
        }

        private static float EAR(float[] lms, int top, int bot, int inner, int outer)
        {
            float v = Vector3.Distance(LM(lms, top), LM(lms, bot));
            float h = Vector3.Distance(LM(lms, inner), LM(lms, outer));
            return h > 1e-6f ? v / (2.0f * h) : 0f;
        }
    }
}
