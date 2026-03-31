using UnityEngine;

public class PourReceiver : MonoBehaviour
{
    [Header("Container Info")]
    public string containerName = "Cup";  // "Shaker", "Glass", etc.
    public ContainerType containerType = ContainerType.ServingGlass;  // ← Changed from 'type'
    
    [Header("Fill Settings")]
    [Range(0f, 1f)]
    public float fillAmount = 0f;
    public float maxCapacity = 1.0f;

    [Header("Visual Feedback")]
    public Color emptyColor = Color.clear;
    public Color fullColor = new Color(1f, 0.8f, 0f, 0.5f);  // Amber liquid
    
    // What liquids are in this container
    private System.Collections.Generic.List<string> contents = new System.Collections.Generic.List<string>();
    
    public enum ContainerType
    {
        Shaker,
        ServingGlass,  // ← Changed from 'Cup'
        Bottle
    }
    
    void Start()
{
    // Auto-set capacity based on container type
    if (containerType == ContainerType.Shaker)
    {
        maxCapacity = 2.0f;  // Shakers hold 2 bottles
    }
    else if (containerType == ContainerType.ServingGlass)
    {
        maxCapacity = 1.0f;  // Glasses hold 1 bottle (or mixed from shaker)
    }
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
        
        Debug.Log($"[DEBUG] ✓ Added {liquidType} to {containerName}. Fill: {fillAmount * 100:F0}%");
        
        // Update visual if needed
        // UpdateVisual();
    }
    
    public void Empty()
    {
        fillAmount = 0f;
        contents.Clear();
        Debug.Log($"[DEBUG] 🧹 Emptied {containerName}");
        // UpdateVisual();
    }
    
    public string GetContents()
    {
        return string.Join(", ", contents);
    }
}