using UnityEngine;

/// <summary>
/// Smoothly anchors a bottle GameObject to the tracked hand wrist position.
/// Attach to the bottle (or an empty parent) and assign the HandTrackingManager reference.
/// </summary>
public class BottleAnchor : MonoBehaviour
{
    [Header("References")]
    public HandTrackingManager handTracker;

    [Header("Position")]
    [Tooltip("Local offset from wrist to center of palm / grip point")]
    public Vector3 positionOffset = new Vector3(0f, 0.02f, 0.05f);

    [Header("Rotation")]
    [Tooltip("Local rotation offset to orient the bottle upright relative to the palm")]
    public Vector3 rotationOffset = new Vector3(-90f, 0f, 0f);

    [Header("Smoothing")]
    public float positionLerpSpeed = 12f;
    public float rotationLerpSpeed = 10f;

    [Header("Visibility")]
    [Tooltip("Hide the bottle when hand tracking confidence is lost")]
    public bool hideOnTrackingLoss = true;

    [Tooltip("Seconds to wait before hiding after tracking loss (avoids flicker)")]
    public float hideDelay = 0.3f;

    private float timeSinceTrackingLost;
    private bool isVisible = true;
    private Renderer[] renderers;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    void LateUpdate()
    {
        if (handTracker == null) return;

        if (handTracker.IsTracking)
        {
            timeSinceTrackingLost = 0f;

            if (!isVisible)
                SetVisible(true);

            // Target position = wrist world + offset rotated into wrist orientation
            Vector3 targetPos = handTracker.WristWorldPosition
                                + handTracker.WristWorldRotation * positionOffset;

            Quaternion targetRot = handTracker.WristWorldRotation
                                   * Quaternion.Euler(rotationOffset);

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * positionLerpSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationLerpSpeed);
        }
        else
        {
            timeSinceTrackingLost += Time.deltaTime;

            if (hideOnTrackingLoss && isVisible && timeSinceTrackingLost > hideDelay)
                SetVisible(false);
        }
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (renderers == null) return;

        foreach (var r in renderers)
        {
            if (r != null) r.enabled = visible;
        }
    }
}
