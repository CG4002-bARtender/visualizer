using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class QRCodeManager : MonoBehaviour
{
    [Header("AR Components")]
    public ARTrackedImageManager trackedImageManager;
    
    [Header("Bottle Prefabs (4 items)")]
    public GameObject[] bottlePrefabs; // AlcoBottleV1, AlcoBottleV7, EmptyGlassV4, Shaker
    
    [Header("Bottle Positioning")] // How high above QR code (adjust to taste)
    public float bottleHeightOffset = 0.08f;  // Default (not used anymore)
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
        
        // Don't spawn if already exists
        if (spawnedBottles.ContainsKey(imageName))
            return;
        
        GameObject bottlePrefab = GetBottlePrefabByName(imageName);
        if (bottlePrefab != null)
        {
            GameObject bottle = Instantiate(bottlePrefab, trackedImage.transform.position, Quaternion.identity);
            bottle.transform.SetParent(trackedImage.transform, false);
            bottle.transform.localPosition = Vector3.up * GetBottleHeightOffset(imageName);
            bottle.transform.localRotation = Quaternion.identity;

            bottle.tag = "Grabbable"; //NEW

            spawnedBottles.Add(imageName, bottle);
            trackedImages.Add(imageName, trackedImage);
            
            Debug.Log($"✓ Spawned {bottlePrefab.name} for QR: {imageName}");
        }
    }
    
    void UpdateBottlePosition(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;
        
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
            Debug.Log($"✗ Removed bottle: {imageName}");
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
                Debug.LogWarning($"⚠ No prefab mapped for QR code: {qrCodeName}");
                return null;
        }

        if (index >= bottlePrefabs.Length)
        {
            Debug.LogWarning($"⚠ bottlePrefabs array too short — index {index} requested but only {bottlePrefabs.Length} entries assigned in Inspector");
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
    
    // Helper method to check if any QR codes are being tracked
    public bool HasTrackedQRCodes()
    {
        return trackedImages.Count > 0;
    }

    public void ResetAllBottlePositions()
    {
        foreach (var kvp in spawnedBottles)
        {
            string imageName = kvp.Key;
            GameObject bottle = kvp.Value;
            
            if (trackedImages.ContainsKey(imageName))
            {
                ARTrackedImage trackedImage = trackedImages[imageName];
                
                // Re-parent with clean transform
                bottle.transform.SetParent(trackedImage.transform, false);
                bottle.transform.localPosition = Vector3.up * bottleHeightOffset;
                bottle.transform.localRotation = Quaternion.identity;
                
                Debug.Log($"✓ Reset {bottle.name} to QR anchor");
            }
        }
        
        Debug.Log("✓ All bottles reset!");
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
            default: return bottleHeightOffset;
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
