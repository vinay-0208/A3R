using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using A3R.ARTracking;

namespace A3R.Diagnostics
{
    public class A3R_ARDiagnosticsUI : MonoBehaviour
    {
        private A3R_MarkerlessFoundationController _phase1Controller;
        private Camera _cachedCam;
        private Vector3 _lastCamPos;
        private Quaternion _lastCamRot;
        private float _camMoveDelta;
        private float _camRotDelta;
        private bool _isFirstSample = true;
        private float _sampleTimer = 0f;
        private const float SAMPLE_INTERVAL = 0.2f;
        private float _fps;
        private float _frameTime;
        private GUIStyle _labelStyle;
        private bool _stylesInitialized = false;
        private bool _arcoreRunning = false;
        private string _camTrackingState = "N/A";
        private float _statusTimer = 0f;

        private void Start()
        {
            _phase1Controller = FindFirstObjectByType<A3R_MarkerlessFoundationController>();
            _cachedCam = Camera.main;
            UpdateSubsystemStatus();
        }

        private void Update()
        {
            _frameTime = Time.deltaTime * 1000f;
            _fps = Time.deltaTime > 1e-5f ? 1.0f / Time.deltaTime : 60f;

            if (_cachedCam == null) _cachedCam = Camera.main;

            _sampleTimer += Time.deltaTime;
            if (_sampleTimer >= SAMPLE_INTERVAL)
            {
                _sampleTimer = 0f;
                if (_cachedCam != null)
                {
                    if (_isFirstSample)
                    {
                        _lastCamPos = _cachedCam.transform.position;
                        _lastCamRot = _cachedCam.transform.rotation;
                        _isFirstSample = false;
                    }
                    else
                    {
                        var cp = _cachedCam.transform.position;
                        var cr = _cachedCam.transform.rotation;
                        _camMoveDelta = Vector3.Distance(_lastCamPos, cp);
                        _camRotDelta = Quaternion.Angle(_lastCamRot, cr);
                        _lastCamPos = cp;
                        _lastCamRot = cr;
                    }
                }
            }

            _statusTimer += Time.deltaTime;
            if (_statusTimer >= 1.0f)
            {
                _statusTimer = 0f;
                UpdateSubsystemStatus();
            }
        }

        private void UpdateSubsystemStatus()
        {
            var loaders = UnityEngine.XR.Management.XRGeneralSettings.Instance?.Manager?.activeLoaders;
            _arcoreRunning = false;
            if (loaders != null)
            {
                foreach (var loader in loaders)
                {
                    if (loader != null && loader.name.Contains("ARCore"))
                    {
                        _arcoreRunning = true;
                        break;
                    }
                }
            }

            _camTrackingState = "N/A";
            if (_cachedCam != null)
            {
                var trackedPoseDriver = _cachedCam.GetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
                if (trackedPoseDriver != null)
                {
                    _camTrackingState = "TrackedPoseDriver Found";
                }
                else
                {
                    var tpd = _cachedCam.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                    if (tpd != null) _camTrackingState = "TrackedPoseDriver Found";
                }
            }
        }

        private void OnGUI()
        {
            if (!A3R.UI.A3R_CleanUIController.DeveloperModeEnabled) return;
            
            if (!_stylesInitialized)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 19,
                    wordWrap = false
                };
                _labelStyle.normal.textColor = Color.yellow;
                _stylesInitialized = true;
            }

            var s = _labelStyle;

