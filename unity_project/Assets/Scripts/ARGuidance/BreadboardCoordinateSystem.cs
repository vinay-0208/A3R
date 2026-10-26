using UnityEngine;
using A3R.ARTracking;

namespace A3R.ARGuidance
{
    /// <summary>
    /// BreadboardCoordinateSystem — High-level coordinate resolver for circuit components.
    /// Uses A3R_BreadboardMap (CAD-calibrated ground truth) as its authoritative backend.
    /// </summary>
    public static class BreadboardCoordinateSystem
    {
        // Approximate ESP32 board center, placed physically to the right of the breadboard
        private static readonly Vector3 ESP32_CENTER = new Vector3(0.08f, A3R_BreadboardConfig.HEIGHT, 0.0f);

        /// <summary>
        /// Gets the local position of a breadboard hole via row and column.
        /// Forwards directly to A3R_BreadboardMap (CAD-calibrated 2.54mm pitch & 7.62mm center gap).
        /// </summary>
        public static Vector3 GetRowColLocalPosition(int row, string col)
        {
            string holeId = $"{col}{row}";
            if (A3R_BreadboardMap.TryGetHolePosition(holeId, out Vector3 pos))
            {
                return pos;
            }

            // Fallback calculation if map lookup fails
            float x = (row - 15.5f) * A3R_BreadboardMap.TIE_POINT_PITCH;
            float z = 0f;
            switch (col.ToUpperInvariant())
            {
                case "A": z = -(A3R_BreadboardMap.CENTER_GAP * 0.5f + 4 * A3R_BreadboardMap.TIE_POINT_PITCH); break;
                case "B": z = -(A3R_BreadboardMap.CENTER_GAP * 0.5f + 3 * A3R_BreadboardMap.TIE_POINT_PITCH); break;
                case "C": z = -(A3R_BreadboardMap.CENTER_GAP * 0.5f + 2 * A3R_BreadboardMap.TIE_POINT_PITCH); break;
                case "D": z = -(A3R_BreadboardMap.CENTER_GAP * 0.5f + 1 * A3R_BreadboardMap.TIE_POINT_PITCH); break;
                case "E": z = -(A3R_BreadboardMap.CENTER_GAP * 0.5f); break;
                case "F": z = +(A3R_BreadboardMap.CENTER_GAP * 0.5f); break;
                case "G": z = +(A3R_BreadboardMap.CENTER_GAP * 0.5f + 1 * A3R_BreadboardMap.TIE_POINT_PITCH); break;
                case "H": z = +(A3R_BreadboardMap.CENTER_GAP * 0.5f + 2 * A3R_BreadboardMap.TIE_POINT_PITCH); break;
                case "I": z = +(A3R_BreadboardMap.CENTER_GAP * 0.5f + 3 * A3R_BreadboardMap.TIE_POINT_PITCH); break;
                case "J": z = +(A3R_BreadboardMap.CENTER_GAP * 0.5f + 4 * A3R_BreadboardMap.TIE_POINT_PITCH); break;
            }
            return new Vector3(x, A3R_BreadboardConfig.HEIGHT, z);
        }

        public static Vector3 GetESP32Pin(string pinName)
        {
            if (pinName == "GPIO25")
            {
                return ESP32_CENTER + new Vector3(-0.012f, 0f, 0.015f);
            }
            else if (pinName == "GND")
            {
                return ESP32_CENTER + new Vector3(-0.012f, 0f, -0.015f);
            }
            else if (pinName == "USB")
            {
                return ESP32_CENTER + new Vector3(0f, 0f, -0.025f);
            }
            
            return ESP32_CENTER;
        }

        public static Vector3 ResolveLogicalName(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "breadboard_center": return Vector3.zero;
                
                // LED is in Row 10 (Anode) and Row 11 (Cathode)
                case "led_anode": return GetRowColLocalPosition(10, "D");
                case "led_cathode": return GetRowColLocalPosition(11, "D");
                case "led_center": return (GetRowColLocalPosition(10, "D") + GetRowColLocalPosition(11, "D")) * 0.5f;

                // Resistor goes from Row 15 to Row 20
                case "resistor_leg1": return GetRowColLocalPosition(15, "D");
                case "resistor_leg2": return GetRowColLocalPosition(20, "D");
                case "resistor_center": return (GetRowColLocalPosition(15, "D") + GetRowColLocalPosition(20, "D")) * 0.5f;

                // Wire 1: LED Anode to Resistor
                case "wire1_start": return GetRowColLocalPosition(10, "E");
                case "wire1_end": return GetRowColLocalPosition(15, "E");
                case "wire1_center": return (GetRowColLocalPosition(10, "E") + GetRowColLocalPosition(15, "E")) * 0.5f;

                // Wire 2: Resistor to ESP32 GPIO25
                case "wire2_start": return GetRowColLocalPosition(20, "A");
                case "wire2_end": return GetESP32Pin("GPIO25");
                case "wire2_center": return (GetRowColLocalPosition(20, "A") + GetESP32Pin("GPIO25")) * 0.5f;

                // Wire 3: LED Cathode to GND
                case "wire3_start": return GetRowColLocalPosition(11, "A");
                case "wire3_end": return GetESP32Pin("GND");
                case "wire3_center": return (GetRowColLocalPosition(11, "A") + GetESP32Pin("GND")) * 0.5f;

                case "esp32_gpio25": return GetESP32Pin("GPIO25");
                case "esp32_gnd": return GetESP32Pin("GND");
                case "esp32_usb": return GetESP32Pin("USB");
                case "esp32_center": return ESP32_CENTER;

                default: return Vector3.zero;
            }
        }
    }
}
