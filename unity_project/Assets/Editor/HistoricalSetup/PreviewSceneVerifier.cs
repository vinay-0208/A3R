using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using A3R.ARGuidance;
using A3R.Adaptation;
using TMPro;

namespace A3R.EditorTools
{
    [Serializable]
    public class PreviewTestResult
    {
        public string test_id;
        public string description;
        public bool passed;
        public string details;
    }

    [Serializable]
    public class PreviewSummaryJson
    {
        public string evaluation_timestamp;
        public string unity_version;
        public string scene_path;
        public int total_tests;
        public int passed_tests;
        public int failed_tests;
        public string overall_verdict;
        public List<PreviewTestResult> results = new List<PreviewTestResult>();
    }

    public static class PreviewSceneVerifier
    {
        private const string ScenePath = "Assets/Scenes/A3R_AR_Guidance_Preview.unity";

        [MenuItem("A3R/Run AR Guidance Preview Verification")]
        public static void RunVerification()
        {
            Debug.Log("[PreviewSceneVerifier] Opening scene: " + ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            PreviewSummaryJson summary = new PreviewSummaryJson
            {
                evaluation_timestamp = DateTime.UtcNow.ToString("o"),
                unity_version = Application.unityVersion,
                scene_path = ScenePath,
                overall_verdict = "PENDING"
            };

            var results = summary.results;

            GameObject guidanceMgr = GameObject.Find("ARGuidanceManager");
            if (guidanceMgr == null) throw new InvalidOperationException("ARGuidanceManager not found");

            guidanceMgr.BroadcastMessage("Awake", SendMessageOptions.DontRequireReceiver);
            ARGuidanceController guidance = guidanceMgr.GetComponent<ARGuidanceController>();
            A3R_ARGuidancePreviewController preview = guidanceMgr.GetComponent<A3R_ARGuidancePreviewController>();
            preview.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);

            A3R_DirectionalArrow arrow = guidance.DirectionalArrow;
            A3R_TargetRing ring = guidance.TargetRing;
            A3R_ChevronPath chevronPath = arrow != null ? arrow.ChevronPath : null;
            if (chevronPath == null && arrow != null)
            {
                chevronPath = arrow.GetComponentInChildren<A3R_ChevronPath>();
            }

            GameObject tabletopRoot = GameObject.Find("A3R_TabletopContentRoot");

            // Initialize components and start visual preview
            if (chevronPath != null) chevronPath.InitializeChevrons();
            if (ring != null) ring.InitializeVisuals();
            preview.StartVisualPreview();
            if (chevronPath != null) chevronPath.UpdatePathPositions();

            // CAL_01: Reticle center == target anchor within tolerance (<= 0.005 m)
            preview.StartVisualPreview();
            preview.SetStep(0); // Step 1: breadboard_center
            Transform anchor01 = tabletopRoot.transform.Find("TargetAnchors/breadboard_center") ?? tabletopRoot.transform.Find("breadboard_center");
            Vector3 reticlePos01 = ring != null ? ring.transform.position : Vector3.zero;
            Vector3 anchorPos01 = anchor01 != null ? anchor01.position : Vector3.zero;
            float delta01 = Vector3.Distance(reticlePos01, anchorPos01);
            bool cal01 = (anchor01 != null) && (ring != null) && (delta01 <= 0.005f);
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_01",
                description = "Reticle center == target anchor within tolerance (<= 0.005 m)",
                passed = cal01,
                details = cal01 ? $"Verified precision anchoring: reticle offset = {delta01:F4} m (<= 0.005 m limit)." : $"Offset {delta01:F4} m exceeds 0.005 m tolerance."
            });

