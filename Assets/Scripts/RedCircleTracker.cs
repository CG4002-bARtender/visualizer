using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class RedCircleTracker : MonoBehaviour
{
    [Header("References")]
    public ARCameraManager arCameraManager;
    public SimpleHandSimulator handSimulator;

    [Header("Color Detection Settings")]
    [Range(0f, 1f)]
    public float greenThreshold = 0.6f;
    [Range(0f, 1f)]
    public float saturationThreshold = 0.4f;
    public int sampleStep = 6;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private bool redDetected = false;
    private Vector2 redPosition = Vector2.zero;
    private int frameSkip = 0;
    private int lastRedPixelCount = 0;

    void Start()
    {
        if (arCameraManager == null)
            arCameraManager = FindObjectOfType<ARCameraManager>();
    }

    void Update()
    {
        frameSkip++;
        if (frameSkip < 2) return;
        frameSkip = 0;

        DetectRedCircle();

        if (redDetected)
        {
            handSimulator?.OnHandPositionReceived(redPosition.x, redPosition.y);

            if (showDebugLogs)
                Debug.Log($"[GreenDot] ({redPosition.x:F3}, {redPosition.y:F3}) | pixels: {lastRedPixelCount}");
        }
        else
        {
            handSimulator?.OnRedDotLost();
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
            transformation = XRCpuImage.Transformation.MirrorY
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

                if (IsGreenPixel(r, g, b))
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

            redPosition.x = 1.0f - (centerX / width);
            redPosition.y = 1.0f - (centerY / height);
        }
        else
        {
            redDetected = false;
        }
    }

    bool IsGreenPixel(float r, float g, float b)
    {
        if (g < greenThreshold) return false;
        if (g <= r || g <= b) return false;

        float maxVal = Mathf.Max(r, g, b);
        float minVal = Mathf.Min(r, g, b);

        if (maxVal == 0) return false;

        float saturation = (maxVal - minVal) / maxVal;

        return saturation >= saturationThreshold;
    }

    public bool IsRedDetected() => redDetected;
    public Vector2 GetRedPosition() => redPosition;
    public int GetRedPixelCount() => lastRedPixelCount;
}
