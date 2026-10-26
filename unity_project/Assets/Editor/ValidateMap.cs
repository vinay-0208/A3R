using UnityEngine;
using UnityEditor;
using A3R.ARTracking;

public static class ValidateMap
{
    public static void Run()
    {
        var report = A3R_BreadboardMap.Validate();
        Debug.Log(report.FormattedReport);
        
        string[] holes = { "D10", "D11", "D15", "D20" };
        foreach (var h in holes)
        {
            var pos = A3R_BreadboardMap.GetHolePosition(h);
            Debug.Log($"{h}: X={pos.x:F5} Y={pos.y:F5} Z={pos.z:F5}");
        }
    }
}
