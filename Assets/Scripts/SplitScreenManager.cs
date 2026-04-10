using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Split-screen AR for Google Cardboard.
/// - Left eye:  main AR camera (rect = left half) — full AR passthrough + 3D
/// - Right eye: child camera (rect = right half) — CommandBuffer blits AR material + 3D on top
///
/// Attach to Main Camera (XR Origin > Camera Offset > Main Camera).
/// </summary>
[RequireComponent(typeof(Camera))]
public class SplitScreenManager : MonoBehaviour
{
    public bool enableStereo = true;
    public float ipd = 0f;

    private Camera arCamera;
    private ARCameraBackground arBackground;
    private Camera rightCam;
    private CommandBuffer rightCmd;

    void Start()
    {
        arCamera    = GetComponent<Camera>();
        arBackground = GetComponent<ARCameraBackground>();

        if (!enableStereo) return;

        if (arCamera == null || arBackground == null)
        {
            Debug.LogError("[SplitScreen] Attach to Main Camera with ARCameraBackground.");
            return;
        }

        StartCoroutine(Init());
    }

    IEnumerator Init()
    {
        Debug.Log("[SplitScreen] Waiting for ARKit...");
        float t = 0f;
        while (arBackground.material == null)
        {
            t += Time.deltaTime;
            if (t > 10f) { Debug.LogWarning("[SplitScreen] Timeout waiting for AR material."); break; }
            yield return null;
        }
        Debug.Log($"[SplitScreen] AR material ready: {arBackground.material?.name}");
        Setup();
    }

    void Setup()
    {
        // Left eye = main AR camera, left half of screen
        arCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
        Debug.Log($"[SplitScreen] arCamera.rect set to {arCamera.rect}");

        // Right eye = child camera, right half
        var go = new GameObject("RightEyeCamera");
        go.transform.SetParent(arCamera.transform, false);
        go.transform.localPosition = new Vector3(ipd, 0f, 0f);

        rightCam = go.AddComponent<Camera>();
        rightCam.CopyFrom(arCamera);
        rightCam.rect       = new Rect(0.5f, 0f, 0.5f, 1f);
        rightCam.depth      = arCamera.depth + 1;
        rightCam.clearFlags = CameraClearFlags.Depth;

        // Remove any ARCameraBackground that CopyFrom pulled in
        var bg = go.GetComponent<ARCameraBackground>();
        if (bg != null) Destroy(bg);

        // Blit AR background material into right eye before 3D renders
        rightCmd = new CommandBuffer { name = "RightEye AR Background" };
        rightCmd.Blit(null, BuiltinRenderTextureType.CameraTarget, arBackground.material);
        rightCam.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, rightCmd);

        Debug.Log($"[SplitScreen] Setup done. leftRect={arCamera.rect} rightRect={rightCam.rect}");
    }

    void LateUpdate()
    {
        if (!enableStereo || rightCam == null) return;

        // AR Foundation may reset camera.rect each frame — enforce it
        if (arCamera.rect != new Rect(0f, 0f, 0.5f, 1f))
            arCamera.rect = new Rect(0f, 0f, 0.5f, 1f);
        if (rightCam.rect != new Rect(0.5f, 0f, 0.5f, 1f))
            rightCam.rect = new Rect(0.5f, 0f, 0.5f, 1f);

        // Sync right eye projection with left
        rightCam.projectionMatrix = arCamera.projectionMatrix;
        rightCam.nearClipPlane    = arCamera.nearClipPlane;
        rightCam.farClipPlane     = arCamera.farClipPlane;
    }

    void OnDestroy()
    {
        if (rightCam != null && rightCmd != null)
            rightCam.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, rightCmd);
        rightCmd?.Release();

        if (arCamera != null)
            arCamera.rect = new Rect(0f, 0f, 1f, 1f);
    }
}
