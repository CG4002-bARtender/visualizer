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
    
    [Header("Bottle Positioning")]
    public float bottleHeightOffset = 0.08f; // How high above QR code (adjust to taste)
    
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
            Vector3 spawnPos = trackedImage.transform.position + Vector3.up * bottleHeightOffset;
            // Spawn bottle above QR code
            GameObject bottle = Instantiate(bottlePrefab, spawnPos, Quaternion.identity);
            bottle.transform.SetParent(trackedImage.transform, false);  // ← false is key!
            bottle.transform.localRotation = Quaternion.identity;    

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
    // This function maps QR code names to bottle prefabs
    // The string names MUST match your Reference Image Library exactly (case-sensitive!)
    
    switch (qrCodeName)
    {
        case "QR 1":
            // QR 1 shows AlcoBottleV1 (bottlePrefabs[0])
            return bottlePrefabs[0];
            
        case "QR 2":
            // QR 2 shows AlcoBottleV7 (bottlePrefabs[1])
            return bottlePrefabs[1];
            
        case "QR 3":
            // QR 3 shows EmptyGlassV4 (bottlePrefabs[2])
            return bottlePrefabs[2];
            
        case "QR 4":
            // QR 4 shows Shaker (bottlePrefabs[3])
            return bottlePrefabs[3];
            
        default:
            // If QR code name doesn't match anything above, show warning
            Debug.LogWarning($"⚠ No prefab mapped for QR code: {qrCodeName}");
            Debug.LogWarning($"💡 Make sure Reference Image Library names match the case statements!");
            return null;
    }
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

    // Add this method anywhere in the class
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
}
