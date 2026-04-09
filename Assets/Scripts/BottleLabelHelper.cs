using UnityEngine;
using TMPro;

public static class BottleLabelHelper
{
    public static float fontSize    = 0.06f;
    public static float labelOffset = 0.001f; // just above table surface (at QR code level)

    public static GameObject AddLabel(Transform parent, string labelText, float extraYOffset = 0f)
    {
        var go = new GameObject("BottleLabel");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, labelOffset + extraYOffset, 0f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one * 0.1f; // scale down so text isn't huge

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text         = labelText;
        tmp.fontSize     = fontSize;
        tmp.alignment    = TextAlignmentOptions.Center;
        tmp.color        = Color.white;
        tmp.outlineWidth = 0.3f;
        tmp.outlineColor = Color.black;
        tmp.enableWordWrapping = false;

        // Add a BillboardLabel component so it always faces the camera
        go.AddComponent<BillboardLabel>();

        return go;
    }
}
