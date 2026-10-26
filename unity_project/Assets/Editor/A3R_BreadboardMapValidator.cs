using UnityEditor;
using UnityEngine;
using A3R.ARTracking;

namespace A3R.Editor
{
    public static class A3R_BreadboardMapValidator
    {
        [MenuItem("A3R/Validate Breadboard Map")]
        public static void ValidateBreadboardMap()
        {
            var report = A3R_BreadboardMap.Validate();
            if (report.Pass)
            {
                EditorUtility.DisplayDialog("A3R Breadboard Map Validation", report.FormattedReport, "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("A3R Breadboard Map Validation FAILED", report.FormattedReport, "OK");
            }
        }
    }
}
