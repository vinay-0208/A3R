using UnityEngine;
using System.Collections.Generic;
using A3R.ContentManager;

namespace A3R.ARGuidance
{
    public class ESP32CircuitGuidance : MonoBehaviour
    {
        private ARContentManager _contentManager;
        private Transform _breadboardRoot;

        // Cumulative Visuals
        private GameObject _breadboardFootprint;
        private GameObject _ledGhost;
        private GameObject _resistorGhost;
        private GameObject _esp32Ghost;
        
        // Active Step Visuals
        private GameObject _holeHighlightPrefab;
        private List<GameObject> _activeHoleHighlights = new List<GameObject>();
        
        private List<LineRenderer> _allWires = new List<LineRenderer>();

        private Material CreateTransparentMaterial(Color color)
        {
            return ProceduralMeshUtility.CreateMaterial(color, true, false);
        }

        private void Awake()
        {
            _contentManager = GetComponent<ARContentManager>() ?? FindFirstObjectByType<ARContentManager>();
            // Do NOT create component ghosts during Awake in Phase 1 (no breadboard root exists yet).
            // Ghosts are created on demand when a valid breadboard root is assigned.
        }

        private void Start()
        {
            if (_contentManager != null)
            {
                _contentManager.OnStepChanged += OnStepChanged;
                _contentManager.OnTaskComplete += OnTaskComplete;
            }
        }

        private void OnDestroy()
        {
            if (_contentManager != null)
            {
                _contentManager.OnStepChanged -= OnStepChanged;
                _contentManager.OnTaskComplete -= OnTaskComplete;
            }
        }

        public void SetBreadboardRoot(Transform root)
        {
            _breadboardRoot = root;
            if (root == null)
            {
                ClearActiveHighlights();
                return;
            }

            if (_breadboardFootprint == null)
            {
                CreateComponentGhosts();
            }

            if (_ledGhost != null) _ledGhost.transform.SetParent(root, false);
            if (_resistorGhost != null) _resistorGhost.transform.SetParent(root, false);
            if (_esp32Ghost != null) _esp32Ghost.transform.SetParent(root, false);
            if (_breadboardFootprint != null) _breadboardFootprint.transform.SetParent(root, false);
            
            foreach (var wire in _allWires) wire.transform.SetParent(root, false);
        }

        private void CreateComponentGhosts()
        {
            // Breadboard Footprint
            _breadboardFootprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _breadboardFootprint.SetActive(false);
            Destroy(_breadboardFootprint.GetComponent<Collider>());
            _breadboardFootprint.transform.localScale = new Vector3(0.085f, 0.001f, 0.055f); // 85mm x 55mm
            _breadboardFootprint.GetComponent<MeshRenderer>().material = CreateTransparentMaterial(new Color(0.2f, 0.8f, 1.0f, 0.3f));

            // LED
            _ledGhost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _ledGhost.SetActive(false);
            Destroy(_ledGhost.GetComponent<Collider>());
            _ledGhost.transform.localScale = new Vector3(0.005f, 0.004f, 0.005f); // 5mm radius
            _ledGhost.GetComponent<MeshRenderer>().material = CreateTransparentMaterial(new Color(1f, 0f, 0f, 0.6f));

            // Resistor
            _resistorGhost = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _resistorGhost.SetActive(false);
            Destroy(_resistorGhost.GetComponent<Collider>());
            _resistorGhost.transform.localScale = new Vector3(0.002f, 0.005f, 0.002f);
            _resistorGhost.transform.localRotation = Quaternion.Euler(0, 0, 90);
            _resistorGhost.GetComponent<MeshRenderer>().material = CreateTransparentMaterial(new Color(0.9f, 0.8f, 0.5f, 0.8f));

            // ESP32
            _esp32Ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _esp32Ghost.SetActive(false);
            Destroy(_esp32Ghost.GetComponent<Collider>());
            _esp32Ghost.transform.localScale = new Vector3(0.025f, 0.003f, 0.05f); // 25mm x 50mm
            _esp32Ghost.GetComponent<MeshRenderer>().material = CreateTransparentMaterial(new Color(0.1f, 0.1f, 0.1f, 0.7f));
            
            // Highlight Ring Prefab
            _holeHighlightPrefab = new GameObject("HoleHighlight");
            _holeHighlightPrefab.SetActive(false);
            var mf = _holeHighlightPrefab.AddComponent<MeshFilter>();
            mf.sharedMesh = ProceduralMeshUtility.CreateRingMesh(0.0015f, 0.0025f, 16); // 2.5mm radius
            var mr = _holeHighlightPrefab.AddComponent<MeshRenderer>();
            mr.material = ProceduralMeshUtility.CreateMaterial(new Color(0.0f, 1f, 0.8f, 1.0f), false, false);
        }

        private LineRenderer CreateWire()
        {
            GameObject wireObj = new GameObject("A3R_JumperWire");
            if (_breadboardRoot != null) wireObj.transform.SetParent(_breadboardRoot, false);
            else wireObj.transform.SetParent(transform, false);
            
            LineRenderer lr = wireObj.AddComponent<LineRenderer>();
            lr.startWidth = 0.0015f; // 1.5mm thick wire
            lr.endWidth = 0.0015f;
            lr.positionCount = 0;
            lr.useWorldSpace = false;
            lr.material = new Material(Shader.Find("Unlit/Color"));
            _allWires.Add(lr);
            return lr;
        }

        private void ClearActiveHighlights()
        {
            foreach (var h in _activeHoleHighlights)
            {
                if (h != null) Destroy(h);
            }
            _activeHoleHighlights.Clear();
            
            foreach (var wire in _allWires)
            {
                if (wire != null) Destroy(wire.gameObject);
            }
            _allWires.Clear();
            
            if (_breadboardFootprint != null) _breadboardFootprint.SetActive(false);
            if (_ledGhost != null) _ledGhost.SetActive(false);
            if (_resistorGhost != null) _resistorGhost.SetActive(false);
            if (_esp32Ghost != null) _esp32Ghost.SetActive(false);
        }

        private void HighlightHoles(Color color, params Vector3[] localPositions)
        {
            foreach (var pos in localPositions)
            {
                var go = Instantiate(_holeHighlightPrefab, _breadboardRoot);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.identity;
                go.GetComponent<MeshRenderer>().material.color = color;
                go.SetActive(true);
                _activeHoleHighlights.Add(go);
            }
        }

        private void DrawWire(Vector3 start, Vector3 end, Color color)
        {
            LineRenderer lr = CreateWire();
            lr.positionCount = 4;
            lr.SetPosition(0, start);
            lr.SetPosition(1, start + Vector3.up * 0.015f);
            lr.SetPosition(2, end + Vector3.up * 0.015f);
            lr.SetPosition(3, end);
            lr.material.color = color;
        }

        private void OnStepChanged(int index, InstructionStep step)
        {
            if (_breadboardRoot == null) return;
            ClearActiveHighlights();
            
            // Phase 3: BreadboardARRoot already renders the authoritative 0.082m x 0.054m outline.
            // Do NOT activate duplicate _breadboardFootprint.
            if (_breadboardFootprint != null)
            {
                _breadboardFootprint.SetActive(false);
            }
        }

        private void OnTaskComplete()
        {
            ClearActiveHighlights();
        }
    }
}

