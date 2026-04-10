using UnityEngine;
using System.Collections;

public class SimpleHandSimulator : MonoBehaviour
{
    [Header("References")]
    public QRCodeManager qrCodeManager;
    public Transform arCamera;
    public SplitScreenManager splitScreen;

    [Header("Held Object Settings")]
    public float holdDistance = 0.4f; // Distance in front of camera when holding

    [Header("Pour Settings")]
    public float pourAngle = 60f;
    public float pourActiveDuration = 2f;
    public float pourHeightOffset = 0.25f;
    public GameObject liquidStreamPrefab;

    private bool isInPourState = false;
    private Coroutine pourEntryCoroutine = null;
    private Coroutine pourSequenceCoroutine = null;
    private GameObject activeLiquidStream;
    private PourReceiver activePourTarget = null;
    private System.Action pourCompleteCallback = null;
    private Color pendingLiquidColor = Color.white;

    [Header("Shake Settings")]
    public float shakeMoveTime = 0.5f;
    public float shakeActiveDuration = 2f;
    public float shakeReturnTime = 0.4f;
    public float shakeFrequency = 3f;
    public float shakeAmplitude = 0.08f;
    public float shakeTiltAngle = 25f;

    private bool isInShakeState = false;
    private Coroutine shakeSequenceCoroutine = null;
    private System.Action shakeCompleteCallback = null;

    [Header("Debug")]
    public bool showDebugLogs = true;

    // Object holding state
    private GameObject currentCup;
    private bool isHoldingCup = false;
    private Transform originalParent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    // Green dot tracking state
    private Vector2 greenDotScreenPos = new Vector2(0.5f, 0.5f); // Normalized 0-1, defaults to center
    private bool greenDotDetected = false;

