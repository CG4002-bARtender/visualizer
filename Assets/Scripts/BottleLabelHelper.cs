using UnityEngine;
using TMPro;

/// <summary>
/// Spawns a world-space TMP label below a bottle.
/// Call BottleLabelHelper.AddLabel(parent, text, yOffset) after spawning any bottle.
/// </summary>
public static class BottleLabelHelper
{
    public static float fontSize    = 0.04f;
    public static float labelOffset = -0.04f; // below the bottle base

    public static GameObject AddLabel(Transform parent, string labelText, float extraYOffset = 0f)
    {
        var go = new GameObject("BottleLabel");
        go.transform.SetParent(parent, false);
        go.transform.localPosition  = new Vector3(0f, labelOffset + extraYOffset, 0f);
        go.transform.localRotation  = Quaternion.identity;
        go.transform.localScale     = Vector3.one;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text          = labelText;
        tmp.fontSize      = fontSize;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = Color.white;
        tmp.outlineWidth  = 0.2f;
        tmp.outlineColor  = Color.black;

        // Face upward (flat on table, readable from above camera)
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        return go;
    }
}
