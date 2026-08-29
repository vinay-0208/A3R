using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace A3R.ARTracking
{
    /// <summary>
    /// ARImageTracker — Module 1 of the A³R system.
    ///
    /// Wraps AR Foundation's ARTrackedImageManager to detect and track the printed
    /// reference image (A3R_ReferenceImageLibrary). When the image is detected, a
    /// content root GameObject is instantiated and parented to the tracked image's
    /// transform, anchoring all AR instructional overlays to the physical marker.
    ///
    /// All AR content (step overlays, 3D models, arrows) must be children of the
    /// ContentRoot transform to remain correctly anchored as the marker moves.
    /// </summary>
    [RequireComponent(typeof(ARTrackedImageManager))]
    public class ARImageTracker : MonoBehaviour
    {
        // ── Inspector References ────────────────────────────────────────────────
        [Header("Content Root")]
        [Tooltip("Prefab instantiated once when the reference image is first detected. " +
                 "All AR content should be parented to this object's transform.")]
        [SerializeField] private GameObject contentRootPrefab;

        [Header("Tracking Settings")]
        [Tooltip("Reference image name as defined in the XRReferenceImageLibrary asset.")]
        [SerializeField] private string referenceImageName = "A3R_Marker";

        // ── Events ───────────────────────────────────────────────────────────────
        /// <summary>Fired when the reference image is first detected. Arg: content root Transform.</summary>
        public event Action<Transform> OnMarkerDetected;

        /// <summary>Fired when the tracked image pose updates. Arg: updated content root Transform.</summary>
        public event Action<Transform> OnMarkerUpdated;

        /// <summary>Fired when tracking is lost (marker out of view).</summary>
        public event Action OnMarkerLost;

        // ── Private State ────────────────────────────────────────────────────────
        private ARTrackedImageManager _trackedImageManager;
        private GameObject            _contentRoot;
        private bool                  _isTracking;

        // ── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            _trackedImageManager = GetComponent<ARTrackedImageManager>();
        }

        private void OnEnable()
        {
            _trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        }

        private void OnDisable()
        {
            _trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        }

        // ── Public API ───────────────────────────────────────────────────────────
        /// <summary>Returns true if the reference marker is currently being tracked.</summary>
        public bool IsTracking => _isTracking;

        /// <summary>Returns the content root transform, or null if not yet detected.</summary>
        public Transform ContentRoot => _contentRoot != null ? _contentRoot.transform : null;

        // ── ARTrackedImageManager Callback ───────────────────────────────────────
        private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
        {
            // Newly detected images
            foreach (var trackedImage in args.added)
            {
                if (IsOurMarker(trackedImage))
                    HandleMarkerDetected(trackedImage);
            }

            // Updated poses
            foreach (var trackedImage in args.updated)
            {
                if (IsOurMarker(trackedImage))
                    HandleMarkerUpdated(trackedImage);
            }

            // Removed / lost tracking
            foreach (var trackedImage in args.removed)
            {
                if (IsOurMarker(trackedImage))
                    HandleMarkerLost();
            }
        }

        // ── Tracking State Handlers ──────────────────────────────────────────────
        private void HandleMarkerDetected(ARTrackedImage trackedImage)
        {
            Debug.Log($"[ARImageTracker] Reference marker detected: {trackedImage.referenceImage.name}");

            if (_contentRoot == null)
            {
                // Instantiate content root parented to the tracked image's transform
                if (contentRootPrefab != null)
                {
                    _contentRoot = Instantiate(contentRootPrefab,
                                               trackedImage.transform.position,
                                               trackedImage.transform.rotation,
                                               trackedImage.transform);
                    _contentRoot.name = "A3R_ContentRoot";
                }
                else
                {
                    // Fallback: empty GameObject
                    _contentRoot = new GameObject("A3R_ContentRoot");
                    _contentRoot.transform.SetParent(trackedImage.transform, false);
                }
            }

            _contentRoot.transform.SetPositionAndRotation(
                trackedImage.transform.position,
                trackedImage.transform.rotation);
            _contentRoot.SetActive(true);
            _isTracking = true;

            OnMarkerDetected?.Invoke(_contentRoot.transform);
        }

        private void HandleMarkerUpdated(ARTrackedImage trackedImage)
        {
            if (_contentRoot == null) { HandleMarkerDetected(trackedImage); return; }

            bool wasTracking = _isTracking;

            switch (trackedImage.trackingState)
            {
                case TrackingState.Tracking:
                    _contentRoot.SetActive(true);
                    _isTracking = true;
                    OnMarkerUpdated?.Invoke(_contentRoot.transform);
                    break;

                case TrackingState.Limited:
                    // Still show content but flag as limited quality
                    _contentRoot.SetActive(true);
                    _isTracking = true;
                    OnMarkerUpdated?.Invoke(_contentRoot.transform);
                    Debug.LogWarning("[ARImageTracker] Tracking state: Limited — marker may be partially occluded.");
                    break;

                case TrackingState.None:
                    _contentRoot.SetActive(false);
                    if (wasTracking)
                    {
                        _isTracking = false;
                        OnMarkerLost?.Invoke();
                        Debug.Log("[ARImageTracker] Tracking lost — content hidden until marker re-detected.");
                    }
                    break;
            }
        }

        private void HandleMarkerLost()
        {
            if (_contentRoot != null)
                _contentRoot.SetActive(false);

            if (_isTracking)
            {
                _isTracking = false;
                OnMarkerLost?.Invoke();
                Debug.Log("[ARImageTracker] Marker removed from tracked images list.");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        private bool IsOurMarker(ARTrackedImage trackedImage)
        {
            return trackedImage.referenceImage.name == referenceImageName;
        }
    }
}
