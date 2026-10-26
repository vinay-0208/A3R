using UnityEngine;

namespace A3R.ARGuidance
{
    /// <summary>
    /// A3R_DirectionalArrow — Primary directional guidance controller.
    /// Drives the modern A3R_ChevronPath navigation system (multi-chevron directional path)
    /// while maintaining 100% backward compatibility with ARGuidanceController and preview tests.
    /// Completely supersedes the old single primitive downward arrow.
    /// </summary>
    public class A3R_DirectionalArrow : MonoBehaviour
    {
        [Header("Chevron Path System")]
        [SerializeField] private A3R_ChevronPath chevronPath;

        [Header("Emissive Palette")]
        private static readonly Color ColorNormal = new Color(0.0f, 0.90f, 1.0f, 0.85f);
        private static readonly Color ColorCue = new Color(0.25f, 1.0f, 1.0f, 0.95f);
        private static readonly Color ColorHint = new Color(0.70f, 0.28f, 1.0f, 0.85f);
        private static readonly Color ColorSlow = new Color(0.16f, 0.47f, 1.0f, 0.85f);
        private static readonly Color ColorSimplify = new Color(1.0f, 0.57f, 0.0f, 0.85f);

        [SerializeField] private Color arrowColor = ColorNormal;

        private Transform _targetTransform;
        private Vector3 _targetOffset = Vector3.zero;
        private bool _isTrackingValid = true;
        private GuidanceVisualState _visualState = GuidanceVisualState.Normal;

        // Public Accessors matching legacy interface and exposing chevron system
        public A3R_ChevronPath ChevronPath => chevronPath;
        public int ChevronCount => chevronPath != null ? chevronPath.ChevronCount : 6;
        public GameObject[] Chevrons => chevronPath != null ? chevronPath.Chevrons : null;
        public GuidanceVisualState CurrentState => _visualState;
        public bool IsVisible => gameObject.activeInHierarchy && _isTrackingValid && _visualState != GuidanceVisualState.Hidden;
        public Vector3 PointingDirection => chevronPath != null ? chevronPath.PointingDirection : transform.forward;
        public Color CurrentColor => arrowColor;

        private void OnDisable()
        {
            if (chevronPath != null)
            {
                chevronPath.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (chevronPath != null && _isTrackingValid && _visualState != GuidanceVisualState.Hidden)
            {
                chevronPath.gameObject.SetActive(true);
            }
        }

        private void Awake()
        {
            InitializeVisuals();
        }

        private void InitializeVisuals()
        {
            // Ensure any old root renderer is disabled to prevent duplicate/primitive arrow rendering
            MeshRenderer rootMr = GetComponent<MeshRenderer>();
            if (rootMr != null)
            {
                rootMr.enabled = false;
            }

            // Ensure child A3R_ChevronPath component exists
            if (chevronPath == null)
            {
                chevronPath = GetComponentInChildren<A3R_ChevronPath>();
            }

            if (chevronPath == null)
            {
                GameObject pathObj = new GameObject("ModernChevronPath");
                pathObj.transform.SetParent(transform, false);
                chevronPath = pathObj.AddComponent<A3R_ChevronPath>();
            }

            chevronPath.InitializeChevrons();
            SetVisualState(_visualState);
        }

        public void SetTarget(Transform target, Vector3 offset, Color color)
        {
            _targetTransform = target;
            _targetOffset = offset;

            if (_visualState == GuidanceVisualState.Normal)
            {
                arrowColor = ColorNormal;
            }

            if (chevronPath == null)
            {
                InitializeVisuals();
            }

            if (chevronPath != null)
            {
                chevronPath.SetTarget(target, offset, arrowColor);
            }

            if (target != null)
            {
                gameObject.SetActive(_isTrackingValid && _visualState != GuidanceVisualState.Hidden);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void SetVisualState(GuidanceVisualState state)
        {
            _visualState = state;

            switch (state)
            {
                case GuidanceVisualState.Hidden:
                    gameObject.SetActive(false);
                    if (chevronPath != null) chevronPath.SetVisualState(GuidanceVisualState.Hidden);
                    return;

                case GuidanceVisualState.Normal:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    arrowColor = ColorNormal;
                    break;

                case GuidanceVisualState.ProminentCue:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    arrowColor = ColorCue;
                    break;

                case GuidanceVisualState.HintEmphasized:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    arrowColor = ColorHint;
                    break;

                case GuidanceVisualState.PacedSlow:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    arrowColor = ColorSlow;
                    break;

                case GuidanceVisualState.IsolatedStep:
                    gameObject.SetActive(_isTrackingValid && _targetTransform != null);
                    arrowColor = ColorSimplify;
                    break;
            }

            if (chevronPath != null)
            {
                chevronPath.SetVisualState(state);
            }
        }

        public void SetTrackingState(bool isTracking)
        {
            _isTrackingValid = isTracking;
            if (chevronPath != null)
            {
                chevronPath.SetTrackingState(isTracking);
            }

            if (!isTracking)
            {
                gameObject.SetActive(false);
            }
            else if (_targetTransform != null && _visualState != GuidanceVisualState.Hidden)
            {
                gameObject.SetActive(true);
            }
        }
    }
}
