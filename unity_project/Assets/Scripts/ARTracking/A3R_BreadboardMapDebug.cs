using System.Collections.Generic;
using UnityEngine;

namespace A3R.ARTracking
{
    /// <summary>
    /// A3R_BreadboardMapDebug — Lightweight procedural visualization of A3R_BreadboardMap.
    /// Operates purely in LOCAL space as a child of BreadboardARRoot / CalibratedBoardFrame.
    /// Renders all 400 tie points, the center gap, and power rails in a SINGLE combined mesh
    /// (1 draw call, 0 per-frame overhead).
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class A3R_BreadboardMapDebug : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [Tooltip("Toggle the visibility of the debug hole grid.")]
        [SerializeField] private bool _visible = true;

        [Tooltip("Size of each hole quad in meters (default 0.8 mm).")]
        [SerializeField] private float _holeMarkerSize = 0.0008f;

        [Tooltip("Elevation above breadboard surface to avoid Z-fighting.")]
        [SerializeField] private float _elevationOffset = 0.0001f;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _combinedMesh;

        public bool IsVisible => _visible;

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            SetupMaterial();
            BuildProceduralGrid();
        }

        private void SetupMaterial()
        {
            if (_meshRenderer == null) return;

            // Use vertex colors for efficient single-pass rendering of all colors
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader)
            {
                name = "A3R_BreadboardMapDebug_Mat"
            };
            mat.SetColor("_Color", Color.white);

            _meshRenderer.material = mat;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _meshRenderer.enabled = _visible;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_meshRenderer != null)
                _meshRenderer.enabled = visible;
        }

        /// <summary>
        /// Generates a single combined procedural mesh representing all 400 tie points,
        /// colored by role (Terminal: Light Gray/Cyan, Positive: Red, Negative: Blue, Center Divider: Dark Gray).
        /// </summary>
        public void BuildProceduralGrid()
        {
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();

            if (_combinedMesh != null)
            {
                Destroy(_combinedMesh);
            }

            _combinedMesh = new Mesh
            {
                name = "A3R_BreadboardMap_CombinedGrid"
            };

            List<Vector3> vertices = new List<Vector3>(1600);
            List<int> triangles = new List<int>(2400);
            List<Color> colors = new List<Color>(1600);

            float halfSize = _holeMarkerSize * 0.5f;
            Color terminalColor = new Color(0.2f, 0.85f, 0.95f, 0.9f); // Soft Cyan
            Color vccColor = new Color(0.95f, 0.2f, 0.2f, 0.95f);       // Red
            Color gndColor = new Color(0.2f, 0.4f, 0.95f, 0.95f);       // Blue

            // 1. Generate quads for each hole
            foreach (var kvp in A3R_BreadboardMap.GetAllHolePositions())
            {
                string id = kvp.Key;
                Vector3 pos = kvp.Value;
                pos.y += _elevationOffset;

                Color c = terminalColor;
                if (id.Contains("+")) c = vccColor;
                else if (id.Contains("-")) c = gndColor;

                int baseIndex = vertices.Count;

                // Quad on X-Z plane
                vertices.Add(new Vector3(pos.x - halfSize, pos.y, pos.z - halfSize));
                vertices.Add(new Vector3(pos.x - halfSize, pos.y, pos.z + halfSize));
                vertices.Add(new Vector3(pos.x + halfSize, pos.y, pos.z + halfSize));
                vertices.Add(new Vector3(pos.x + halfSize, pos.y, pos.z - halfSize));

                colors.Add(c);
                colors.Add(c);
                colors.Add(c);
                colors.Add(c);

                triangles.Add(baseIndex + 0);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 0);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }

            // 2. Add Center Divider trough visualization (along X axis at Z = 0)
            float troughHalfLen = 15f * A3R_BreadboardMap.TIE_POINT_PITCH; // covers 30 rows
            float troughHalfW = (A3R_BreadboardMap.CENTER_GAP * 0.5f) - halfSize;
            float troughY = A3R_BreadboardConfig.HEIGHT + _elevationOffset * 0.5f;
            Color troughColor = new Color(0.15f, 0.15f, 0.15f, 0.75f); // Dark trough

            int troughBase = vertices.Count;
            vertices.Add(new Vector3(-troughHalfLen, troughY, -troughHalfW));
            vertices.Add(new Vector3(-troughHalfLen, troughY, +troughHalfW));
            vertices.Add(new Vector3(+troughHalfLen, troughY, +troughHalfW));
            vertices.Add(new Vector3(+troughHalfLen, troughY, -troughHalfW));

            colors.Add(troughColor);
            colors.Add(troughColor);
            colors.Add(troughColor);
            colors.Add(troughColor);

            triangles.Add(troughBase + 0);
            triangles.Add(troughBase + 1);
            triangles.Add(troughBase + 2);
            triangles.Add(troughBase + 0);
            triangles.Add(troughBase + 2);
            triangles.Add(troughBase + 3);

            _combinedMesh.SetVertices(vertices);
            _combinedMesh.SetTriangles(triangles, 0);
            _combinedMesh.SetColors(colors);
            _combinedMesh.RecalculateNormals();
            _combinedMesh.RecalculateBounds();

            _meshFilter.sharedMesh = _combinedMesh;
        }

        private void OnDestroy()
        {
            if (_combinedMesh != null)
            {
                Destroy(_combinedMesh);
                _combinedMesh = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Lightweight Gizmo in Scene view when selected
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;

            foreach (var kvp in A3R_BreadboardMap.GetAllHolePositions())
            {
                Gizmos.DrawWireCube(kvp.Value, new Vector3(0.001f, 0.0002f, 0.001f));
            }
        }
    }
}
