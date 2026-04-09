using UnityEngine;

/// <summary>
/// Makes the attached GameObject always face the main camera.
/// Attach to world-space labels so they're readable from any angle.
/// </summary>
public class BillboardLabel : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}