            // 4-Corner Registration Instructional Prompts
            if (_phase1Controller != null && 
                (_phase1Controller.CurrentState == PlacementState.SEARCHING ||
                 (_phase1Controller.CurrentState >= PlacementState.FOUR_CORNER_TL &&
                  _phase1Controller.CurrentState <= PlacementState.FOUR_CORNER_BL)))
            {
                var promptStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 48,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                promptStyle.normal.textColor = Color.yellow;
                
                string promptTxt = "";
                switch (_phase1Controller.CurrentState)
                {
                    case PlacementState.SEARCHING: promptTxt = "Point the camera at the table"; break;
                    case PlacementState.FOUR_CORNER_TL: promptTxt = "Tap TOP-LEFT corner"; break;
                    case PlacementState.FOUR_CORNER_TR: promptTxt = "Tap TOP-RIGHT corner"; break;
                    case PlacementState.FOUR_CORNER_BR: promptTxt = "Tap BOTTOM-RIGHT corner"; break;
                    case PlacementState.FOUR_CORNER_BL: promptTxt = "Tap BOTTOM-LEFT corner"; break;
                }
                
                // Draw a large background box at the top
                GUI.Box(new Rect(0, 40, Screen.width, 100), "");
                GUI.Label(new Rect(0, 40, Screen.width, 100), promptTxt, promptStyle);
            }

            float panelW = Mathf.Min(Screen.width * 0.48f, 610f);
            GUILayout.BeginArea(new Rect(8, 8, panelW, Screen.height - 16));
            GUILayout.Label("=== A3R DIAGNOSTICS ===", s);

            string arSessionState = ARSession.state.ToString();
            GUILayout.Label($"\nAR SESSION\n  Session: {arSessionState}\n  ARCore: {(_arcoreRunning ? "Running" : "Not Running")}\n  Camera: {_camTrackingState}", s);

            if (_cachedCam != null)
            {
                var cp = _cachedCam.transform.position;
                var cr = _cachedCam.transform.eulerAngles;
                string moveDeltaStr = _isFirstSample ? "N/A" : _camMoveDelta.ToString("F5");
                string rotDeltaStr = _isFirstSample ? "N/A" : _camRotDelta.ToString("F5") + "°";
                GUILayout.Label($"\nCAMERA\n  Pos: {cp:F3}\n  Rot: {cr:F3}\n  Move Δ: {moveDeltaStr}\n  Rot Δ: {rotDeltaStr}", s);
            }

