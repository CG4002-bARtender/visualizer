using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class GreenCircleTracker : MonoBehaviour
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

    [Header("Grip Offset")]
    [Tooltip("Distance to shift from wrist centroid toward grip center (normalized 0–1 screen space)")]
    [Range(0f, 0.6f)]
    public float offsetDistance = 0.375f;
    [Tooltip("Direction to shift, in degrees. 0=right, 90=up, 180=left, 270=down")]
    [Range(0f, 360f)]
    public float offsetAngle = 45f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private bool greenDetected = false;
    private Vector2 greenPosition = Vector2.zero;
    private Vector2 rawCentroid = Vector2.zero;
    private int frameSkip = 0;
    private int lastGreenPixelCount = 0;

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

        DetectGreenCircle();

        if (greenDetected)
        {
            handSimulator?.OnHandPositionReceived(greenPosition.x, greenPosition.y);

        }
        else
        {
            handSimulator?.OnGreenDotLost();
        }
    }

    void DetectGreenCircle()
    {
        if (!arCameraManager.TryAcquireLatestCpuImage(out var image))
        {
            greenDetected = false;
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
        int greenPixelCount = 0;

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
                    greenPixelCount++;
                }
            }
        }

        lastGreenPixelCount = greenPixelCount;

        if (greenPixelCount > 5)
        {
            greenDetected = true;

            float centerX = totalX / greenPixelCount;
            float centerY = totalY / greenPixelCount;

            float rawX = 1.0f - (centerX / width);
            float rawY = 1.0f - (centerY / height);
            rawCentroid = new Vector2(rawX, rawY);

            float aspect = (float)width / height;
            float rad = offsetAngle * Mathf.Deg2Rad;
            greenPosition.x = Mathf.Clamp01(rawX + offsetDistance * Mathf.Cos(rad));
            greenPosition.y = Mathf.Clamp01(rawY + offsetDistance * Mathf.Sin(rad) * aspect);
        }
        else
        {
            greenDetected = false;
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

    public bool IsGreenDetected() => greenDetected;
    public Vector2 GetGreenPosition() => greenPosition;
    public Vector2 GetRawCentroid() => rawCentroid;
    public int GetGreenPixelCount() => lastGreenPixelCount;
}
