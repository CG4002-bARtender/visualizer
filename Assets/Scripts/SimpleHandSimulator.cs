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
    private bool isPouring = false;
    private Coroutine pourCoroutine = null; 
    
    [Header("Pickup Settings")]
    public float maxPickupDistance = 0.5f; // 50cm range
    public float crosshairDetectionRadius = 0.3f; // How close to crosshair ray to detect
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
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
    
GameObject FindObjectAtCrosshair() //CHANGEDDDDD
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
    
    bool IsGrabbableObject(GameObject obj)
    {
        if (obj == null) return false;
        
        string name = obj.name;
        return name.Contains("AlcoBottle") ||
               name.Contains("EmptyGlass") ||
               name.Contains("Shaker");
    }
    
    string GetFriendlyName(string objectName)
    {
        if (objectName.Contains("AlcoBottleV1")) return "Bottle 1";
        if (objectName.Contains("AlcoBottleV7")) return "Bottle 2";
        if (objectName.Contains("EmptyGlass")) return "Glass";
        if (objectName.Contains("Shaker")) return "Shaker";
        return objectName;
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
    
    // NEW: Called when button is PRESSED DOWN
public void OnPourButtonDown()
{
    if (!isHoldingCup || currentCup == null)
    {
        Debug.Log("⚠ Not holding anything to pour");
        return;
    }
    
    if (isPouring) return;  // Already pouring
    
    isPouring = true;
    
    if (pourCoroutine != null)
    {
        StopCoroutine(pourCoroutine);
    }
    
    pourCoroutine = StartCoroutine(ContinuousPourAnimation());
    
    if (showDebugLogs) Debug.Log("✓ Started pouring (hold button)");
}

// NEW: Called when button is RELEASED
public void OnPourButtonUp()
{
    if (!isPouring) return;
    
    isPouring = false;
    
    if (pourCoroutine != null)
    {
        StopCoroutine(pourCoroutine);
        pourCoroutine = null;
    }
    
    // Return bottle to upright
    if (currentCup != null)
    {
        StartCoroutine(ReturnToUpright());
    }
    
    if (showDebugLogs) Debug.Log("✓ Stopped pouring (released button)");
}

// NEW: Continuous pour animation
IEnumerator ContinuousPourAnimation()
{
    Quaternion startRot = currentCup.transform.rotation;
    Quaternion pourRot = startRot * Quaternion.Euler(0, 0, pourAngle);
    
    // PHASE 1: Tilt to pour angle
    float elapsed = 0;
    while (elapsed < pourDuration && isPouring)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / pourDuration;
        
        if (currentCup != null)
        {
            currentCup.transform.rotation = Quaternion.Lerp(startRot, pourRot, t);
        }
        
        yield return null;
    }
    
    // PHASE 2: Hold pour angle while button is held
    while (isPouring)
    {
        // Keep bottle at pour angle
        if (currentCup != null)
        {
            Quaternion baseRot = arCamera.rotation;
            currentCup.transform.rotation = baseRot * Quaternion.Euler(0, 0, pourAngle);
        }
        
        yield return null;
    }
}

// NEW: Return to upright when button released
IEnumerator ReturnToUpright()
{
    if (currentCup == null) yield break;
    
    Quaternion currentRot = currentCup.transform.rotation;
    Quaternion uprightRot = arCamera.rotation;
    
    float elapsed = 0;
    while (elapsed < pourDuration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / pourDuration;
        
        if (currentCup != null)
        {
            currentCup.transform.rotation = Quaternion.Lerp(currentRot, uprightRot, t);
        }
        
        yield return null;
    }
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
                OnPourButtonDown();
                break;
                
            default:
                if (showDebugLogs) Debug.Log($"⚠ Unknown action: {action}");
                break;
        }
    }
}