            if (_phase1Controller != null)
            {
                GUILayout.Label($"\nPLANE DETECTION\n  Planes: {_phase1Controller.PlaneCount}\n  Horizontal: {_phase1Controller.TrackingPlaneCount}\n  Best: {_phase1Controller.BestPlaneState}", s);

                GUILayout.Label($"\nRAYCAST\n  Hits: {_phase1Controller.RaycastHitCount}\n  Last Hit: {_phase1Controller.LastRaycastHitPos:F3}\n  Hit Type: {_phase1Controller.LastRaycastHitType}", s);

                string previewState = _phase1Controller.IsPreviewVisible ? "VISIBLE" : "HIDDEN";
                string rootState = _phase1Controller.HasPlacedRoot ? "ACTIVE" : "NONE";
                string candPosStr = _phase1Controller.HasValidPose ? _phase1Controller.CandidatePosition.ToString("F3") : "(NONE)";
                string readyPosStr = _phase1Controller.HasReadyPose ? _phase1Controller.ReadyPosition.ToString("F3") : "(NONE)";

                GUILayout.Label($"\nPLACEMENT\n  State: {_phase1Controller.CurrentState}\n  Preview: {previewState}\n  Root: {rootState}\n  CANDIDATE POSE: {candPosStr}\n  READY POSE: {readyPosStr}", s);

                // ── NEW FOUR-CORNER REGISTRATION DIAGNOSTICS ──────────
                string regStateStr = _phase1Controller.CurrentState.ToString();
                
                string p1 = _phase1Controller.CornerPoints[0] != Vector3.zero ? _phase1Controller.CornerPoints[0].ToString("F3") : "None";
                string p2 = _phase1Controller.CornerPoints[1] != Vector3.zero ? _phase1Controller.CornerPoints[1].ToString("F3") : "None";
                string p3 = _phase1Controller.CornerPoints[2] != Vector3.zero ? _phase1Controller.CornerPoints[2].ToString("F3") : "None";
                string p4 = _phase1Controller.CornerPoints[3] != Vector3.zero ? _phase1Controller.CornerPoints[3].ToString("F3") : "None";

                string wp = _phase1Controller.HasReadyPose ? _phase1Controller.ReadyPosition.ToString("F3") : "(NONE)";
                
                string ancProvStr = _phase1Controller.AnchorProviderCreated ? "YES" : "NO";
                string ancTrackIdStr = _phase1Controller.AnchorTrackableId.ToString();
                string ancTrackStateStr = _phase1Controller.AnchorTrackingState.ToString();

                string tempAncStr = _phase1Controller.HasRegistrationAnchor ? "FOUND" : "NO";
                string tempAncType = _phase1Controller.RegistrationAnchorType;
                string tempAncTrack = _phase1Controller.RegistrationAnchorTracking;

                GUILayout.Label($"\nREGISTRATION\n  Method: FOUR CORNER\n  State: {regStateStr}\n  Captured Corners: {_phase1Controller.CapturedCornerCount}/4\n  Corner Marker Parent: {_phase1Controller.CornerMarkerParentName}", s);
                GUILayout.Label($"  Registration Reference: {tempAncStr}\n  Registration Reference Type: {tempAncType}\n  Registration Reference Tracking: {tempAncTrack}", s);
                
                GUILayout.Label($"\nCAD GEOMETRY\n  Width: 82 mm\n  Depth: 54 mm\n  Scale: 1,1,1", s);

                if (_phase1Controller.CurrentState == PlacementState.FOUR_CORNER_REVIEW)
                {
                    var activePts = _phase1Controller.ActiveCornerPoints;
                    float wTop = Vector3.Distance(activePts[0], activePts[1]) * 1000f; // TL -> TR
                    float wBot = Vector3.Distance(activePts[3], activePts[2]) * 1000f; // BL -> BR
                    float dLft = Vector3.Distance(activePts[0], activePts[3]) * 1000f; // TL -> BL
                    float dRgt = Vector3.Distance(activePts[1], activePts[2]) * 1000f; // TR -> BR
                    
                    float avgW = (wTop + wBot) / 2f;
                    float avgD = (dLft + dRgt) / 2f;
                    
                    float wErr = (avgW - 82f) / 82f * 100f;
                    float dErr = (avgD - 54f) / 54f * 100f;
                    
                    GUILayout.Label($"\nMEASURED CORNERS\n  TL → TR (Width Top): {wTop:F1} mm\n  BL → BR (Width Bottom): {wBot:F1} mm\n  TL → BL (Depth Left): {dLft:F1} mm\n  TR → BR (Depth Right): {dRgt:F1} mm", s);
                    GUILayout.Label($"  Avg Width: {avgW:F1} mm (Err: {wErr:F1}%)\n  Avg Depth: {avgD:F1} mm (Err: {dErr:F1}%)", s);
                    GUILayout.Label($"  CAD Width: 82.0 mm\n  CAD Depth: 54.0 mm", s);

                    GUILayout.Label($"\nFOUR-CORNER REVIEW\n  Review Position: {_phase1Controller.FourCornerReviewPosition:F3}\n  Review Rotation: {_phase1Controller.FourCornerReviewRotation:F1}\n\n  Candidate Pose: {candPosStr}\n  Preview World Position: {_phase1Controller.PreviewWorldPosition:F3}\n  Preview World Rotation: {_phase1Controller.PreviewWorldRotation:F1}", s);
                }
                else
                {
                    GUILayout.Label($"\nREGISTERED POSE\n  World Position: {wp}\n  World Rotation: {_phase1Controller.PreviewWorldRotation:F1}", s);
                }

                GUILayout.Label($"\nCORNER POINTS\n  P1 (TL): {p1}\n  P2 (TR): {p2}\n  P3 (BR): {p3}\n  P4 (BL): {p4}", s);

                GUILayout.Label($"\nA3R_MainAnchor\n  Provider: {ancProvStr}\n  TrackableId: {ancTrackIdStr}\n  Tracking: {ancTrackStateStr}", s);
                
                GUILayout.Label($"\nPREVIEW MESH\n  Preview Root Parent: {_phase1Controller.PreviewParentName}\n  Instance ID: {_phase1Controller.PreviewVisualInstanceId}\n  Parent: {_phase1Controller.PreviewVisualParentName}\n  MeshFilter: {(_phase1Controller.HasPreviewMeshFilter ? "FOUND" : "MISSING")}\n  MeshRenderer: {(_phase1Controller.HasPreviewMeshRenderer ? "FOUND" : "MISSING")}\n  Vertices: {_phase1Controller.PreviewVertexCount}\n  Triangles: {_phase1Controller.PreviewTriangleCount}\n  Mesh Bounds: {_phase1Controller.PreviewMeshBoundsSize.x:F3}m x {_phase1Controller.PreviewMeshBoundsSize.z:F3}m", s);

                GUILayout.Label($"\nBREADBOARD ROOT\n  Parent: {_phase1Controller.BreadboardRootParentName}\n  World Pos: {(_phase1Controller.HasPlacedRoot ? _phase1Controller.RootPosition.ToString("F3") : "(NONE)")}\n  Locked: {_phase1Controller.IsRootLocked}", s);

                GUILayout.Label("\nBREADBOARD MAP\n  Grid: 30 Rows x 10 Cols (300 Term + 100 Rail = 400 Holes)\n  Pitch: 2.54mm | Center Gap: 7.62mm\n  Status: CAD-VERIFIED (Pololu 400-Point)", s);

                // Phase 4 Tutorial
                var resistorTut = A3R.ARGuidance.A3R_ResistorTutorialController.Instance
                    ?? FindFirstObjectByType<A3R.ARGuidance.A3R_ResistorTutorialController>();
                if (resistorTut != null)
                {
                    GUILayout.Label($"\nPHASE 4 TUTORIAL\n  Step: {resistorTut.StepName}\n  State: {resistorTut.StateString}\n  Target Active: {(resistorTut.IsTargetActive ? "YES" : "NO")}", s);
                    
                    if (resistorTut.IsTargetActive)
                    {
                        var t1Local = A3R.ARGuidance.BreadboardCoordinateSystem.ResolveLogicalName("resistor_leg1");
                        var t2Local = A3R.ARGuidance.BreadboardCoordinateSystem.ResolveLogicalName("resistor_leg2");
                        
                        var root = _phase1Controller.BreadboardRootTransform;
                        Vector3 t1World = root != null ? root.TransformPoint(t1Local) : Vector3.zero;
                        Vector3 t2World = root != null ? root.TransformPoint(t2Local) : Vector3.zero;

                        GUILayout.Label($"\nPHASE 4\n\nComponent: RESISTOR\n\nTerminal 1:\nRow = 15\nColumn = D\nLocal = ({t1Local.x:F4}, {t1Local.y:F4}, {t1Local.z:F4})\nWorld = ({t1World.x:F4}, {t1World.y:F4}, {t1World.z:F4})\n\nTerminal 2:\nRow = 20\nColumn = D\nLocal = ({t2Local.x:F4}, {t2Local.y:F4}, {t2Local.z:F4})\nWorld = ({t2World.x:F4}, {t2World.y:F4}, {t2World.z:F4})\n\nBoard Root:\nA3R_MainAnchor\n\nBoard Scale:\n1,1,1", s);
                    }
                }
            }
            else
            {
                GUILayout.Label("\nPLACEMENT\n  Controller: NULL", s);
            }

            GUILayout.Label($"\nRENDERING\n  FPS: {_fps:F1}\n  Frame Time: {_frameTime:F1}ms", s);

            GUILayout.EndArea();
        }
    }
}
