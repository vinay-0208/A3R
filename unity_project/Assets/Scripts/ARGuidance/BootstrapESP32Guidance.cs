using UnityEngine;
using A3R.ARTracking;

namespace A3R.ARGuidance
{
    public class BootstrapESP32Guidance : MonoBehaviour
    {
        private ARWorkbenchTracker _tracker;
        private ESP32CircuitGuidance _espGuidance;

        private void Start()
        {
            _tracker = FindFirstObjectByType<ARWorkbenchTracker>();
            
            // Add the ESP32 guidance component to the scene
            GameObject espObj = new GameObject("ESP32CircuitGuidance");
            _espGuidance = espObj.AddComponent<ESP32CircuitGuidance>();
        }

        private void Update()
        {
            if (_tracker != null && _tracker.IsTracking && _tracker.ContentRoot != null)
            {
                // Assign the actual breadboard root once it exists
                _espGuidance.SetBreadboardRoot(_tracker.ContentRoot);
                Destroy(this); // Only need to do this once
            }
        }
    }
}
