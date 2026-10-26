using UnityEngine;

namespace A3R.ARTracking
{
    /// <summary>
    /// Represents the strict local hole grid coordinate system of the breadboard.
    /// Used as a deterministic fallback if physical holes cannot be individually tracked.
    /// Operates in BreadboardARRoot local space.
    /// </summary>
    public class A3R_HoleGrid : MonoBehaviour
    {
        [Header("Grid Configuration")]
        [Tooltip("Standard standard 2.54mm (0.1 inch) pitch.")]
        public float HoleSpacing = 0.00254f;

        [Tooltip("Number of rows (horizontal layout assuming width > depth)")]
        public int Rows = 10; // J-A, typically 5 holes per side, 2 sides = 10

        [Tooltip("Number of columns (vertical lines)")]
        public int Columns = 30; // typically 30 columns on half size

        [Tooltip("Offset of Hole (0,0) from the BreadboardARRoot origin in local space")]
        public Vector3 OriginOffset = new Vector3(-0.035f, 0.009f, -0.015f);

        /// <summary>
        /// Converts a logical grid coordinate (row, column) into a physical local position.
        /// </summary>
        public Vector3 GetLocalPosition(int row, int col)
        {
            float xOffset = col * HoleSpacing;
            float zOffset = row * HoleSpacing;
            
            // Add a gap in the middle (between row 4 and 5, typically for ICs)
            if (row >= 5) 
            {
                zOffset += 0.00762f; // ~0.3 inch gap
            }

            return OriginOffset + new Vector3(xOffset, 0f, zOffset);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.yellow;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    Gizmos.DrawSphere(GetLocalPosition(r, c), 0.0005f);
                }
            }
        }
    }
}