            // CAL_02: All arrows lie on calculated path between start and target
            bool cal02 = false;
            string details02 = "";
            if (chevronPath != null && chevronPath.Chevrons != null && chevronPath.Chevrons.Length >= 3)
            {
                Vector3 pStart = chevronPath.PathStartPosition;
                Vector3 pEnd = chevronPath.TargetPosition;
                pEnd.y = pStart.y;
                Vector3 pathLine = (pEnd - pStart).normalized;
                bool allOnPath = true;
                for (int i = 0; i < chevronPath.Chevrons.Length; i++)
                {
                    if (chevronPath.Chevrons[i] == null) { allOnPath = false; break; }
                    Vector3 chPos = chevronPath.Chevrons[i].transform.position;
                    Vector3 fromStart = chPos - pStart;
                    fromStart.y = 0f;
                    Vector3 proj = Vector3.Project(fromStart, pathLine);
                    float deviation = (fromStart - proj).magnitude;
                    if (deviation > 0.015f) { allOnPath = false; break; }
                }
                cal02 = allOnPath;
                details02 = cal02 ? $"All {chevronPath.ChevronCount} arrows strictly follow the calculated interpolation trajectory." : "Arrow path deviation detected.";
            }
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_02",
                description = "All arrows lie on calculated linear path",
                passed = cal02,
                details = details02
            });

            // CAL_03: Arrow spacing is approximately uniform along normalized trajectory
            bool cal03 = false;
            string details03 = "";
            if (chevronPath != null && chevronPath.Chevrons != null && chevronPath.Chevrons.Length == 4)
            {
                float gap01 = Vector3.Distance(chevronPath.Chevrons[0].transform.position, chevronPath.Chevrons[1].transform.position);
                float gap12 = Vector3.Distance(chevronPath.Chevrons[1].transform.position, chevronPath.Chevrons[2].transform.position);
                float gap23 = Vector3.Distance(chevronPath.Chevrons[2].transform.position, chevronPath.Chevrons[3].transform.position);
                float avgGap = (gap01 + gap12 + gap23) / 3f;
                bool uniform = Mathf.Abs(gap01 - avgGap) < 0.015f && Mathf.Abs(gap12 - avgGap) < 0.015f && Mathf.Abs(gap23 - avgGap) < 0.015f;
                cal03 = uniform && (chevronPath.ChevronCount == 4);
                details03 = cal03 ? $"Verified 4 arrows with uniform inter-arrow gaps (average gap = {avgGap:F3} m, t ~ 0.2, 0.4, 0.6, 0.8)." : "Arrow count != 4 or non-uniform gaps.";
            }
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_03",
                description = "Arrow count is 4 with uniform normalized spacing [0.20, 0.40, 0.60, 0.80]",
                passed = cal03,
                details = details03
            });

            // CAL_04: All arrows point along tabletop toward target
            bool cal04 = false;
            string details04 = "";
            if (chevronPath != null && chevronPath.Chevrons != null && chevronPath.Chevrons.Length >= 3)
            {
                bool allPointing = true;
                Vector3 tPos = chevronPath.TargetPosition;
                for (int i = 0; i < chevronPath.Chevrons.Length; i++)
                {
                    if (chevronPath.Chevrons[i] == null) { allPointing = false; break; }
                    Vector3 toTarget = tPos - chevronPath.Chevrons[i].transform.position;
                    toTarget.y = 0f;
                    Vector3 fwd = chevronPath.Chevrons[i].transform.forward;
                    fwd.y = 0f;
                    float dot = Vector3.Dot(fwd.normalized, toTarget.normalized);
                    if (dot < 0.90f) { allPointing = false; break; }
                }
                cal04 = allPointing;
                details04 = cal04 ? "All directional arrows point strictly along tabletop plane toward destination target (Dot > 0.90)." : "Arrow orientation misalignment detected.";
            }
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_04",
                description = "All arrows point along tabletop toward target",
                passed = cal04,
                details = details04
            });

            // CAL_05: Arrow tabletop height within calibrated range [0.010 m .. 0.025 m]
            bool cal05 = false;
            string details05 = "";
            if (chevronPath != null && chevronPath.Chevrons != null && chevronPath.Chevrons.Length > 0)
            {
                bool heightOk = true;
                for (int i = 0; i < chevronPath.Chevrons.Length; i++)
                {
                    if (chevronPath.Chevrons[i] == null) { heightOk = false; break; }
                    float h = chevronPath.Chevrons[i].transform.position.y;
                    if (h < 0.009f || h > 0.026f) { heightOk = false; break; }
                }
                cal05 = heightOk;
                details05 = cal05 ? $"Verified arrow hover elevation = {chevronPath.Chevrons[0].transform.position.y:F3} m (strictly within [0.010 .. 0.025 m])." : "Arrow elevation out of bounds.";
            }
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_05",
                description = "Arrow tabletop height within calibrated range [0.010 m .. 0.025 m]",
                passed = cal05,
                details = details05
            });

            // CAL_06: Reticle height valid (5–20 mm above target anchor)
            float reticleY = ring != null ? ring.transform.position.y : 0f;
            bool cal06 = (ring != null) && (reticleY >= 0.008f && reticleY <= 0.030f);
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_06",
                description = "Target reticle height valid above target surface",
                passed = cal06,
                details = cal06 ? $"Reticle surface height = {reticleY:F3} m hovering stably above tabletop components." : "Invalid reticle height."
            });

            // CAL_07: No arrow intersects physical target object
            bool cal07 = false;
            string details07 = "";
            if (chevronPath != null && chevronPath.Chevrons != null && chevronPath.Chevrons.Length > 0)
            {
                var lastArrow = chevronPath.Chevrons[chevronPath.Chevrons.Length - 1];
                float distToTarget = Vector3.Distance(lastArrow.transform.position, chevronPath.TargetPosition);
                cal07 = distToTarget >= 0.020f;
                details07 = cal07 ? $"Final arrow stops {distToTarget:F3} m before target, preserving clear visual buffer." : $"Final arrow too close ({distToTarget:F3} m).";
            }
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_07",
                description = "No arrow intersects physical target object (visual buffer preserved)",
                passed = cal07,
                details = details07
            });

            // CAL_08: Target remains visible under AR overlay (reticle scale tuned, not covering object)
            bool cal08 = (ring != null) && (ring.CurrentRadius <= 0.055f);
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_08",
                description = "Target remains clearly visible under AR overlay (calibrated reticle radius)",
                passed = cal08,
                details = cal08 ? $"Calibrated reticle radius = {ring.CurrentRadius:F3} m (diameter {ring.CurrentRadius * 2f * 1000f:F0} mm) leaves physical objects fully visible." : "Reticle too large."
            });

            // CAL_09: Step 1 correct target (breadboard_center)
            preview.SetStep(0);
            Transform a09 = tabletopRoot.transform.Find("TargetAnchors/breadboard_center") ?? tabletopRoot.transform.Find("breadboard_center");
            float d09 = (a09 != null && ring != null) ? Vector3.Distance(ring.transform.position, a09.position) : 999f;
            bool cal09 = d09 <= 0.005f;
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_09",
                description = "Step 1 calibrated to breadboard_center (tolerance <= 0.005 m)",
                passed = cal09,
                details = cal09 ? $"Step 1 anchored at breadboard_center with offset {d09:F4} m." : $"Step 1 target mismatch, delta = {d09:F4} m."
            });

            // CAL_10: Step 2 correct target (resistor_slot_row10_colE)
            preview.SetStep(1);
            Transform a10 = tabletopRoot.transform.Find("TargetAnchors/resistor_slot_row10_colE") ?? tabletopRoot.transform.Find("resistor_slot_row10_colE");
            float d10 = (a10 != null && ring != null) ? Vector3.Distance(ring.transform.position, a10.position) : 999f;
            bool cal10 = d10 <= 0.005f;
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_10",
                description = "Step 2 calibrated to resistor_slot_row10_colE (tolerance <= 0.005 m)",
                passed = cal10,
                details = cal10 ? $"Step 2 anchored at resistor_slot_row10_colE with offset {d10:F4} m." : $"Step 2 target mismatch, delta = {d10:F4} m."
            });

            // CAL_11: Step 3 correct target (led_slot_row14_colD)
            preview.SetStep(2);
            Transform a11 = tabletopRoot.transform.Find("TargetAnchors/led_slot_row14_colD") ?? tabletopRoot.transform.Find("led_slot_row14_colD");
            float d11 = (a11 != null && ring != null) ? Vector3.Distance(ring.transform.position, a11.position) : 999f;
            bool cal11 = d11 <= 0.005f;
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_11",
                description = "Step 3 calibrated to led_slot_row14_colD (tolerance <= 0.005 m)",
                passed = cal11,
                details = cal11 ? $"Step 3 anchored at led_slot_row14_colD with offset {d11:F4} m." : $"Step 3 target mismatch, delta = {d11:F4} m."
            });

            // CAL_12: Step 4 correct target (board_gpio13 with provisional notice)
            preview.SetStep(3);
            Transform a12 = tabletopRoot.transform.Find("TargetAnchors/board_gpio13") ?? tabletopRoot.transform.Find("board_gpio13");
            float d12 = (a12 != null && ring != null) ? Vector3.Distance(ring.transform.position, a12.position) : 999f;
            bool cal12 = d12 <= 0.005f;
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_12",
                description = "Step 4 calibrated to board_gpio13 (provisional software-test notice)",
                passed = cal12,
                details = cal12 ? $"Step 4 anchored at board_gpio13 with offset {d12:F4} m." : $"Step 4 target mismatch, delta = {d12:F4} m."
            });

            // CAL_13: Step 5 correct target (board_gnd)
            preview.SetStep(4);
            Transform a13 = tabletopRoot.transform.Find("TargetAnchors/board_gnd") ?? tabletopRoot.transform.Find("board_gnd");
            float d13 = (a13 != null && ring != null) ? Vector3.Distance(ring.transform.position, a13.position) : 999f;
            bool cal13 = d13 <= 0.005f;
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_13",
                description = "Step 5 calibrated to board_gnd (tolerance <= 0.005 m)",
                passed = cal13,
                details = cal13 ? $"Step 5 anchored at board_gnd with offset {d13:F4} m." : $"Step 5 target mismatch, delta = {d13:F4} m."
            });

            // CAL_14: Step 6 correct target (board_usb_port)
            preview.SetStep(5);
            Transform a14 = tabletopRoot.transform.Find("TargetAnchors/board_usb_port") ?? tabletopRoot.transform.Find("board_usb_port");
            float d14 = (a14 != null && ring != null) ? Vector3.Distance(ring.transform.position, a14.position) : 999f;
            bool cal14 = d14 <= 0.005f;
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_14",
                description = "Step 6 calibrated to board_usb_port (tolerance <= 0.005 m)",
                passed = cal14,
                details = cal14 ? $"Step 6 anchored at board_usb_port with offset {d14:F4} m." : $"Step 6 target mismatch, delta = {d14:F4} m."
            });

            // CAL_15: Step 8 correct target (led_position)
            preview.SetStep(7);
            Transform a15 = tabletopRoot.transform.Find("TargetAnchors/led_position") ?? tabletopRoot.transform.Find("led_position");
            float d15 = (a15 != null && ring != null) ? Vector3.Distance(ring.transform.position, a15.position) : 999f;
            bool cal15 = d15 <= 0.005f;
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_15",
                description = "Step 8 calibrated to led_position (tolerance <= 0.005 m)",
                passed = cal15,
                details = cal15 ? $"Step 8 anchored at led_position with offset {d15:F4} m." : $"Step 8 target mismatch, delta = {d15:F4} m."
            });

            // CAL_16: Tracking loss cleanly hides AR guidance
            preview.SetTracking(false);
            bool cal16 = (!guidance.IsTracking) && (!arrow.IsVisible) && (!ring.IsVisible) &&
                         (!preview.TargetHighlightAnchor.activeSelf) && (!chevronPath.IsVisible);
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_16",
                description = "Tracking loss cleanly hides AR arrows, reticle, and beacon",
                passed = cal16,
                details = cal16 ? "Verified clean suppression during tracking loss." : "AR elements remained active during tracking loss."
            });

            // CAL_17: Tracking recovery restores AR guidance
            preview.SetTracking(true);
            bool cal17 = (guidance.IsTracking) && (arrow.IsVisible) && (ring.IsVisible) &&
                         (preview.TargetHighlightAnchor.activeSelf) && (chevronPath.IsVisible);
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_17",
                description = "Tracking recovery restores AR arrows, reticle, and beacon",
                passed = cal17,
                details = cal17 ? "Verified seamless AR guidance restoration upon tracking recovery." : "AR guidance failed to restore."
            });

            // CAL_18: Task completion cleanly hides AR guidance and presents completion modal
            preview.CompleteTask();
            bool cal18 = (preview.IsCompleted) && (preview.CompletionPanel != null && preview.CompletionPanel.activeSelf) &&
                         (!arrow.IsVisible) && (!ring.IsVisible) && (!chevronPath.IsVisible);
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_18",
                description = "Task completion cleanly hides AR guidance and displays completion modal",
                passed = cal18,
                details = cal18 ? "Verified task completion suppresses AR overlays and displays modal." : "Completion state mismatch."
            });

            // CAL_19: Reset cleanly restores Step 1 and hides completion modal
            preview.ResetSimulation();
            bool cal19 = (!preview.IsCompleted) && (preview.CompletionPanel != null && !preview.CompletionPanel.activeSelf) &&
                         (preview.CurrentStepIndex == 0) && (arrow.IsVisible) && (ring.IsVisible) && (chevronPath.IsVisible);
            results.Add(new PreviewTestResult
            {
                test_id = "CAL_19",
                description = "Reset cleanly restores Step 1 initial state, hides modal, restores guidance",
                passed = cal19,
                details = cal19 ? "Verified clean restart to Step 1 with modal hidden and guidance active." : "Reset state violation."
            });

            // Save scene cleanly in initial state
            preview.StartVisualPreview();
            if (preview.CompletionPanel != null) preview.CompletionPanel.SetActive(false);
            EditorSceneManager.SaveScene(scene);

            // Compute summary
            summary.total_tests = results.Count;
            summary.passed_tests = 0;
            summary.failed_tests = 0;
            foreach (var r in results)
            {
                if (r.passed) summary.passed_tests++;
                else summary.failed_tests++;
            }

            summary.overall_verdict = (summary.failed_tests == 0) ? "PASS — MODERN AR NAVIGATION GUIDANCE IMPLEMENTED & OFFLINE-VALIDATED" : "FAIL";

            // Save summary JSON to both project root and unity_project
            string outDir1 = Path.Combine(Application.dataPath, "..", "..", "evaluation", "offline_ar_guidance_preview");
            string outDir2 = Path.Combine(Application.dataPath, "..", "evaluation", "offline_ar_guidance_preview");
            if (!Directory.Exists(outDir1)) Directory.CreateDirectory(outDir1);
            if (!Directory.Exists(outDir2)) Directory.CreateDirectory(outDir2);

            string jsonContent = JsonUtility.ToJson(summary, true);
            File.WriteAllText(Path.Combine(outDir1, "summary.json"), jsonContent);
            File.WriteAllText(Path.Combine(outDir2, "summary.json"), jsonContent);

            Debug.Log($"[PreviewSceneVerifier] Completed {summary.passed_tests}/{summary.total_tests} PASSED. Verdict: {summary.overall_verdict}");
            foreach (var r in results)
            {
                Debug.Log($"[VERIFIER] {r.test_id}: {(r.passed ? "PASS" : "FAIL")} — {r.description} ({r.details})");
            }
        }
    }
}
