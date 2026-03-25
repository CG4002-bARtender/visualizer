using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class SimpleHandSimulator : MonoBehaviour
{
    [Header("References")]
    public QRCodeManager qrCodeManager;
    public Transform arCamera;
    
    [Header("UI - Hand Targeting")]
    public Image handCrosshair; // UI Image for hand crosshair
    public RectTransform handCrosshairRect; // RectTransform for positioning
    public TextMeshProUGUI targetInfoText; // Shows what you're aiming at
    public Color canGrabColor = Color.green;
    public Color tooFarColor = Color.red;
    public Color holdingColor = Color.yellow;
    public Color noTargetColor = new Color(1, 1, 1, 0.5f); // White semi-transparent
    
    [Header("Held Object Settings")]
    public float holdDistance = 0.4f; // Distance in front of camera when holding
    
    [Header("Pour Settings")]
    public float pourAngle = 60f;
    public GameObject liquidStreamPrefab;
    public float pourRayDistance = 0.5f;  // How far to check for containers below
    public LayerMask pourTargetLayer = -1;  // All layers

    private bool isInPourState = false;
    private Coroutine pourEntryCoroutine = null;
    private GameObject activeLiquidStream;
    private PourReceiver activePourTarget = null;

    [Header("Pickup Settings")]
    public float maxPickupDistance = 0.5f; // 50cm range
    public float crosshairDetectionRadius = 0.3f; // How close to crosshair ray to detect
    
    [Header("Debug")]
    public bool showDebugLogs = true;

    [Header("UI References")]
    public GameObject pourProgressPanel;  // The panel
    public TextMeshProUGUI pourStatusText;  // "Pouring into: Shaker"
    public TextMeshProUGUI pourPercentText;  // "45%"
    public Image fillBar;  // Optional: visual fill bar
    
    // Object holding state
    private GameObject currentCup;
    private bool isHoldingCup = false;
    private Transform originalParent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;

    // Current target
    private GameObject currentTarget = null;

    // Highlight state
    [Header("Highlight Settings")]
    public Color highlightColor = new Color(1f, 0.85f, 0f, 1f); // Gold tint
    private GameObject currentHighlightedObject = null;
    private Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();
    
    void Start()
    {
        // Auto-find camera if not assigned
        if (arCamera == null)
        {
            arCamera = Camera.main.transform;
        }
        
        // Auto-assign crosshair rect if not set
        if (handCrosshairRect == null && handCrosshair != null)
        {
            handCrosshairRect = handCrosshair.GetComponent<RectTransform>();
        }
        ShowPourUI(false);

        // Pin crosshair to screen center
        if (handCrosshairRect != null)
        {
            handCrosshairRect.anchoredPosition = Vector2.zero;
        }
    }
    
    void Update()
    {
        // Update held object position
        if (isHoldingCup && currentCup != null)
        {
            UpdateHeldObjectPosition();
        }

        // Update what we're targeting and UI
        UpdateTargeting();

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

            UpdatePourUI(activePourTarget);
        }
    }
    
    void UpdateHeldObjectPosition()
    {
        if (currentCup == null || arCamera == null) return;

        // Pin bottle to screen center, holdDistance in front of camera
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
    
    void UpdateTargeting()
    {
        if (handCrosshair == null) return;
        
        // Find what the crosshair is pointing at
        GameObject target = FindObjectAtCrosshair();
        currentTarget = target;
        
        // Update UI based on state
        if (isHoldingCup)
        {
            // Currently holding something - show yellow
            handCrosshair.color = holdingColor;
            if (targetInfoText != null)
            {
                targetInfoText.text = $"HOLDING: {GetFriendlyName(currentCup.name)}";
                targetInfoText.color = holdingColor;
            }
        }
        else if (target != null)
        {
            // Aiming at a grabbable object
            float distance = Vector3.Distance(arCamera.position, target.transform.position);
            
            if (distance <= maxPickupDistance)
            {
                // In range - GREEN (can grab!)
                handCrosshair.color = canGrabColor;
                if (targetInfoText != null)
                {
                    targetInfoText.text = $"CAN GRAB: {GetFriendlyName(target.name)}\n" +
                                         $"Distance: {distance:F2}m";
                    targetInfoText.color = canGrabColor;
                }
            }
            else
            {
                // Too far - RED
                handCrosshair.color = tooFarColor;
                if (targetInfoText != null)
                {
                    targetInfoText.text = $"TOO FAR: {GetFriendlyName(target.name)}\n" +
                                         $"Distance: {distance:F2}m (max {maxPickupDistance}m)";
                    targetInfoText.color = tooFarColor;
                }
            }
        }
        else
        {
            // Not aiming at anything - WHITE
            handCrosshair.color = noTargetColor;
            if (targetInfoText != null)
            {
                targetInfoText.text = "No target in range";
                targetInfoText.color = Color.white;
            }
        }
    }
    
GameObject FindObjectAtCrosshair()
{
    Camera cam = arCamera.GetComponent<Camera>();
    if (cam == null) return null;

    // Cast ray from camera through screen center
    Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));
    
    GameObject[] allObjects = FindObjectsOfType<GameObject>();
    
    GameObject nearest = null;
    float nearestDistanceToRay = float.MaxValue;  // Changed: now tracking distance to RAY, not camera
    
    foreach (GameObject obj in allObjects)
    {
        if (!IsGrabbableObject(obj)) continue;
        if (isHoldingCup && obj == currentCup) continue;
        
        Vector3 objectPos = obj.transform.position;
        
        // Calculate distance along ray
        float rayDistance = Vector3.Dot(objectPos - ray.origin, ray.direction);
        if (rayDistance <= 0) continue;  // Behind camera
        
        // Find closest point on ray to object
        Vector3 closestPoint = ray.GetPoint(rayDistance);
        
        // Distance from object to the ray (how "aligned" it is with crosshair)
        float distanceToRay = Vector3.Distance(objectPos, closestPoint);
        
        // Select the one CLOSEST TO THE RAY (nearest to where you're pointing)
        if (distanceToRay < nearestDistanceToRay)
        {
            nearest = obj;
            nearestDistanceToRay = distanceToRay;
        }
    }
    
    return nearest;  // Returns object closest to crosshair, ignoring camera distance
}
    
    bool IsGrabbableObject(GameObject obj) //NEW: changed to use Grabbable tag
    {
        if (obj == null) return false;
        
        // Check if object has the "Grabbable" tag
        return obj.CompareTag("Grabbable");
    }
    
    string GetFriendlyName(string objectName)
{
    string name = objectName.ToLower();
    
    // Alcohol bottles
    if (name.Contains("midori")) return "Midori";
    if (name.Contains("vodka")) return "Vodka";
    if (name.Contains("bourbon")) return "Bourbon";
    if (name.Contains("gin")) return "Gin";
    if (name.Contains("scotch")) return "Scotch";
    if (name.Contains("darkrum") || name.Contains("dark rum")) return "Dark Rum";
    if (name.Contains("rye") && name.Contains("whiskey")) return "Rye Whiskey";
    if (name.Contains("whiskey")) return "Whiskey";
    if (name.Contains("purple") && name.Contains("liqueur")) return "Purple Liqueur";
    
    // Legacy names (for backward compatibility)
    if (name.Contains("alcobottlev1")) return "Vodka Bottle";
    if (name.Contains("alcobottlev7")) return "Rum Bottle";
    
    // Containers
    if (name.Contains("shaker")) return "Shaker";
    
    // Glasses
    if (name.Contains("emptyglass_midorisour")) return "Midori Sour Glass";
    if (name.Contains("emptyglass_godfather")) return "Godfather Glass";
    if (name.Contains("emptyglass_irishcoffee")) return "Irish Coffee Glass";
    if (name.Contains("emptyglass_martini")) return "Martini Glass";
    if (name.Contains("emptyglass_oldfashioned")) return "Old Fashioned Glass";
    if (name.Contains("emptyglass_tuxedo")) return "Tuxedo Glass";
    if (name.Contains("emptyglass_vodka")) return "Vodka Glass";
    if (name.Contains("emptyglass_neat")) return "Neat Glass";
    if (name.Contains("emptyglass")) return "Empty Glass";
    
    // Fallback: clean up the name
    string cleaned = objectName.Replace("(Clone)", "").Trim();
    return cleaned;
}

    // ===== PUBLIC METHODS - Called by Buttons =====
    
    public void OnGrabCupButton()
    {
        if (isHoldingCup)
        {
            if (showDebugLogs) Debug.Log("⚠ Already holding something!");
            return;
        }
        
        // Grab the object we're currently targeting
        if (currentTarget != null)
        {
            PickUpObject(currentTarget);
            if (showDebugLogs) Debug.Log($"🎯 Grabbed: {currentTarget.name}");
        }
        else
        {
            if (showDebugLogs) Debug.Log("⚠ No object targeted! Point red dot at an object.");
        }
    }
    
    public void OnReleaseCupButton()
    {
        ExitPourState();
        ClearHighlight();

        if (!isHoldingCup || currentCup == null)
        {
            if (showDebugLogs) Debug.Log("⚠ Not holding anything!");
            return;
        }
        
        // Return to original position
        if (originalParent != null)
        {
            currentCup.transform.parent = originalParent;
            currentCup.transform.localPosition = originalPosition;
            currentCup.transform.localRotation = originalRotation;
            
            if (showDebugLogs) Debug.Log($"✓ Returned {currentCup.name} to original QR code");
        }
        else
        {
            // Fallback: drop at nearest QR position
            Vector3 nearestTable = qrCodeManager.GetNearestQRPosition(currentCup.transform.position);
            currentCup.transform.position = nearestTable;
            currentCup.transform.rotation = Quaternion.identity;
            
            if (showDebugLogs) Debug.Log($"✓ Dropped {currentCup.name} at nearest QR");
        }
        
        isHoldingCup = false;
        currentCup = null;
    }
    
    
    void PickUpObject(GameObject obj)
    {
        currentCup = obj;
        
        // Remember original position to return to
        originalParent = obj.transform.parent;
        originalPosition = obj.transform.localPosition;
        originalRotation = obj.transform.localRotation;
        
        // Unparent from QR code so it can follow hand
        obj.transform.parent = null;
        
        isHoldingCup = true;
        
        if (showDebugLogs) Debug.Log($"✓ Picked up {obj.name}!");
    }
    
    // ===== NEW POUR BUTTON - CUTSCENE STYLE =====

    public void OnPourButton()
    {
        if (!isHoldingCup || currentCup == null)
        {
            Debug.Log("⚠ Not holding anything to pour");
            return;
        }

        if (isInPourState || pourEntryCoroutine != null) return;

        PourReceiver pourTarget = DeterminePourTarget();

        if (pourTarget == null)
        {
            Debug.Log("⚠ No valid pour target found");
            return;
        }

        if (!pourTarget.CanReceiveLiquid())
        {
            Debug.Log($"⚠ {pourTarget.containerName} is already full!");
            return;
        }

        activePourTarget = pourTarget;
        pourEntryCoroutine = StartCoroutine(PourEntryAnimation(pourTarget));
    }

    PourReceiver DeterminePourTarget()
    {
        if (currentCup == null) return null;
        
        string heldItemName = currentCup.name.ToLower();
        
        // If holding shaker → pour into serving glass
        if (heldItemName.Contains("shaker"))
        {
            return FindPourReceiverByType("serving");
        }
        // If holding bottle → pour into shaker (or serving glass if no shaker needed)
        else
        {
            // Try to find shaker first
            PourReceiver shaker = FindPourReceiverByType("shaker");
            
            if (shaker == null)
            {
                Debug.LogWarning("⚠ No shaker found!");
            }
            
            return shaker;
        }
    }

    PourReceiver FindPourReceiverByType(string containerType)
    {
        PourReceiver[] allReceivers = FindObjectsOfType<PourReceiver>();
        
        foreach (PourReceiver receiver in allReceivers)
        {
            if (containerType == "shaker" && receiver.containerType == PourReceiver.ContainerType.Shaker)
            {
                return receiver;
            }
            else if (containerType == "serving" && receiver.containerType == PourReceiver.ContainerType.ServingGlass)
            {
                return receiver;
            }
        }
        
        return null;
    }

    IEnumerator PourEntryAnimation(PourReceiver pourTarget)
    {
        Vector3 startPosition = currentCup.transform.position;
        Quaternion startRotation = currentCup.transform.rotation;
        Vector3 pourPosition = pourTarget.transform.position + Vector3.up * 0.25f;
        Quaternion pouringRot = Quaternion.Euler(0, 0, pourAngle);

        // Move to target and tilt simultaneously (0.4s)
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

        // Start particles
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
        ShowPourUI(true);
        UpdatePourUI(pourTarget);
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
        ShowPourUI(false);
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

    // Helper: determine which liquid is in this bottle
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
        {
            Debug.LogWarning($"⚠ No SpoutPosition found on {bottle.name}!");
        }
        
        return spout;
    }

    // ===== MQTT SLOT-BASED INPUT =====

    // Highlight the object at a given QR slot, clearing any previous highlight
    public void HighlightSlot(int slotId)
    {
        string qrName = "qr" + slotId;
        GameObject obj = qrCodeManager?.GetBottleAtQR(qrName);

        // Nothing changed
        if (obj == currentHighlightedObject) return;

        ClearHighlight();

        if (obj == null) return;

        // Save original colors and apply tint to all renderers
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            // renderer.material creates a per-instance copy, safe to modify
            originalColors[r] = r.material.color;
            r.material.color = highlightColor;
        }

        currentHighlightedObject = obj;
        if (showDebugLogs) Debug.Log($"✨ Highlighted slot {slotId}: {obj.name}");
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

    // Grab the object physically at QR slot N (0,1,2,3,4)
    public void GrabObjectAtSlot(int slotId)
    {
        if (isHoldingCup) return;

        string qrName = "qr" + slotId;
        GameObject obj = qrCodeManager?.GetBottleAtQR(qrName);

        if (obj != null)
        {
            ClearHighlight();
            PickUpObject(obj);
            if (showDebugLogs) Debug.Log($"🤖 MQTT grabbed slot {slotId}: {obj.name}");
        }
        else
        {
            if (showDebugLogs) Debug.LogWarning($"⚠ Nothing at slot {slotId} ({qrName})");
        }
    }

    // Pour into a specific target type ("shaker" or "serving_glass") — called by MQTT state 3
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
            if (showDebugLogs) Debug.LogWarning($"⚠ No PourReceiver found for target: {pourTargetName}");
            return;
        }

        if (!target.CanReceiveLiquid())
        {
            if (showDebugLogs) Debug.Log($"⚠ {target.containerName} is already full");
            return;
        }

        activePourTarget = target;
        pourEntryCoroutine = StartCoroutine(PourEntryAnimation(target));
    }

    // Shake the held shaker
    public void OnMQTTShake()
    {
        if (showDebugLogs) Debug.Log("🍸 Shaking!");
        // Shake animation can be added here
    }

    // ===== MQTT/FAKE INPUT - Legacy string-based =====

    public void SimulateFakeInput(string action)
    {
        if (showDebugLogs) Debug.Log($"📥 Received action: {action}");
        
        switch (action.ToLower())
        {
            case "grab":
            case "hold":
            case "pickup":
            case "holding":
                OnGrabCupButton();
                break;
                
            case "release":
            case "putdown":
            case "drop":
                OnReleaseCupButton();
                break;
                
            case "pour":
            case "pouring":
                OnPourButton();
                break;
                
            default:
                if (showDebugLogs) Debug.Log($"⚠ Unknown action: {action}");
                break;
        }
    }

    //UI Console Messages
    void ShowPourUI(bool show)
    {
        if (pourProgressPanel != null)
        {
            pourProgressPanel.SetActive(show);
        }
    }

    void UpdatePourUI(PourReceiver target)
    {
        if (target == null)
        {
            ShowPourUI(false);
            return;
        }
        
        ShowPourUI(true);
        
        // Update status text
        if (pourStatusText != null)
        {
            pourStatusText.text = $"Pouring into: {target.containerName}";
        }
        
        // Update percentage text
        if (pourPercentText != null)
        {
            int percent = Mathf.RoundToInt(target.fillAmount * 100);
            pourPercentText.text = $"{percent}%";
            
            // Change color based on fill
            if (target.fillAmount >= 1.0f)
            {
                pourPercentText.color = Color.red;  // Full!
            }
            else if (target.fillAmount >= 0.75f)
            {
                pourPercentText.color = new Color(1f, 0.6f, 0f);  // Orange
            }
            else
            {
                pourPercentText.color = Color.yellow;  // Normal
            }
        }
        
        // Update fill bar (optional)
        if (fillBar != null)
        {
            fillBar.fillAmount = target.fillAmount;
        }
    }
}