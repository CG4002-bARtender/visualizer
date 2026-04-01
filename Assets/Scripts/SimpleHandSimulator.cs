using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SimpleHandSimulator : MonoBehaviour
{
    [Header("References")]
    public QRCodeManager qrCodeManager;
    public Transform arCamera;

    [Header("Held Object Settings")]
    public float holdDistance = 0.4f; // Distance in front of camera when holding

    [Header("Pour Settings")]
    public float pourAngle = 60f;
    public GameObject liquidStreamPrefab;

    private bool isInPourState = false;
    private Coroutine pourEntryCoroutine = null;
    private GameObject activeLiquidStream;
    private PourReceiver activePourTarget = null;

    [Header("Debug")]
    public bool showDebugLogs = true;

    // Object holding state
    private GameObject currentCup;
    private bool isHoldingCup = false;
    private Transform originalParent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    // Highlight state
    [Header("Highlight Settings")]
    public Color highlightColor = new Color(1f, 0.85f, 0f, 1f); // Gold tint
    private GameObject currentHighlightedObject = null;
    private Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();

    void Start()
    {
        if (arCamera == null)
        {
            arCamera = Camera.main.transform;
        }
    }

    void Update()
    {
        if (isHoldingCup && currentCup != null)
        {
            UpdateHeldObjectPosition();
        }

        // Continuous pour: track particle position and fill container
        if (isInPourState && currentCup != null && activePourTarget != null)
        {
            if (activeLiquidStream != null)
            {
                Transform spout = GetSpoutTransform(currentCup);
                if (spout != null)
                    activeLiquidStream.transform.position = spout.position;
            }

            if (activePourTarget.CanReceiveLiquid())
                activePourTarget.AddLiquid(GetLiquidType(currentCup.name), 0.5f * Time.deltaTime);
        }
    }

    void UpdateHeldObjectPosition()
    {
        if (currentCup == null || arCamera == null) return;

        Vector3 targetPos = arCamera.position + arCamera.forward * holdDistance;

        currentCup.transform.position = Vector3.Lerp(
            currentCup.transform.position,
            targetPos,
            Time.deltaTime * 10f
        );

        currentCup.transform.rotation = Quaternion.Slerp(
            currentCup.transform.rotation,
            arCamera.rotation,
            Time.deltaTime * 10f
        );
    }

    // ===== CORE PICKUP / RELEASE =====

    void PickUpObject(GameObject obj)
    {
        currentCup = obj;

        originalParent = obj.transform.parent;
        originalPosition = obj.transform.localPosition;
        originalRotation = obj.transform.localRotation;

        obj.transform.parent = null;

        isHoldingCup = true;

        if (showDebugLogs) Debug.Log($"[DEBUG] Picked up {obj.name}");
    }

    public void OnReleaseCupButton()
    {
        ExitPourState();
        ClearHighlight();

        if (!isHoldingCup || currentCup == null)
        {
            if (showDebugLogs) Debug.Log("[DEBUG] Not holding anything");
            return;
        }

        if (originalParent != null)
        {
            currentCup.transform.parent = originalParent;
            currentCup.transform.localPosition = originalPosition;
            currentCup.transform.localRotation = originalRotation;

            if (showDebugLogs) Debug.Log($"[DEBUG] Returned {currentCup.name} to original QR code");
        }
        else
        {
            Vector3 nearestTable = qrCodeManager.GetNearestQRPosition(currentCup.transform.position);
            currentCup.transform.position = nearestTable;
            currentCup.transform.rotation = Quaternion.identity;

            if (showDebugLogs) Debug.Log($"[DEBUG] Dropped {currentCup.name} at nearest QR");
        }

        isHoldingCup = false;
        currentCup = null;
    }

    // ===== MQTT SLOT-BASED INPUT =====

    public void HighlightSlot(int slotId)
    {
        string qrName = "qr" + slotId;
        GameObject obj = qrCodeManager?.GetBottleAtQR(qrName);

        if (obj == currentHighlightedObject) return;

        ClearHighlight();

        if (obj == null) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            originalColors[r] = r.material.color;
            r.material.color = highlightColor;
        }

        currentHighlightedObject = obj;
        if (showDebugLogs) Debug.Log($"[DEBUG] Highlighted slot {slotId}: {obj.name}");
    }

    public void ClearHighlight()
    {
        if (currentHighlightedObject == null) return;

        Renderer[] renderers = currentHighlightedObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r != null && originalColors.ContainsKey(r))
                r.material.color = originalColors[r];
        }

        originalColors.Clear();
        currentHighlightedObject = null;
    }

    public void GrabObjectAtSlot(int slotId)
    {
        if (isHoldingCup) return;

        string qrName = "qr" + slotId;
        GameObject obj = qrCodeManager?.GetBottleAtQR(qrName);

        if (obj != null)
        {
            ClearHighlight();
            PickUpObject(obj);
            if (showDebugLogs) Debug.Log($"[DEBUG] MQTT grabbed slot {slotId}: {obj.name}");
        }
        else
        {
            if (showDebugLogs) Debug.LogWarning($"[DEBUG] Nothing at slot {slotId} ({qrName})");
        }
    }

    public void OnMQTTPour(string pourTargetName)
    {
        if (!isHoldingCup || currentCup == null) return;
        if (isInPourState || pourEntryCoroutine != null) return;

        PourReceiver target = null;
        if (pourTargetName == "shaker")
            target = FindPourReceiverByType("shaker");
        else if (pourTargetName == "serving_glass")
            target = FindPourReceiverByType("serving");

        if (target == null)
        {
            if (showDebugLogs) Debug.LogWarning($"[DEBUG] No PourReceiver found for target: {pourTargetName}");
            return;
        }

        if (!target.CanReceiveLiquid())
        {
            if (showDebugLogs) Debug.Log($"[DEBUG] {target.containerName} is already full");
            return;
        }

        activePourTarget = target;
        pourEntryCoroutine = StartCoroutine(PourEntryAnimation(target));
    }

    public void OnMQTTShake()
    {
        if (showDebugLogs) Debug.Log("[DEBUG] Shaking!");
    }

    // ===== POUR MECHANICS =====

    PourReceiver FindPourReceiverByType(string containerType)
    {
        PourReceiver[] allReceivers = FindObjectsOfType<PourReceiver>();

        foreach (PourReceiver receiver in allReceivers)
        {
            if (containerType == "shaker" && receiver.containerType == PourReceiver.ContainerType.Shaker)
                return receiver;
            else if (containerType == "serving" && receiver.containerType == PourReceiver.ContainerType.ServingGlass)
                return receiver;
        }

        return null;
    }

    IEnumerator PourEntryAnimation(PourReceiver pourTarget)
    {
        Vector3 startPosition = currentCup.transform.position;
        Quaternion startRotation = currentCup.transform.rotation;
        Vector3 pourPosition = pourTarget.transform.position + Vector3.up * 0.25f;
        Quaternion pouringRot = Quaternion.Euler(0, 0, pourAngle);

        float elapsed = 0;
        float moveTime = 0.4f;
        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveTime;
            if (currentCup != null)
            {
                currentCup.transform.position = Vector3.Lerp(startPosition, pourPosition, t);
                currentCup.transform.rotation = Quaternion.Lerp(startRotation, pouringRot, t);
            }
            yield return null;
        }

        if (currentCup != null)
        {
            currentCup.transform.position = pourPosition;
            currentCup.transform.rotation = pouringRot;
        }

        if (liquidStreamPrefab != null && currentCup != null)
        {
            Transform spout = GetSpoutTransform(currentCup);
            Vector3 spoutPos = spout != null ? spout.position : currentCup.transform.position + currentCup.transform.up * 0.15f;
            activeLiquidStream = Instantiate(liquidStreamPrefab, spoutPos, Quaternion.Euler(90, 0, 0));
            activeLiquidStream.transform.parent = null;
            activeLiquidStream.GetComponent<ParticleSystem>()?.Play();
        }

        isInPourState = true;
        pourEntryCoroutine = null;
    }

    public void ExitPourState()
    {
        if (!isInPourState && pourEntryCoroutine == null) return;

        if (pourEntryCoroutine != null)
        {
            StopCoroutine(pourEntryCoroutine);
            pourEntryCoroutine = null;
        }

        isInPourState = false;

        if (activeLiquidStream != null)
        {
            activeLiquidStream.GetComponent<ParticleSystem>()?.Stop();
            Destroy(activeLiquidStream, 1.0f);
            activeLiquidStream = null;
        }

        if (currentCup != null)
            StartCoroutine(UntiltAnimation());

        activePourTarget = null;
    }

    IEnumerator UntiltAnimation()
    {
        float elapsed = 0;
        float tiltTime = 0.3f;
        Quaternion startRot = currentCup.transform.rotation;
        while (elapsed < tiltTime)
        {
            elapsed += Time.deltaTime;
            if (currentCup != null)
                currentCup.transform.rotation = Quaternion.Lerp(startRot, arCamera.rotation, elapsed / tiltTime);
            yield return null;
        }
    }

    // ===== HELPERS =====

    string GetLiquidType(string bottleName)
    {
        string name = bottleName.ToLower();

        if (name.Contains("midori")) return "Midori";
        if (name.Contains("vodka")) return "Vodka";
        if (name.Contains("bourbon")) return "Bourbon";
        if (name.Contains("gin")) return "Gin";
        if (name.Contains("scotch")) return "Scotch";
        if (name.Contains("rum")) return "Rum";
        if (name.Contains("whiskey")) return "Whiskey";
        if (name.Contains("shaker")) return "Mixed";

        return "Unknown";
    }

    Transform GetSpoutTransform(GameObject bottle)
    {
        Transform spout = bottle.transform.Find("SpoutPosition");

        if (spout == null)
            Debug.LogWarning($"[DEBUG] No SpoutPosition found on {bottle.name}");

        return spout;
    }

}
