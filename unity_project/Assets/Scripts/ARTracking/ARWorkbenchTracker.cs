using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using A3R.ARGuidance;

namespace A3R.ARTracking
{
    public class ARWorkbenchTracker : MonoBehaviour
    {
        [Header("Content Root")]
        [SerializeField] private GameObject contentRootPrefab;
        
        [Header("Settings")]
        public bool enableMarkerless = false;
        public bool requireTapToPlace = true; // FORCE tap to place
        private bool _hasPlaced = false;

        public event Action<Transform> OnWorkbenchDetected;
        public event Action<Transform> OnWorkbenchUpdated;
        public event Action OnWorkbenchLost;

        private ARPlaneManager _planeManager;
        private ARRaycastManager _raycastManager;
        private ARAnchorManager _anchorManager;
        private Camera _arCamera;
        
        private GameObject _contentRoot;
        private ARAnchor _currentAnchor;
        private bool _isTracking = false;
        
        private GameObject _previewFootprint;

        private static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

        public bool IsTracking => _isTracking;
        public Transform ContentRoot => _contentRoot != null ? _contentRoot.transform : null;

        private void Awake()
        {
            // Fully decommission in Phase 2: A3R_MarkerlessFoundationController is authoritative
            if (FindFirstObjectByType<A3R_MarkerlessFoundationController>() != null || !enableMarkerless)
            {
                enableMarkerless = false;
                this.enabled = false;
                if (_previewFootprint != null) { Destroy(_previewFootprint); _previewFootprint = null; }
                return;
            }

            if (!this.enabled) return; // Prevent creating stray debug objects if disabled in scene
            
            _arCamera = Camera.main;
            if (!enableMarkerless) return;
            
            var origin = FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null)
            {
                _planeManager = origin.GetComponent<ARPlaneManager>();
                if (_planeManager == null) _planeManager = origin.gameObject.AddComponent<ARPlaneManager>();
                
                _planeManager.enabled = false;
                _planeManager.requestedDetectionMode = UnityEngine.XR.ARSubsystems.PlaneDetectionMode.Horizontal;
                _planeManager.enabled = true;

                _raycastManager = origin.GetComponent<ARRaycastManager>();
                if (_raycastManager == null) _raycastManager = origin.gameObject.AddComponent<ARRaycastManager>();

                _anchorManager = origin.GetComponent<ARAnchorManager>();
                if (_anchorManager == null) _anchorManager = origin.gameObject.AddComponent<ARAnchorManager>();
            }
            
            // Create preview footprint
            _previewFootprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _previewFootprint.SetActive(false);
            Destroy(_previewFootprint.GetComponent<Collider>());
            _previewFootprint.transform.localScale = new Vector3(0.085f, 0.001f, 0.055f); // 85mm x 55mm
            var mat = ProceduralMeshUtility.CreateMaterial(new Color(0, 1, 1, 0.4f), true, false);
            _previewFootprint.GetComponent<MeshRenderer>().material = mat;
        }

        public string diag_RaycastResult = "N/A";
        public string diag_SelectedPlaneId = "N/A";
        public string diag_AnchorAttempt = "N/A";
        public string diag_AnchorResult = "N/A";
        public string diag_FailureReason = "N/A";
        public string diag_PlaneAlignment = "N/A";
        public string diag_PlaneArea = "N/A";
        public string diag_PlaneCenter = "N/A";
        public string diag_AnchorTrackingState = "N/A";

        private void Update()
        {
            if (!enableMarkerless) return;
            
            if (_isTracking && _currentAnchor != null)
            {
                if (_currentAnchor.trackingState != TrackingState.None)
                {
                    _contentRoot.transform.SetPositionAndRotation(_currentAnchor.transform.position, _currentAnchor.transform.rotation);
                    OnWorkbenchUpdated?.Invoke(_contentRoot.transform);
                }
                else
                {
                    HandleLost();
                }
                return;
            }

            if (_planeManager == null || _raycastManager == null) return;

            if (!_hasPlaced)
            {
                // Raycast from center of screen
                Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                if (_raycastManager.Raycast(screenCenter, s_Hits, TrackableType.PlaneWithinPolygon))
                {
                    var hit = s_Hits[0];
                    Vector3 camPos = _arCamera != null ? _arCamera.transform.position : Vector3.zero;
                    float dist = Vector3.Distance(hit.pose.position, camPos);
                    
                    // Reject invalid distances
                    if (dist > 2.0f || dist < 0.1f)
                    {
                        diag_RaycastResult = $"Rejected dist: {dist:F2}m";
                        _previewFootprint.SetActive(false);
                    }
                    else
                    {
                        diag_RaycastResult = "Valid Plane Target";
                        _previewFootprint.SetActive(true);
                        _previewFootprint.transform.SetPositionAndRotation(hit.pose.position, hit.pose.rotation);
                        
                        // Wait for tap to place
                        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
                        {
                            PlaceAnchor(hit.pose, hit.trackable as ARPlane);
                        }
                    }
                }
                else
                {
                    diag_RaycastResult = "Point at a horizontal surface...";
                    _previewFootprint.SetActive(false);
                }
            }
        }

        private void PlaceAnchor(Pose pose, ARPlane plane)
        {
            if (_anchorManager == null) return;
            diag_AnchorAttempt = $"Attempting at {pose.position}";
            ARAnchor anchor = null;
            
            if (plane != null) anchor = _anchorManager.AttachAnchor(plane, pose);
            if (anchor == null)
            {
                GameObject anchorGO = new GameObject("A3R_ARAnchor_Fallback");
                anchorGO.transform.SetPositionAndRotation(pose.position, pose.rotation);
                anchor = anchorGO.AddComponent<ARAnchor>();
            }

            if (anchor != null)
            {
                _currentAnchor = anchor;
                _isTracking = true;
                _hasPlaced = true;
                diag_AnchorResult = "Success";
                
                // Hide preview, flash success briefly
                _previewFootprint.SetActive(false);

                if (_contentRoot == null)
                {
                    if (contentRootPrefab != null) _contentRoot = Instantiate(contentRootPrefab);
                    else _contentRoot = new GameObject("A3R_ContentRoot");
                    DontDestroyOnLoad(_contentRoot);
                }
                
                _contentRoot.transform.SetPositionAndRotation(pose.position, pose.rotation);
                
                // Trigger OnMarkerDetected so the guidance system starts
                OnWorkbenchDetected?.Invoke(_contentRoot.transform);
            }
            else
            {
                diag_AnchorResult = "Failed to create ARAnchor";
            }
        }

        private void HandleLost()
        {
            _isTracking = false;
            OnWorkbenchLost?.Invoke();
        }
    }
}


