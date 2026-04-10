using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class QRCodeManager : MonoBehaviour
{
    [Header("AR Components")]
    public ARTrackedImageManager trackedImageManager;

    [Header("Label Settings")]
    public GameObject floatingLabelPrefab;
    public float slotNumberHeight = 0.03f;

    // Track spawned objects
    private Dictionary<string, GameObject> spawnedBottles = new Dictionary<string, GameObject>();
    private Dictionary<string, ARTrackedImage> trackedImages = new Dictionary<string, ARTrackedImage>();
    
    void Start()
    {
        BottleLabelHelper.labelPrefab = floatingLabelPrefab;
        // Scanning is disabled until the game enters idle state
        trackedImageManager.enabled = false;
    }

    void OnEnable()
    {
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    void OnDisable()
    {
        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    public void EnableScanning()
    {
        if (!trackedImageManager.enabled)
            trackedImageManager.enabled = true;
    }

    public void DisableScanning()
    {
        if (trackedImageManager.enabled)
            trackedImageManager.enabled = false;
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

        // Don't spawn if already exists
        if (spawnedBottles.ContainsKey(imageName))
            return;

        // Spawn a number label (e.g. qr0 → "0") above the QR code
        string slotNumber = imageName.Replace("qr", "");
        GameObject label = BottleLabelHelper.AddLabel(trackedImage.transform, slotNumber, slotNumberHeight, absoluteOffset: true);

        spawnedBottles.Add(imageName, label);

        Debug.Log($"[DEBUG] ✓ Spawned slot number '{slotNumber}' for QR: {imageName}");
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

    // Re-spawn default bottles for all currently tracked QR images that have no bottle
    // Called after a round ends and cocktail bottles are cleared
    public void RespawnDefaultBottles()
    {
        foreach (var kvp in trackedImages)
        {
            if (!spawnedBottles.ContainsKey(kvp.Key))
                SpawnBottleForQRCode(kvp.Value);
        }
    }

}
