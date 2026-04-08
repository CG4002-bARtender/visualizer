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
    public GameObject emptyGlassAviationPrefab;
    public GameObject emptyGlassGodfatherPrefab;
    public GameObject emptyGlassIrishCoffeePrefab;
    public GameObject emptyGlassMartiniPrefab;
    public GameObject emptyGlassMidoriSourPrefab;
    public GameObject emptyGlassNeatPrefab;
    public GameObject emptyGlassOldFashionedPrefab;
    public GameObject emptyGlassTuxedoPrefab;
    public GameObject emptyGlassVodkaPrefab;
    
    [Header("Final Drink Prefabs")]
    public GameObject drinkAviationPrefab;
    public GameObject drinkGodfatherPrefab;
    public GameObject drinkIrishCoffeePrefab;
    public GameObject drinkMartiniPrefab;
    public GameObject drinkMidoriSourPrefab;
    public GameObject drinkOldFashionedPrefab;
    public GameObject drinkNeatPrefab;
    public GameObject drinkTuxedoPrefab;
    public GameObject drinkVodkaPrefab;

    [Header("Shaker")]
    public GameObject shakerPrefab;

    [Header("Result Markers")]
    public GameObject failMarkerPrefab;
    public GameObject successMarkerPrefab;
    public float markerHeight = 0.08f;

    [Header("Settings")]
    public float bottleHeight = 0.08f;
    public float shakerHeight = 0.06f;
    public float glassHeight = 0.03f;

    // Track currently spawned items for cleanup
    private List<GameObject> currentCocktailItems = new List<GameObject>();
    private GameObject currentResultMarker;
    
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
        
        newItem.transform.localPosition = Vector3.up * heightOffset;
        newItem.transform.localRotation = Quaternion.identity;

        // Add label below bottle (use ingredient name from prefab name, cleaned up)
        string label = System.Text.RegularExpressions.Regex.Replace(prefab.name, @"(Prefab|prefab|AlcoBottle|EmptyGlass|Drink|V\d+)$", "").Trim();
        var labelGO = BottleLabelHelper.AddLabel(newItem.transform, label);
        currentCocktailItems.Add(labelGO);

        currentCocktailItems.Add(newItem);
        qrCodeManager.RegisterBottleAtQR(qrName, newItem);

        Debug.Log($"[DEBUG]   ✓ Spawned {prefab.name} at {qrName}");
    }
    
    public void ClearCurrentCocktail()
    {
        foreach (GameObject item in currentCocktailItems)
        {
            if (item != null)
                Destroy(item);
        }
        currentCocktailItems.Clear();

        if (currentResultMarker != null)
        {
            Destroy(currentResultMarker);
            currentResultMarker = null;
        }

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

    public void ShowFailMarker()
    {
        SpawnResultMarker(failMarkerPrefab, "Fail");
    }

    public void ShowSuccessMarker()
    {
        SpawnResultMarker(successMarkerPrefab, "Success");
    }

    void SpawnResultMarker(GameObject prefab, string label)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[DEBUG] ⚠ {label} marker prefab not assigned in CocktailManager!");
            return;
        }

        Transform qrTransform = qrCodeManager.GetQRTransform("qr4");
        if (qrTransform == null)
        {
            Debug.LogWarning("[DEBUG] ⚠ qr4 not found for result marker");
            return;
        }

        if (currentResultMarker != null)
            Destroy(currentResultMarker);

        currentResultMarker = Instantiate(prefab, qrTransform.position, Quaternion.identity);
        currentResultMarker.transform.SetParent(qrTransform, false);
        currentResultMarker.transform.localPosition = Vector3.up * markerHeight;
        currentResultMarker.transform.localRotation = Quaternion.identity;
        Debug.Log($"[DEBUG] {label} marker shown at qr4");
    }

    public void ShowFinalDrink(int drinkInt)
    {
        GameObject prefab = GetFinalDrinkPrefab(drinkInt);
        if (prefab != null)
            ReplaceItemAtQR("qr4", prefab, glassHeight);
        else
            Debug.LogWarning($"[DEBUG] ⚠ No final drink prefab for drink {drinkInt}");
    }

    GameObject GetFinalDrinkPrefab(int drinkInt)
    {
        switch (drinkInt)
        {
            case 0: return drinkAviationPrefab;
            case 1: return drinkGodfatherPrefab;
            case 2: return drinkIrishCoffeePrefab;
            case 3: return drinkMartiniPrefab;
            case 4: return drinkMidoriSourPrefab;
            case 5: return drinkOldFashionedPrefab;
            case 6: return drinkNeatPrefab;
            case 7: return drinkTuxedoPrefab;
            case 8: return drinkVodkaPrefab;
            case 9: return drinkNeatPrefab;
            default:
                Debug.LogWarning($"[DEBUG] ⚠ No final drink prefab mapped for drink int: {drinkInt}");
                return null;
        }
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
            case 0: return emptyGlassAviationPrefab;      // Aviation
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