using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System;
using System.Runtime.InteropServices;

/// <summary>
/// Hand tracking via Apple Vision framework.
///
/// Grabs CPU image from ARKit's rear camera each frame via ARCameraManager,
/// passes the pixel data to a native Vision function for hand pose detection.
/// No separate AVCaptureSession — zero camera contention.
/// </summary>
public class HandTrackingManager : MonoBehaviour
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int DetectHandPose(IntPtr pixelData, int width, int height,
                                              int bytesPerRow, int orientation, float[] outData);
#endif

    [Header("AR Camera")]
    [Tooltip("Auto-found if left empty.")]
    public ARCameraManager cameraManager;

    [Header("Detection")]
    [Tooltip("Max detections per second. Vision is expensive — 10-15 is plenty.")]
    public float detectionRate = 12f;

    [Header("Depth Estimation")]
    public float baseDepth = 0.4f;
    public float referenceJointSpan = 0.12f;

    [Header("Confidence")]
    public float confidenceThreshold = 0.3f;

    [Header("Smoothing")]
    public float positionSmoothSpeed = 15f;
    public float rotationSmoothSpeed = 10f;

    // Public API
    [HideInInspector] public Vector3 WristWorldPosition { get; private set; }
    [HideInInspector] public Quaternion WristWorldRotation { get; private set; }
    [HideInInspector] public bool IsTracking { get; private set; }
    [HideInInspector] public float Confidence { get; private set; }

    private Vector3 smoothedPosition;
    private Quaternion smoothedRotation = Quaternion.identity;
    private Camera mainCam;
    private float[] nativeResult = new float[9];
    private float lastDetectionTime;

    // Reusable pixel buffer to avoid per-frame allocation
    private IntPtr pixelBuffer = IntPtr.Zero;
    private int pixelBufferSize = 0;

    void Start()
    {
        mainCam = Camera.main;

#if UNITY_IOS && !UNITY_EDITOR
        if (cameraManager == null)
            cameraManager = FindObjectOfType<ARCameraManager>();

        if (cameraManager == null)
            Debug.LogError("[HandTrackingManager] No ARCameraManager found!");
        else
            Debug.Log("[HandTrackingManager] Ready — using ARKit camera frames for hand detection");
#else
        Debug.Log("[HandTrackingManager] Editor mode — use mouse (left-click) for debug.");
#endif
    }

    void OnDestroy()
    {
        if (pixelBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(pixelBuffer);
            pixelBuffer = IntPtr.Zero;
        }
    }

    void Update()
    {
#if UNITY_IOS && !UNITY_EDITOR
        // Throttle detection to save CPU
        if (Time.time - lastDetectionTime < 1f / detectionRate) return;
        lastDetectionTime = Time.time;

        RunDetection();
#elif UNITY_EDITOR
        UpdateEditorSimulation();
#endif
    }

#if UNITY_IOS && !UNITY_EDITOR
    private void RunDetection()
    {
        if (cameraManager == null) return;

        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage cpuImage))
            return;

        // Downscale for performance — 480x360 is plenty for hand landmarks
        int targetWidth = cpuImage.width / 4;
        int targetHeight = cpuImage.height / 4;

        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
            outputDimensions = new Vector2Int(targetWidth, targetHeight),
            outputFormat = TextureFormat.BGRA32,
            transformation = XRCpuImage.Transformation.None
        };

        int dataSize = cpuImage.GetConvertedDataSize(conversionParams);

        // Reuse buffer across frames
        if (dataSize > pixelBufferSize)
        {
            if (pixelBuffer != IntPtr.Zero)
                Marshal.FreeHGlobal(pixelBuffer);
            pixelBuffer = Marshal.AllocHGlobal(dataSize);
            pixelBufferSize = dataSize;
        }

        cpuImage.Convert(conversionParams, pixelBuffer, dataSize);
        cpuImage.Dispose();

        int bytesPerRow = targetWidth * 4; // BGRA = 4 bytes per pixel
        int orientation = GetVisionOrientation();

        int detected = DetectHandPose(pixelBuffer, targetWidth, targetHeight,
                                       bytesPerRow, orientation, nativeResult);

        if (detected == 1)
        {
            ProcessHandData(
                new Vector2(nativeResult[0], nativeResult[1]),
                new Vector2(nativeResult[2], nativeResult[3]),
                new Vector2(nativeResult[4], nativeResult[5]),
                new Vector2(nativeResult[6], nativeResult[7]),
                nativeResult[8]
            );
        }
        else
        {
            Confidence = 0f;
            IsTracking = false;
        }
    }

    private int GetVisionOrientation()
    {
        switch (Input.deviceOrientation)
        {
            case DeviceOrientation.Portrait:            return 6; // kCGImagePropertyOrientationRight
            case DeviceOrientation.PortraitUpsideDown:  return 8; // kCGImagePropertyOrientationLeft
            case DeviceOrientation.LandscapeLeft:       return 1; // kCGImagePropertyOrientationUp
            case DeviceOrientation.LandscapeRight:      return 3; // kCGImagePropertyOrientationDown
            default:                                    return 6;
        }
    }
