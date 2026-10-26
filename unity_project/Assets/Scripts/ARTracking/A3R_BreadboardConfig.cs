using UnityEngine;

namespace A3R.ARTracking
{
    /// <summary>
    /// A3R_BreadboardConfig — Authoritative single source of truth for
    /// physical breadboard footprint dimensions used in markerless AR placement.
    /// Matches the actual physical half-size / 400-tie-point breadboard:
    /// 82 mm (0.082m) length x 54 mm (0.054m) width x 9 mm (0.009m) height.
    /// </summary>
    public static class A3R_BreadboardConfig
    {
        // Actual physical breadboard dimensions in meters
        public const float WIDTH = 0.082f;   // 82 mm (length along X-axis, left-to-right across camera view)
        public const float DEPTH = 0.054f;   // 54 mm (depth along Z-axis, top-to-bottom across power rails)
        public const float HEIGHT = 0.009f;  // 9 mm (physical breadboard height)

        public static Vector3 PhysicalSize => new Vector3(WIDTH, HEIGHT, DEPTH);
        public static Vector2 SurfaceSize => new Vector2(WIDTH, DEPTH);

        // Phase 4 Placeholder Component Offsets (Local to BreadboardARRoot center)
        // Authoritative single source of truth for development targets, easily modifiable in one place.
        public static readonly Vector3 DEFAULT_RESISTOR_LOCAL_POS = new Vector3(-0.020f, HEIGHT, -0.010f);
        public static readonly Vector3 DEFAULT_LED_LOCAL_POS      = new Vector3(-0.020f, HEIGHT,  0.010f);
        public static readonly Vector3 DEFAULT_ESP32_LOCAL_POS    = new Vector3( 0.015f, HEIGHT,  0.000f);
    }
}
