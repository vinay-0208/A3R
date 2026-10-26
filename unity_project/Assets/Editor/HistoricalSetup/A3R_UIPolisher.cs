using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class A3R_UIPolisher : MonoBehaviour
{
    [MenuItem("A3R/Apply UI Polish")]
    public static void PolishUI()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        
        Image[] images = canvas.GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            if (img.gameObject.name.Contains("Panel") || img.gameObject.name.Contains("Card") || img.gameObject.name.Contains("Drawer"))
            {
                // Dark glassmorphism
                Color c = img.color;
                if (c.r < 0.2f && c.g < 0.2f && c.b < 0.2f) // If it's already dark
                {
                    img.color = new Color(0.05f, 0.05f, 0.08f, 0.75f);
                }
            }
        }
        
        Debug.Log("UI Polished.");
    }
}