    // Highlight state
    [Header("Highlight Settings")]
    public Color highlightColor = new Color(1f, 0.85f, 0f, 1f); // Gold tint
    private GameObject currentHighlightedObject = null;
    private System.Collections.Generic.Dictionary<Renderer, Color> originalColors = new System.Collections.Generic.Dictionary<Renderer, Color>();

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main.transform;
        if (splitScreen == null)
            splitScreen = FindObjectOfType<SplitScreenManager>();

    }

    void Update()
    {
        if (isHoldingCup && currentCup != null && !isInShakeState && pourSequenceCoroutine == null)
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

        Vector2 targetScreenPos = greenDotDetected ? greenDotScreenPos : new Vector2(0.5f, 0.5f);
        bool isStereo = splitScreen != null && splitScreen.enableStereo;
        float screenX = targetScreenPos.x * Screen.width * (isStereo ? 0.5f : 1f);

        Vector3 screenPoint = new Vector3(
            screenX,
            targetScreenPos.y * Screen.height,
            holdDistance
        );

        Vector3 worldPosition = arCamera.GetComponent<Camera>().ScreenToWorldPoint(screenPoint);

        currentCup.transform.position = Vector3.Lerp(
            currentCup.transform.position,
            worldPosition,
            Time.deltaTime * 10f
        );

        currentCup.transform.rotation = Quaternion.Slerp(
            currentCup.transform.rotation,
            Quaternion.identity,
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

    public void OnMQTTPour(string pourTargetName, Color liquidColor, System.Action onComplete = null)
    {
        if (!isHoldingCup || currentCup == null) return;
        if (isInPourState || pourEntryCoroutine != null || pourSequenceCoroutine != null) return;

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
        pourCompleteCallback = onComplete;
        pendingLiquidColor = liquidColor;
        pourSequenceCoroutine = StartCoroutine(PourSequence(target));
    }

    static Color GetIngredientColor(string ingredient)
    {
        switch (ingredient?.ToLower().Replace(" ", ""))
        {
            case "gin":           return new Color(0.85f, 0.95f, 1.00f, 0.8f);  // clear/pale blue
            case "purpleliqueur": return new Color(0.55f, 0.10f, 0.80f, 0.8f);  // purple
            case "scotch":        return new Color(0.75f, 0.45f, 0.10f, 0.8f);  // amber
            case "bourbon":       return new Color(0.70f, 0.35f, 0.05f, 0.8f);  // dark amber
            case "darkrum":       return new Color(0.45f, 0.20f, 0.05f, 0.8f);  // dark brown
            case "midori":        return new Color(0.10f, 0.75f, 0.20f, 0.8f);  // bright green
            case "ryewhiskey":    return new Color(0.80f, 0.50f, 0.10f, 0.8f);  // golden
            case "vodka":         return new Color(0.90f, 0.95f, 1.00f, 0.8f);  // clear/white
            case "whiskey":       return new Color(0.75f, 0.45f, 0.10f, 0.8f);  // amber
            case "mixed":         return new Color(0.70f, 0.60f, 0.50f, 0.8f);  // blended
            default:              return new Color(0.80f, 0.80f, 0.90f, 0.8f);  // fallback
        }
    }

    public static Color GetIngredientColorByName(string ingredient) => GetIngredientColor(ingredient);

    public void OnMQTTShake(System.Action onComplete = null)
    {
        if (!isHoldingCup || currentCup == null) return;
        if (isInShakeState || shakeSequenceCoroutine != null) return;

        shakeCompleteCallback = onComplete;
        shakeSequenceCoroutine = StartCoroutine(ShakeSequence());
    }

    public void ExitShakeState()
    {
        if (!isInShakeState && shakeSequenceCoroutine == null) return;

        if (shakeSequenceCoroutine != null)
        {
            StopCoroutine(shakeSequenceCoroutine);
            shakeSequenceCoroutine = null;
        }

        shakeCompleteCallback = null;
        isInShakeState = false;
    }

    IEnumerator ShakeSequence()
    {
        isInShakeState = true;

        // Phase 1: smoothly move to center screen
        Vector3 startPos = currentCup.transform.position;
        Quaternion startRot = currentCup.transform.rotation;
        float elapsed = 0f;

        while (elapsed < shakeMoveTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / shakeMoveTime);
            Vector3 centerPos = GetCenterScreenPosition();
            if (currentCup != null)
            {
                currentCup.transform.position = Vector3.Lerp(startPos, centerPos, t);
                currentCup.transform.rotation = Quaternion.Lerp(startRot, Quaternion.identity, t);
            }
            yield return null;
        }

        // Phase 2: arc shake (up/down arc with forward/back tilt)
        elapsed = 0f;
        while (elapsed < shakeActiveDuration)
        {
            elapsed += Time.deltaTime;
            float wave = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f);
            Vector3 center = GetCenterScreenPosition();
            if (currentCup != null)
            {
                currentCup.transform.position = center
                    + arCamera.up      * (shakeAmplitude * wave)
                    + arCamera.forward * (shakeAmplitude * 0.4f * -wave); // arc: forward on down-stroke
                currentCup.transform.rotation = Quaternion.Euler(shakeTiltAngle * wave, 0f, 0f);
            }
            yield return null;
        }

        // Phase 3: smoothly return to hand
        if (currentCup != null)
        {
            Vector3 shakeEndPos = currentCup.transform.position;
            Quaternion shakeEndRot = currentCup.transform.rotation;
            elapsed = 0f;
            while (elapsed < shakeReturnTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / shakeReturnTime);
                if (currentCup != null)
                {
                    currentCup.transform.position = Vector3.Lerp(shakeEndPos, GetCurrentHandPosition(), t);
                    currentCup.transform.rotation = Quaternion.Lerp(shakeEndRot, arCamera.rotation, t);
                }
                yield return null;
            }
        }

        isInShakeState = false;
        shakeSequenceCoroutine = null;

        System.Action callback = shakeCompleteCallback;
        shakeCompleteCallback = null;
        callback?.Invoke();
    }

    Vector3 GetCenterScreenPosition()
    {
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.45f, holdDistance);
        return arCamera.GetComponent<Camera>().ScreenToWorldPoint(screenCenter);
    }

    Vector3 GetCurrentHandPosition()
    {
        Vector2 targetScreenPos = greenDotDetected ? greenDotScreenPos : new Vector2(0.5f, 0.5f);
        bool isStereo = splitScreen != null && splitScreen.enableStereo;
        float screenX = targetScreenPos.x * Screen.width * (isStereo ? 0.5f : 1f);
        Vector3 screenPoint = new Vector3(screenX, targetScreenPos.y * Screen.height, holdDistance);
        return arCamera.GetComponent<Camera>().ScreenToWorldPoint(screenPoint);
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

    IEnumerator PourSequence(PourReceiver pourTarget)
    {
        // Phase 1: tilt and move to pour position
        yield return StartCoroutine(PourEntryAnimation(pourTarget));

        // Phase 2: active pour for fixed duration
        yield return new WaitForSeconds(pourActiveDuration);

        // Phase 3: stop particles and untilt
        isInPourState = false;

        if (activeLiquidStream != null)
        {
            activeLiquidStream.GetComponent<ParticleSystem>()?.Stop();
            Destroy(activeLiquidStream, 1.0f);
            activeLiquidStream = null;
        }

        activePourTarget = null;

        if (currentCup != null)
            yield return StartCoroutine(UntiltAnimation());

        pourSequenceCoroutine = null;

        // Signal completion to server
        System.Action callback = pourCompleteCallback;
        pourCompleteCallback = null;
        callback?.Invoke();
    }

    IEnumerator PourEntryAnimation(PourReceiver pourTarget)
    {
        Vector3 startPosition = currentCup.transform.position;
        Quaternion startRotation = currentCup.transform.rotation;
        Vector3 pourPosition = pourTarget.transform.position + Vector3.up * pourHeightOffset;
        // Tilt left in world space (roll around world Z axis)
        Quaternion pouringRot = Quaternion.Euler(0f, 0f, pourAngle);

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

            ParticleSystem ps = activeLiquidStream.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.startColor = pendingLiquidColor;
                ps.Play();
            }
        }

        isInPourState = true;
        pourEntryCoroutine = null;
    }

    public void ExitPourState()
    {
        if (!isInPourState && pourEntryCoroutine == null && pourSequenceCoroutine == null) return;

        if (pourSequenceCoroutine != null)
        {
            StopCoroutine(pourSequenceCoroutine);
            pourSequenceCoroutine = null;
        }

        if (pourEntryCoroutine != null)
        {
            StopCoroutine(pourEntryCoroutine);
            pourEntryCoroutine = null;
        }

        pourCompleteCallback = null;
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
                currentCup.transform.rotation = Quaternion.Lerp(startRot, Quaternion.identity, elapsed / tiltTime);
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

    // ===== GREEN DOT TRACKING - Called by GreenCircleTracker =====

    public void OnHandPositionReceived(float normalizedX, float normalizedY)
    {
        greenDotScreenPos = new Vector2(normalizedX, normalizedY);
        greenDotDetected = true;
    }

    public void OnGreenDotLost()
    {
        greenDotDetected = false;
    }

}
