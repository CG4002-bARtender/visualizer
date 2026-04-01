using UnityEngine;
using System.Collections.Generic;

public class CocktailManager : MonoBehaviour
{
    [Header("References")]
    public QRCodeManager qrCodeManager;
    
    [Header("Bottle Prefabs")]
    public GameObject bourbonPrefab;
    public GameObject darkRumPrefab;
    public GameObject ginPrefab;
    public GameObject midoriPrefab;
    public GameObject purpleLiqueurPrefab;
    public GameObject ryeWhiskeyPrefab;
    public GameObject vodkaPrefab;
    public GameObject scotchPrefab;
    public GameObject whiskeyPrefab;
    
    [Header("Empty Serving Glasses")]
    public GameObject emptyGlassGodfatherPrefab;
    public GameObject emptyGlassIrishCoffeePrefab;
    public GameObject emptyGlassMartiniPrefab;
    public GameObject emptyGlassMidoriSourPrefab;
    public GameObject emptyGlassNeatPrefab;
    public GameObject emptyGlassOldFashionedPrefab;
    public GameObject emptyGlassTuxedoPrefab;
    public GameObject emptyGlassVodkaPrefab;
    
    [Header("Shaker")]
    public GameObject shakerPrefab;
    
    [Header("Settings")]
    public float bottleHeight = 0.08f;
    public float shakerHeight = 0.06f;
    public float glassHeight = 0.03f;
    
    // Track currently spawned items for cleanup
    private List<GameObject> currentCocktailItems = new List<GameObject>();
    
    void ReplaceItemAtQR(string qrName, GameObject prefab, float heightOffset)
    {
        if (prefab == null || qrCodeManager == null)
        {
            Debug.LogWarning($"[DEBUG] ⚠ Cannot replace at {qrName}: prefab or QRCodeManager is null");
            return;
        }
        
        Transform qrTransform = qrCodeManager.GetQRTransform(qrName);
        
        if (qrTransform == null)
        {
            Debug.LogWarning($"[DEBUG] ⚠ QR code '{qrName}' not found!");
            return;
        }
        
        // Remove existing item at this QR
        qrCodeManager.RemoveBottleAtQR(qrName);
        
        GameObject newItem = Instantiate(prefab, qrTransform.position, Quaternion.identity); // NEW: Spawn at QR position first
        newItem.transform.SetParent(qrTransform, false); // NEW: Parent FIRST with worldPositionStays = false
        
        newItem.transform.localPosition = Vector3.up * heightOffset; // NEW: Then set LOCAL position (relative to QR)
        newItem.transform.localRotation = Quaternion.identity;
        
        currentCocktailItems.Add(newItem); // Track for cleanup    
        qrCodeManager.RegisterBottleAtQR(qrName, newItem); // Register with QRCodeManager
        
        Debug.Log($"[DEBUG]   ✓ Spawned {prefab.name} at {qrName}");
    }
    
    void ClearCurrentCocktail()
    {
        foreach (GameObject item in currentCocktailItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        
        currentCocktailItems.Clear();
        Debug.Log("[DEBUG] 🧹 Cleared previous cocktail items");
    }
    
    // ===== MQTT-DRIVEN SETUP =====

    // Called by MQTTManager when a new order arrives.
    // drinkInt maps to the drink enum (0=Aviation, 1=Godfather, ...).
    // bottleMap keys are slot IDs (0,1,3); values are ingredient name strings.
    public void SetupFromMQTT(int drinkInt, Dictionary<int, string> bottleMap)
    {
        Debug.Log($"[DEBUG] 🍹 SetupFromMQTT: drink={drinkInt}, slots={string.Join(", ", bottleMap)}");
        ClearCurrentCocktail();

        // Place bottles at slots 0, 1, 3 from the bottle_map
        int[] slots = { 0, 1, 3 };
        string[] qrNames = { "qr0", "qr1", "qr3" };

        for (int i = 0; i < slots.Length; i++)
        {
            if (bottleMap.ContainsKey(slots[i]))
            {
                string ingredient = bottleMap[slots[i]];
                GameObject prefab = GetBottlePrefabByIngredient(ingredient);
                if (prefab != null)
                    ReplaceItemAtQR(qrNames[i], prefab, bottleHeight);
                else
                    Debug.LogWarning($"[DEBUG] ⚠ No prefab found for ingredient '{ingredient}' at slot {slots[i]}");
            }
            else
            {
                Debug.Log($"[DEBUG]   slot {slots[i]} not in bottle_map, skipping");
            }
        }

        // Shaker always at qr2
        if (shakerPrefab != null)
            ReplaceItemAtQR("qr2", shakerPrefab, shakerHeight);
        else
            Debug.LogWarning("[DEBUG] ⚠ shakerPrefab not assigned in CocktailManager!");

        // Serving glass at qr4 based on drink type
        GameObject glassPrefab = GetGlassPrefabByDrink(drinkInt);
        if (glassPrefab != null)
            ReplaceItemAtQR("qr4", glassPrefab, glassHeight);
        else
            Debug.LogWarning($"[DEBUG] ⚠ No glass prefab for drink {drinkInt}");

        Debug.Log($"[DEBUG] ✓ MQTT setup complete for drink {drinkInt}");
    }

    GameObject GetBottlePrefabByIngredient(string ingredient)
    {
        switch (ingredient.ToLower().Replace(" ", ""))
        {
            case "gin":            return ginPrefab;
            case "purpleliqueur":  return purpleLiqueurPrefab;
            case "bourbon":        return bourbonPrefab;
            case "scotch":         return scotchPrefab;
            case "darkrum":        return darkRumPrefab;
            case "midori":         return midoriPrefab;
            case "ryewhiskey":     return ryeWhiskeyPrefab;
            case "vodka":          return vodkaPrefab;
            case "whiskey":        return whiskeyPrefab;
            default:
                Debug.LogWarning($"[DEBUG] ⚠ No prefab mapped for ingredient: {ingredient}");
                return null;
        }
    }

    GameObject GetGlassPrefabByDrink(int drinkInt)
    {
        switch (drinkInt)
        {
            case 0: return emptyGlassMartiniPrefab;      // Aviation (uses coupe/martini style)
            case 1: return emptyGlassGodfatherPrefab;    // Godfather
            case 2: return emptyGlassIrishCoffeePrefab;  // IrishCoffee
            case 3: return emptyGlassMartiniPrefab;      // Martini
            case 4: return emptyGlassMidoriSourPrefab;   // MidoriSour
            case 5: return emptyGlassOldFashionedPrefab; // OldFashioned
            case 6: return emptyGlassNeatPrefab;         // ScotchNeat
            case 7: return emptyGlassTuxedoPrefab;       // Tuxedo
            case 8: return emptyGlassVodkaPrefab;        // VodkaNeat
            case 9: return emptyGlassNeatPrefab;         // WhiskeyNeat
            default:
                Debug.LogWarning($"[DEBUG] ⚠ No glass mapped for drink int: {drinkInt}");
                return emptyGlassMartiniPrefab;
        }
    }
}