#endif

    private void ProcessHandData(Vector2 wrist, Vector2 indexMCP, Vector2 middleMCP, Vector2 thumbTip, float confidence)
    {
        Confidence = confidence;

        if (confidence < confidenceThreshold)
        {
            IsTracking = false;
            return;
        }

        IsTracking = true;

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        // Vision: (0,0) bottom-left. ViewportPointToRay: (0,0) bottom-left. No Y-flip needed.
        // Rear camera is not mirrored — no X-flip needed either.
        Vector2 vWrist = wrist;
        Vector2 vMiddle = middleMCP;
        Vector2 vIndex = indexMCP;

        // Estimate depth from apparent joint span
        float jointSpan = Vector2.Distance(vWrist, vMiddle);
        float estimatedDepth = baseDepth;
        if (jointSpan > 0.01f)
        {
            estimatedDepth = baseDepth * (referenceJointSpan / jointSpan);
            estimatedDepth = Mathf.Clamp(estimatedDepth, 0.15f, 1.0f);
        }

        // Project to world space
        Ray wristRay = mainCam.ViewportPointToRay(new Vector3(vWrist.x, vWrist.y, 0));
        Vector3 targetPos = wristRay.GetPoint(estimatedDepth);

        Ray middleRay = mainCam.ViewportPointToRay(new Vector3(vMiddle.x, vMiddle.y, 0));
        Vector3 middleWorld = middleRay.GetPoint(estimatedDepth);

        Ray indexRay = mainCam.ViewportPointToRay(new Vector3(vIndex.x, vIndex.y, 0));
        Vector3 indexWorld = indexRay.GetPoint(estimatedDepth);

        // Derive palm orientation
        Vector3 palmUp = (middleWorld - targetPos).normalized;
        Vector3 palmRight = (indexWorld - middleWorld).normalized;
        Vector3 palmNormal = Vector3.Cross(palmUp, palmRight).normalized;

        if (palmUp.sqrMagnitude > 0.001f && palmNormal.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(palmNormal, palmUp);
            smoothedRotation = Quaternion.Slerp(smoothedRotation, targetRot, Time.deltaTime * rotationSmoothSpeed);
        }

        smoothedPosition = Vector3.Lerp(smoothedPosition, targetPos, Time.deltaTime * positionSmoothSpeed);

        WristWorldPosition = smoothedPosition;
        WristWorldRotation = smoothedRotation;
    }

#if UNITY_EDITOR
    private void UpdateEditorSimulation()
    {
        if (Input.GetMouseButton(0))
        {
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            Vector3 mv = mainCam.ScreenToViewportPoint(Input.mousePosition);
            ProcessHandData(
                new Vector2(mv.x, mv.y),
                new Vector2(mv.x + referenceJointSpan * 0.3f, mv.y + referenceJointSpan * 0.9f),
                new Vector2(mv.x, mv.y + referenceJointSpan),
                new Vector2(mv.x - referenceJointSpan * 0.5f, mv.y + referenceJointSpan * 0.3f),
                1.0f
            );
        }
        else
        {
            Confidence = 0f;
            IsTracking = false;
        }
    }
#endif
}
