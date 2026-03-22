using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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
    
    [Header("Red Dot Tracking")]
    public float crosshairSmoothSpeed = 25f; // How fast crosshair follows red dot
    
    [Header("Hand Position Settings")]
    public float holdDistanceFromCamera = 0.3f;
    public Vector3 handOffset = new Vector3(0.1f, -0.1f, 0);
    
    [Header("Pour Settings")]
    public float pourAngle = 60f;
    public float pourDuration = 0.5f;
    public GameObject liquidStreamPrefab;
    public float pourRayDistance = 0.5f;  // How far to check for containers below
    public LayerMask pourTargetLayer = -1;  // All layers

    private bool isPouring = false;
    private Coroutine pourCoroutine = null; 
    private GameObject activeLiquidStream;
    private PourReceiver currentPourTarget = null;

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
    
    // Red dot tracking state
    private Vector2 redDotScreenPos = new Vector2(0.5f, 0.5f); // Normalized 0-1
    private bool redDotDetected = false;
    
    // Current target
    private GameObject currentTarget = null;
    
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

    }
    
    void Update()
    {
        // Update held object position
        if (isHoldingCup && currentCup != null)
        {
            UpdateHeldObjectPosition();
        }
        
        // Update crosshair position from red dot
        if (redDotDetected && handCrosshairRect != null)
        {
            UpdateCrosshairPosition();
        }
        
        // Update what we're targeting and UI
        UpdateTargeting();
    }
    
    void UpdateHeldObjectPosition()
{
    if (currentCup == null || arCamera == null) return;

    // NEW: Use red dot position if available, otherwise use center
    Vector2 targetScreenPos = redDotDetected ? redDotScreenPos : new Vector2(0.5f, 0.5f);
    
    // Convert normalized screen position (0-1) to actual screen coordinates
    Vector3 screenPoint = new Vector3(
        targetScreenPos.x * Screen.width,
        targetScreenPos.y * Screen.height,
        holdDistanceFromCamera  // Distance from camera
    );
    
    // Convert screen point to world position
    Vector3 worldPosition = arCamera.GetComponent<Camera>().ScreenToWorldPoint(screenPoint);
    
    // Smooth movement
    currentCup.transform.position = Vector3.Lerp(
        currentCup.transform.position,
        worldPosition,
        Time.deltaTime * 10f
    );
    
    // Match camera rotation
    currentCup.transform.rotation = arCamera.rotation;
}
    
    void UpdateCrosshairPosition()
    {
        Canvas canvas = handCrosshair.canvas;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        
        // Convert normalized (0-1) red dot position to canvas coordinates
        float targetX = (redDotScreenPos.x - 0.5f) * canvasRect.sizeDelta.x;
        float targetY = (redDotScreenPos.y - 0.5f) * canvasRect.sizeDelta.y;
        
        Vector2 targetPos = new Vector2(targetX, targetY);
        
        // Smooth movement to target position
        Vector2 currentPos = handCrosshairRect.anchoredPosition;
        Vector2 newPos = Vector2.Lerp(currentPos, targetPos, Time.deltaTime * crosshairSmoothSpeed);
        handCrosshairRect.anchoredPosition = newPos;
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
                                         $"Distance: {distance:F2}m\n" +
                                         $"Red dot: ({redDotScreenPos.x:F2}, {redDotScreenPos.y:F2})";
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
                if (redDotDetected)
                {
                    targetInfoText.text = $"No target in range\n" +
                                         $"Red dot: ({redDotScreenPos.x:F2}, {redDotScreenPos.y:F2})";
                }
                else
                {
                    targetInfoText.text = "No red circle detected\n" +
                                         "Show red marker to camera";
                }
                targetInfoText.color = Color.white;
            }
        }
    }
    
