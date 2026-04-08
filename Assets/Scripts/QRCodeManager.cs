using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class QRCodeManager : MonoBehaviour
{
    [Header("AR Components")]
    public ARTrackedImageManager trackedImageManager;
    
    [Header("Bottle Prefabs (4 items)")]
    public GameObject[] bottlePrefabs; // AlcoBottleV1, AlcoBottleV7, EmptyGlassV4, Shaker
    
    [Header("Bottle Positioning")]
    public float bottle1Offset = 0.08f;  // AlcoBottleV1
    public float bottle2Offset = 0.05f;  // AlcoBottleV7
    public float bottle3Offset = 0.03f;  // EmptyGlassV4
    public float bottle4Offset = 0.06f;  // Shaker 
    public float bottle5Offset = 0.06f;  // Serving cup 

    
    // Track spawned objects
    private Dictionary<string, GameObject> spawnedBottles = new Dictionary<string, GameObject>();
    private Dictionary<string, ARTrackedImage> trackedImages = new Dictionary<string, ARTrackedImage>();
    
    void OnEnable()
    {
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }
    
    void OnDisable()
    {
        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }
    
    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        // Handle newly detected QR codes
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            // Strip any mesh/colliders from the anchor so it can't occlude or block held objects
            foreach (Collider col in trackedImage.GetComponentsInChildren<Collider>())
                col.enabled = false;
            var mr = trackedImage.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            SpawnBottleForQRCode(trackedImage);
        }
        
        // Handle updated QR codes
        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            UpdateBottlePosition(trackedImage);
        }
        
        // Handle removed QR codes
        foreach (ARTrackedImage trackedImage in eventArgs.removed)
        {
            RemoveBottle(trackedImage);
        }
    }
    
    void SpawnBottleForQRCode(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;

        // Always register the transform so CocktailManager can place objects here
        if (!trackedImages.ContainsKey(imageName))
            trackedImages.Add(imageName, trackedImage);

        // Don't spawn default prefab if already exists
        if (spawnedBottles.ContainsKey(imageName))
            return;

        GameObject bottlePrefab = GetBottlePrefabByName(imageName);
        if (bottlePrefab != null)
        {
            GameObject bottle = Instantiate(bottlePrefab, trackedImage.transform.position, Quaternion.identity);
            bottle.transform.SetParent(trackedImage.transform, false);
            bottle.transform.localPosition = Vector3.up * GetBottleHeightOffset(imageName);
            bottle.transform.localRotation = Quaternion.identity;

            spawnedBottles.Add(imageName, bottle);

            // Label below bottle using cleaned prefab name
            string label = Regex.Replace(bottlePrefab.name, @"(Prefab|prefab|AlcoBottle|EmptyGlass|Drink|V\d+)$", "").Trim();
            BottleLabelHelper.AddLabel(bottle.transform, label);

            Debug.Log($"[DEBUG] ✓ Spawned {bottlePrefab.name} for QR: {imageName}");
        }
        else
        {
            Debug.Log($"[DEBUG] ✓ Tracking QR: {imageName} (no default prefab)");
        }
    }
    
    void UpdateBottlePosition(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;

        // Keep transform up to date even if no default prefab was spawned
        if (trackedImages.ContainsKey(imageName))
            trackedImages[imageName] = trackedImage;

        if (spawnedBottles.ContainsKey(imageName))
        {
            // NEW: Keep visible unless tracking is completely lost
            GameObject bottle = spawnedBottles[imageName];
            bool shouldShow = trackedImage.trackingState != TrackingState.None;
            if (shouldShow)
                {
                    trackedImages[imageName] = trackedImage;
                    
                    // Ensure bottle stays parented and anchored
                    if (bottle.transform.parent != trackedImage.transform)
                        {
                            bottle.transform.SetParent(trackedImage.transform, false);  // ← Change true to false
                        }
                        bottle.transform.localRotation = Quaternion.identity;  // ← Add this line
                }
        }
    }
    
    void RemoveBottle(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;
        
        if (spawnedBottles.ContainsKey(imageName))
        {
            Destroy(spawnedBottles[imageName]);
            spawnedBottles.Remove(imageName);
            trackedImages.Remove(imageName);
            Debug.Log($"[DEBUG] ✗ Removed bottle: {imageName}");
        }
    }
    
    GameObject GetBottlePrefabByName(string qrCodeName)
    {
        int index = -1;
        switch (qrCodeName)
        {
            case "qr0": index = 0; break;
            case "qr1": index = 1; break;
            case "qr2": index = 2; break;
            case "qr3": index = 3; break;
            case "qr4": index = 4; break;
            default:
                Debug.LogWarning($"[DEBUG] ⚠ No prefab mapped for QR code: {qrCodeName}");
                return null;
        }

        if (index >= bottlePrefabs.Length)
        {
            Debug.LogWarning($"[DEBUG] ⚠ bottlePrefabs array too short — index {index} requested but only {bottlePrefabs.Length} entries assigned in Inspector");
            return null;
        }

        return bottlePrefabs[index];
    }
        
    // Public method to get nearest QR code position (for cup snapping)
    public Vector3 GetNearestQRPosition(Vector3 referencePosition)
    {
        Vector3 nearestPos = referencePosition;
        float nearestDist = float.MaxValue;
        
        foreach (var trackedImage in trackedImages.Values)
        {
            if (trackedImage.trackingState == TrackingState.Tracking)
            {
                float dist = Vector3.Distance(referencePosition, trackedImage.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestPos = trackedImage.transform.position;
                }
            }
        }
        
        // Return position slightly above table
        return nearestPos + Vector3.up * 0.1f;
    }
    
    float GetBottleHeightOffset(string qrCodeName)
    {
        switch (qrCodeName)
        {
            case "qr0": return bottle1Offset;
            case "qr1": return bottle2Offset;
            case "qr2": return bottle3Offset;
            case "qr3": return bottle4Offset;
            case "qr4": return bottle5Offset;
            default: return bottle1Offset;
        }
    }

    // Get the Transform of a tracked QR code
    public Transform GetQRTransform(string qrName)
    {
        if (trackedImages.ContainsKey(qrName))
        {
            return trackedImages[qrName].transform;
        }
        
        return null;
    }
    public void RemoveBottleAtQR(string qrName)
    {
        if (spawnedBottles.ContainsKey(qrName))
        {
            GameObject bottle = spawnedBottles[qrName];
            if (bottle != null)
            {
                Destroy(bottle);
            }
            spawnedBottles.Remove(qrName);
        }
    }

    // Register a new bottle at QR (for cocktail manager)
    public void RegisterBottleAtQR(string qrName, GameObject bottle)
    {
        // Remove old if exists
        RemoveBottleAtQR(qrName);

        // Add new
        spawnedBottles[qrName] = bottle;
    }

    // Get the spawned object at a QR slot (for MQTT slot-based grab)
    public GameObject GetBottleAtQR(string qrName)
    {
        if (spawnedBottles.ContainsKey(qrName))
            return spawnedBottles[qrName];
        return null;
    }

}
