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

    [HideInInspector] public int expectedQRCount = 5;
    [HideInInspector] public System.Collections.Generic.HashSet<string> trackedQRFilter = null;
    [HideInInspector] public System.Action<int, int> onQRCountChanged;
    [HideInInspector] public System.Action onAllQRDetected;

    // Persistent QR tracking — populated on first detection, never cleared
    private Dictionary<string, ARTrackedImage> trackedImages = new Dictionary<string, ARTrackedImage>();

    // Persistent number labels ("0", "1", …) — created once per QR, shown/hidden
    private Dictionary<string, GameObject> defaultLabels = new Dictionary<string, GameObject>();

    // Game objects (bottles, shakers, glasses) placed by CocktailManager / TutorialManager
    private Dictionary<string, GameObject> gameObjects = new Dictionary<string, GameObject>();

    private bool labelsHidden = false;

    void Start()
    {
        BottleLabelHelper.labelPrefab = floatingLabelPrefab;
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
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            // Strip any mesh/colliders from the anchor so it can't occlude or block held objects
            foreach (Collider col in trackedImage.GetComponentsInChildren<Collider>())
                col.enabled = false;
            var mr = trackedImage.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;

            HandleQRDetected(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            HandleQRUpdated(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in eventArgs.removed)
        {
            HandleQRRemoved(trackedImage);
        }
    }

    void HandleQRDetected(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;
        bool isNew = !trackedImages.ContainsKey(imageName);
        trackedImages[imageName] = trackedImage;

        // Create the default number label on first-ever detection (persists forever)
        if (!defaultLabels.ContainsKey(imageName))
        {
            string slotNumber = imageName.Replace("qr", "");
            GameObject label = BottleLabelHelper.AddLabel(trackedImage.transform, slotNumber, slotNumberHeight, absoluteOffset: true);
            defaultLabels[imageName] = label;
            label.SetActive(!labelsHidden && !gameObjects.ContainsKey(imageName));
            Debug.Log($"[QR] Created label '{slotNumber}' for {imageName}");
        }
        else
        {
            // Re-anchor label that was detached during tracking loss
            ReanchorObject(defaultLabels[imageName], trackedImage.transform);
        }

        // Re-anchor game object that was detached during tracking loss
        if (gameObjects.ContainsKey(imageName))
            ReanchorObject(gameObjects[imageName], trackedImage.transform);

        if (isNew)
        {
            int counted = CountTracked();
            onQRCountChanged?.Invoke(counted, expectedQRCount);
            if (counted >= expectedQRCount)
                onAllQRDetected?.Invoke();
        }
    }

    void HandleQRUpdated(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;
        trackedImages[imageName] = trackedImage;

        if (trackedImage.trackingState == TrackingState.None)
            return;

        if (defaultLabels.ContainsKey(imageName))
        {
            GameObject label = defaultLabels[imageName];
            if (label != null && label.transform.parent != trackedImage.transform)
                label.transform.SetParent(trackedImage.transform, false);
        }

        if (gameObjects.ContainsKey(imageName))
        {
            GameObject obj = gameObjects[imageName];
            if (obj != null && obj.transform.parent != trackedImage.transform)
                obj.transform.SetParent(trackedImage.transform, false);
        }
    }

    void HandleQRRemoved(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;

        // Detach objects so they survive the ARTrackedImage being destroyed by ARFoundation.
        // They float at last known position and get re-anchored on re-detection.
        if (defaultLabels.ContainsKey(imageName))
        {
            GameObject label = defaultLabels[imageName];
            if (label != null) label.transform.SetParent(null, true);
        }

        if (gameObjects.ContainsKey(imageName))
        {
            GameObject obj = gameObjects[imageName];
            if (obj != null) obj.transform.SetParent(null, true);
        }

        trackedImages.Remove(imageName);
    }

    void ReanchorObject(GameObject obj, Transform qrTransform)
    {
        if (obj != null && obj.transform.parent != qrTransform)
            obj.transform.SetParent(qrTransform, true);
    }

    // ===== Public API =====

    public Transform GetQRTransform(string qrName)
    {
        if (trackedImages.ContainsKey(qrName))
            return trackedImages[qrName].transform;
        return null;
    }

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

        return nearestPos + Vector3.up * 0.1f;
    }

    // Place a game object at a QR slot — hides the default label
    public void RegisterBottleAtQR(string qrName, GameObject bottle)
    {
        RemoveBottleAtQR(qrName);
        gameObjects[qrName] = bottle;

        if (defaultLabels.ContainsKey(qrName) && defaultLabels[qrName] != null)
            defaultLabels[qrName].SetActive(false);
    }

    // Remove the game object at a QR slot — shows the default label (if not suppressed)
    public void RemoveBottleAtQR(string qrName)
    {
        if (gameObjects.ContainsKey(qrName))
        {
            GameObject obj = gameObjects[qrName];
            if (obj != null)
            {
                obj.transform.SetParent(null);
                Destroy(obj);
            }
            gameObjects.Remove(qrName);
        }

        if (!labelsHidden && defaultLabels.ContainsKey(qrName) && defaultLabels[qrName] != null)
            defaultLabels[qrName].SetActive(true);
    }

    public GameObject GetBottleAtQR(string qrName)
    {
        if (gameObjects.ContainsKey(qrName))
            return gameObjects[qrName];
        return null;
    }

    // Remove all game objects from all slots — shows labels where appropriate
    public void ClearAllGameObjects()
    {
        var keys = new List<string>(gameObjects.Keys);
        foreach (string key in keys)
            RemoveBottleAtQR(key);
    }

    // Label visibility — for tutorial mode
    public void HideLabels()
    {
        labelsHidden = true;
        foreach (var label in defaultLabels.Values)
            if (label != null) label.SetActive(false);
    }

    public void ShowLabels()
    {
        labelsHidden = false;
        foreach (var kvp in defaultLabels)
            if (kvp.Value != null)
                kvp.Value.SetActive(!gameObjects.ContainsKey(kvp.Key));
    }

    public int CountTracked()
    {
        if (trackedQRFilter == null) return trackedImages.Count;
        int count = 0;
        foreach (var key in trackedImages.Keys)
            if (trackedQRFilter.Contains(key)) count++;
        return count;
    }
}
