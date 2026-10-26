using UnityEngine;

namespace A3R.ARTracking
{
    /// <summary>
    /// Represents the authoritative local coordinate bounds of the physical breadboard.
    /// Operates purely in local space relative to the BreadboardARRoot.
    /// </summary>
    public class A3R_BreadboardBounds : MonoBehaviour
    {
        [SerializeField] private float _customWidth = -1f;
        [SerializeField] private float _customDepth = -1f;
        [SerializeField] private float _customHeight = -1f;

        public float Width => _customWidth > 0f ? _customWidth : A3R_BreadboardConfig.WIDTH;
        public float Depth => _customDepth > 0f ? _customDepth : A3R_BreadboardConfig.DEPTH;
        public float Height => _customHeight > 0f ? _customHeight : A3R_BreadboardConfig.HEIGHT;

        public void SetDimensions(float width, float depth, float height = -1f)
        {
            _customWidth = width;
            _customDepth = depth;
            if (height > 0f) _customHeight = height;
        }

        // Local center is (0, 0, 0)
        public Vector3 GetLocalCenter() => Vector3.zero;

        public Vector3 GetLocalSize() => new Vector3(Width, Height, Depth);

        /// <summary>
        /// Checks if a local coordinate point is inside the breadboard bounds.
        /// (Local Y is 0 to Height, Local X/Z are +/- half size)
        /// </summary>
        public bool IsInsideBounds(Vector3 localPosition)
        {
            float halfW = Width * 0.5f;
            float halfD = Depth * 0.5f;

            return localPosition.x >= -halfW && localPosition.x <= halfW &&
                   localPosition.z >= -halfD && localPosition.z <= halfD &&
                   localPosition.y >= 0f && localPosition.y <= Height;
        }

        /// <summary>
        /// Clamps a local coordinate point to remain strictly inside the breadboard bounds.
        /// </summary>
        public Vector3 ClampToBounds(Vector3 localPosition)
        {
            float halfW = Width * 0.5f;
            float halfD = Depth * 0.5f;

            return new Vector3(
                Mathf.Clamp(localPosition.x, -halfW, halfW),
                Mathf.Clamp(localPosition.y, 0f, Height),
                Mathf.Clamp(localPosition.z, -halfD, halfD)
            );
        }
        
        /// <summary>
        /// Renders a simple Cyan wireframe box representing the bounds in the editor or for debug.
        /// </summary>
        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            // Center Y is half height so box goes from 0 to Height
            Gizmos.DrawWireCube(new Vector3(0, Height * 0.5f, 0), GetLocalSize());
        }
    }
}
