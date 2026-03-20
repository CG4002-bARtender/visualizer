using UnityEngine;

public class PourReceiver : MonoBehaviour
{
    [Header("Container Info")]
    public string containerName = "Cup";  // "Shaker", "Glass", etc.
    public ContainerType type = ContainerType.Cup;
    
    [Header("Fill Settings")]
    [Range(0f, 1f)]
    public float fillAmount = 0f;
    public float maxCapacity = 1.0f;  // 100%
    
    [Header("Visual Feedback")]
    public Color emptyColor = Color.clear;
    public Color fullColor = new Color(1f, 0.8f, 0f, 0.5f);  // Amber liquid
    
    // What liquids are in this container
    private System.Collections.Generic.List<string> contents = new System.Collections.Generic.List<string>();
    
    public enum ContainerType
    {
        Shaker,
        Cup,
        Bottle
    }
    
    public bool CanReceiveLiquid()
    {
        return fillAmount < maxCapacity;
    }
    
    public void AddLiquid(string liquidType, float amount)
    {
        if (!CanReceiveLiquid()) return;
        
        fillAmount = Mathf.Clamp01(fillAmount + amount);
        
        if (!contents.Contains(liquidType))
        {
            contents.Add(liquidType);
        }
        
        Debug.Log($"✓ Added {liquidType} to {containerName}. Fill: {fillAmount * 100:F0}%");
        
        // Update visual if needed
        // UpdateVisual();
    }
    
    public void Empty()
    {
        fillAmount = 0f;
        contents.Clear();
        // UpdateVisual();
    }
    
    public string GetContents()
    {
        return string.Join(", ", contents);
    }
}