using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;
using TMPro;

public class RedCircleTracker : MonoBehaviour
{
    [Header("References")]
    public ARCameraManager arCameraManager;
    public SimpleHandSimulator handSimulator;
    public Image handCrosshair;
    public RectTransform handCrosshairRect;
    
    [Header("Color Detection Settings")]
    [Range(0f, 1f)]
    public float redThreshold = 0.6f;
    [Range(0f, 1f)]
    public float saturationThreshold = 0.4f;
    public int sampleStep = 6;
    
    [Header("Debug")]
    public TextMeshProUGUI debugText;
    public bool showDebugInfo = true;
    public Image debugRedDot;
    
    private bool redDetected = false;
    private Vector2 redPosition = Vector2.zero;
    private int frameSkip = 0;
    private int lastRedPixelCount = 0;
    
    void Start()
    {
        if (arCameraManager == null)
        {
            arCameraManager = FindObjectOfType<ARCameraManager>();
        }
        
        if (handCrosshairRect == null && handCrosshair != null)
        {
            handCrosshairRect = handCrosshair.GetComponent<RectTransform>();
        }
    }
    
    void Update()
    {
        frameSkip++;
        if (frameSkip < 2) return;
        frameSkip = 0;
        
        DetectRedCircle();
        
        if (redDetected)
        {
            if (handSimulator != null)
            {
                handSimulator.OnHandPositionReceived(redPosition.x, redPosition.y);
            }
            
            if (showDebugInfo && debugText != null)
            {
                debugText.text = $"✓ RED DETECTED!\n" +
                                $"Position: ({redPosition.x:F2}, {redPosition.y:F2})\n" +
                                $"Red Pixels: {lastRedPixelCount}\n" +
                                $"Threshold: {redThreshold:F1}";
            }
            
            if (debugRedDot != null)
            {
                debugRedDot.gameObject.SetActive(true);
                UpdateDebugDot(redPosition.x, redPosition.y);
            }
        }
        else
        {
            if (handSimulator != null)
            {
                handSimulator.OnRedDotLost();
            }
            
            if (showDebugInfo && debugText != null)
            {
                debugText.text = $"❌ NO RED FOUND\n" +
                                $"Red pixels: {lastRedPixelCount}\n" +
                                $"Threshold: {redThreshold:F1}\n" +
                                $"Sample step: {sampleStep}\n" +
                                $"Try:\n- Brighter red\n- Better lighting\n- Lower threshold";
            }
            
            if (debugRedDot != null)
            {
                debugRedDot.gameObject.SetActive(false);
            }
        }
    }
    
   void DetectRedCircle()
{
    if (!arCameraManager.TryAcquireLatestCpuImage(out var image))
    {
        redDetected = false;
        return;
    }
    
    var conversionParams = new XRCpuImage.ConversionParams
    {
        inputRect = new RectInt(0, 0, image.width, image.height),
        outputDimensions = new Vector2Int(image.width / 4, image.height / 4),
        outputFormat = TextureFormat.RGB24,
        transformation = XRCpuImage.Transformation.MirrorY  // Already mirroring Y
    };
    
    int size = image.GetConvertedDataSize(conversionParams);
    var buffer = new byte[size];
    
    unsafe
    {
        fixed (byte* ptr = buffer)
        {
            image.Convert(conversionParams, (System.IntPtr)ptr, size);
        }
    }
    
    image.Dispose();
    
    int width = conversionParams.outputDimensions.x;
    int height = conversionParams.outputDimensions.y;
    
    float totalX = 0;
    float totalY = 0;
    int redPixelCount = 0;
    
    for (int y = 0; y < height; y += sampleStep)
    {
        for (int x = 0; x < width; x += sampleStep)
        {
            int index = (y * width + x) * 3;
            
            if (index + 2 >= buffer.Length) continue;
            
            float r = buffer[index] / 255f;
            float g = buffer[index + 1] / 255f;
            float b = buffer[index + 2] / 255f;
            
            if (IsRedPixel(r, g, b))
            {
                totalX += x;
                totalY += y;
                redPixelCount++;
            }
        }
    }
    
    lastRedPixelCount = redPixelCount;
    
    if (redPixelCount > 5)
    {
        redDetected = true;
        
        float centerX = totalX / redPixelCount;
        float centerY = totalY / redPixelCount;
        
        // FIXED FOR LANDSCAPE MODE:
        // X: FLIP IT (opposite direction)
        // Camera X is right-to-left, Unity UI X is left-to-right
        redPosition.x = 1.0f - (centerX / width);
        
        // Y: Already correct from before
        // 0 = bottom, 1 = top
        redPosition.y = 1.0f - (centerY / height);
        
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"Red at ({redPosition.x:F2}, {redPosition.y:F2}) | Pixels: {redPixelCount}");
        }
    }
    else
    {
        redDetected = false;
    }
}
    
    bool IsRedPixel(float r, float g, float b)
    {
        if (r < redThreshold) return false;
        if (r <= g || r <= b) return false;
        
        float maxVal = Mathf.Max(r, g, b);
        float minVal = Mathf.Min(r, g, b);
        
        if (maxVal == 0) return false;
        
        float saturation = (maxVal - minVal) / maxVal;
        
        if (saturation < saturationThreshold) return false;
        
        return true;
    }
    
    void UpdateDebugDot(float normalizedX, float normalizedY)
    {
        if (debugRedDot == null) return;
        
        RectTransform dotRect = debugRedDot.GetComponent<RectTransform>();
        Canvas canvas = debugRedDot.canvas;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        
        // Convert normalized (0-1) to canvas coordinates
        // Unity UI: (0,0) = center, so we need to offset
        float targetX = (normalizedX - 0.5f) * canvasRect.sizeDelta.x;
        float targetY = (normalizedY - 0.5f) * canvasRect.sizeDelta.y;
        
        dotRect.anchoredPosition = new Vector2(targetX, targetY);
    }
    
    public bool IsRedDetected()
    {
        return redDetected;
    }
    
    public Vector2 GetRedPosition()
    {
        return redPosition;
    }
    
    public int GetRedPixelCount()
    {
        return lastRedPixelCount;
    }
}