GameObject FindObjectAtCrosshair() 
{
    if (handCrosshairRect == null || !redDotDetected) return null;
    
    Camera cam = arCamera.GetComponent<Camera>();
    if (cam == null) return null;
    
    // Cast ray from camera through crosshair
    Ray ray = cam.ScreenPointToRay(handCrosshairRect.position);
    
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
    
    System.Collections.IEnumerator PourAnimation()
    {
        if (showDebugLogs) Debug.Log("✓ Pouring...");
        
        Quaternion startRot = currentCup.transform.rotation;
        Quaternion pourRot = startRot * Quaternion.Euler(pourAngle, 0, 0);
        
        // Tilt to pour
        float elapsed = 0;
        while (elapsed < pourDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pourDuration;
            
            if (isHoldingCup && currentCup != null)
            {
                Quaternion baseRotation = arCamera.rotation;
                currentCup.transform.rotation = Quaternion.Lerp(baseRotation, baseRotation * Quaternion.Euler(pourAngle, 0, 0), t);
            }
            
            yield return null;
        }
        
        // Hold pour position
        yield return new WaitForSeconds(1.0f);
        
        // Tilt back
        elapsed = 0;
        while (elapsed < pourDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / pourDuration;
            
            if (isHoldingCup && currentCup != null)
            {
                Quaternion baseRotation = arCamera.rotation;
                Quaternion pouredRotation = baseRotation * Quaternion.Euler(pourAngle, 0, 0);
                currentCup.transform.rotation = Quaternion.Lerp(pouredRotation, baseRotation, t);
            }
            
            yield return null;
        }
        
        if (showDebugLogs) Debug.Log("✓ Pour complete!");
    }
    
    // ===== RED DOT TRACKING - Called by RedCircleTracker =====
    
    public void OnHandPositionReceived(float normalizedX, float normalizedY)
    {
        redDotScreenPos = new Vector2(normalizedX, normalizedY);
        redDotDetected = true;
    }
    
    public void OnRedDotLost()
    {
        redDotDetected = false;
    }
    
    // ===== NEW POUR BUTTON - CUTSCENE STYLE =====

    public void OnPourButton()
    {
        if (!isHoldingCup || currentCup == null)
        {
            Debug.Log("⚠ Not holding anything to pour");
            return;
        }
        
        if (isPouring)
        {
            Debug.Log("⚠ Already pouring!");
            return;
        }
        
        // Determine pour target based on what we're holding
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
        
        // Start cutscene animation
        StartCoroutine(PourCutsceneAnimation(pourTarget));
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

    IEnumerator PourCutsceneAnimation(PourReceiver pourTarget)
    {
        isPouring = true;
        
        Debug.Log($"🎬 Starting pour cutscene into {pourTarget.containerName}");
        
        ShowPourUI(true);
        UpdatePourUI(pourTarget);

        // Remember starting state (hand position)
        Vector3 startPosition = currentCup.transform.position;
        Quaternion startRotation = currentCup.transform.rotation;
        
        // Calculate pour position (above the target container)
        Vector3 pourPosition = pourTarget.transform.position + Vector3.up * 0.25f;
        
        // PHASE 1: Move bottle to pour position (1 second)
        float moveTime = 1.0f;
        float elapsed = 0;
        
        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveTime;
            
            if (currentCup != null)
            {
                currentCup.transform.position = Vector3.Lerp(startPosition, pourPosition, t);
                currentCup.transform.rotation = Quaternion.Lerp(startRotation, Quaternion.Euler(0, 0, 0), t);
            }
            
            yield return null;
        }
        
        // PHASE 2: Tilt to pour (0.5 seconds)
        // Quaternion uprightRot = currentCup.transform.rotation;
        // Quaternion pouringRot = uprightRot * Quaternion.Euler(0, 0, pourAngle);
        Quaternion uprightRot = Quaternion.identity;
        Quaternion pouringRot = Quaternion.Euler(0, 0, pourAngle);
        
        float tiltTime = 0.5f;
        elapsed = 0;
        
        while (elapsed < tiltTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / tiltTime;
            
            if (currentCup != null)
            {
                currentCup.transform.rotation = Quaternion.Lerp(uprightRot, pouringRot, t);
            }
            
            yield return null;
        }
        
        // PHASE 3: Spawn liquid particles and pour (2 seconds)
        if (liquidStreamPrefab != null && currentCup != null)
        {
            Transform spoutTransform = GetSpoutTransform(currentCup);
            Vector3 spoutWorldPos = spoutTransform != null ? spoutTransform.position : currentCup.transform.position + currentCup.transform.up * 0.15f;
            
            activeLiquidStream = Instantiate(liquidStreamPrefab, spoutWorldPos, Quaternion.Euler(90, 0, 0));
            activeLiquidStream.transform.parent = null;
            
            ParticleSystem ps = activeLiquidStream.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Play();
            }
        }
        
        // Pour liquid into container
        float pourTime = 2.0f;
        float pourAmount = 1.0f; // Full pour
        float pourRate = pourAmount / pourTime;
        elapsed = 0;
        
        while (elapsed < pourTime)
        {
            elapsed += Time.deltaTime;
            
            // Update liquid stream position
            if (activeLiquidStream != null && currentCup != null)
            {
                Transform spoutTransform = GetSpoutTransform(currentCup);
                if (spoutTransform != null)
                {
                    activeLiquidStream.transform.position = spoutTransform.position;
                }
            }
            
            // Add liquid to receiver
            if (pourTarget != null && pourTarget.CanReceiveLiquid())
            {
                string liquidType = GetLiquidType(currentCup.name);
                pourTarget.AddLiquid(liquidType, pourRate * Time.deltaTime);
            }
            
            yield return null;
        }
        
        // PHASE 4: Stop particles
        if (activeLiquidStream != null)
        {
            ParticleSystem ps = activeLiquidStream.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop();
            }
            
            Destroy(activeLiquidStream, 1.0f); // Destroy after particles finish
            activeLiquidStream = null;
        }
        
        // PHASE 5: Tilt back upright (0.5 seconds)
        elapsed = 0;
        Quaternion currentRot = currentCup.transform.rotation;

        while (elapsed < tiltTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / tiltTime;
            
            if (currentCup != null)
            {
                currentCup.transform.rotation = Quaternion.Lerp(currentRot, uprightRot, t);
            }
            
            yield return null;
        }
        
        // PHASE 6: Return bottle to hand position (1 second)
        elapsed = 0;
        Vector3 currentPosition = currentCup.transform.position;
        
        // Calculate current hand position
        Vector2 targetScreenPos = redDotDetected ? redDotScreenPos : new Vector2(0.5f, 0.5f);
        Vector3 screenPoint = new Vector3(
            targetScreenPos.x * Screen.width,
            targetScreenPos.y * Screen.height,
            holdDistanceFromCamera
        );
        Vector3 handPosition = arCamera.GetComponent<Camera>().ScreenToWorldPoint(screenPoint);
        
        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveTime;
            
            // Recalculate hand position each frame (in case hand moved)
            targetScreenPos = redDotDetected ? redDotScreenPos : new Vector2(0.5f, 0.5f);
            screenPoint = new Vector3(
                targetScreenPos.x * Screen.width,
                targetScreenPos.y * Screen.height,
                holdDistanceFromCamera
            );
            handPosition = arCamera.GetComponent<Camera>().ScreenToWorldPoint(screenPoint);
            
            if (currentCup != null)
            {
                currentCup.transform.position = Vector3.Lerp(currentPosition, handPosition, t);
                currentCup.transform.rotation = Quaternion.Lerp(currentCup.transform.rotation, arCamera.rotation, t);
            }
            
            yield return null;
        }
        
        ShowPourUI(false);

        // Done - bottle is back in hand, still holding
        isPouring = false;
        
        Debug.Log("✅ Pour cutscene complete! Bottle returned to hand.");
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

    // ===== MQTT/FAKE INPUT - For future use =====
    
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