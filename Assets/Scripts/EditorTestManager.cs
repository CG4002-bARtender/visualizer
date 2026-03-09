using UnityEngine;

public class EditorTestManager : MonoBehaviour
{
    [Header("Testing Mode")]
    public bool enableEditorTesting = true; // Toggle in Inspector
    
    [Header("Bottle Prefabs")]
    public GameObject[] bottlePrefabs; // Same 4 bottles as QRCodeManager
    
    [Header("Spawn Positions")]
    public Transform[] spawnPoints; // Where to place bottles in editor
    
    [Header("References")]
    public SimpleHandSimulator handSimulator;
    
    private GameObject[] spawnedBottles;
    
    void Start()
    {
        // Only run in Editor, not on phone
#if UNITY_EDITOR
        if (enableEditorTesting)
        {
            SpawnBottlesForTesting();
            SetupMouseTracking();
        }
#endif
    }
    
    void SpawnBottlesForTesting()
    {
        spawnedBottles = new GameObject[bottlePrefabs.Length];
        
        for (int i = 0; i < bottlePrefabs.Length; i++)
        {
            if (i < spawnPoints.Length && bottlePrefabs[i] != null)
            {
                // Spawn bottle at test position
                spawnedBottles[i] = Instantiate(
                    bottlePrefabs[i], 
                    spawnPoints[i].position, 
                    spawnPoints[i].rotation
                );
                
                Debug.Log($"✓ Spawned {bottlePrefabs[i].name} for testing");
            }
        }
    }
    
    void SetupMouseTracking()
    {
        Debug.Log("✓ Editor testing enabled - Use mouse to simulate hand tracking");
        Debug.Log("  • Move mouse to control crosshair");
        Debug.Log("  • Left-click to GRAB");
        Debug.Log("  • Right-click to RELEASE");
        Debug.Log("  • Middle-click to POUR");
    }
    
    void Update()
    {
#if UNITY_EDITOR
        if (enableEditorTesting)
        {
            SimulateHandTrackingWithMouse();
            SimulateButtonsWithMouse();
        }
#endif
    }
    
    void SimulateHandTrackingWithMouse()
    {
        // Get mouse position in screen space
        Vector3 mousePos = Input.mousePosition;
        
        // Convert to normalized coordinates (0-1)
        float normalizedX = mousePos.x / Screen.width;
        float normalizedY = mousePos.y / Screen.height;
        
        // Send to hand simulator (simulating red dot tracking)
        if (handSimulator != null)
        {
            handSimulator.OnHandPositionReceived(normalizedX, normalizedY);
        }
    }
    
    void SimulateButtonsWithMouse()
    {
        // Left-click = GRAB
        if (Input.GetMouseButtonDown(0))
        {
            handSimulator.OnGrabCupButton();
        }
        
        // Right-click = RELEASE
        if (Input.GetMouseButtonDown(1))
        {
            handSimulator.OnReleaseCupButton();
        }
        
        // Middle-click = POUR
        if (Input.GetMouseButtonDown(2))
        {
            handSimulator.OnPourButton();
        }
    }
}