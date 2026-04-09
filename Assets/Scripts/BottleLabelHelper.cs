using UnityEngine;
using TMPro;

public static class BottleLabelHelper
{
    public static float labelOffset = 0.01f;
    public static GameObject labelPrefab;

    public static GameObject AddLabel(Transform parent, string labelText, float extraYOffset = 0f)
    {
        if (labelPrefab == null)
        {
            Debug.LogWarning("[BottleLabelHelper] labelPrefab is not assigned.");
            return new GameObject("BottleLabel_Empty");
        }

        var go = Object.Instantiate(labelPrefab, parent);
        go.transform.localPosition = new Vector3(0f, labelOffset + extraYOffset, 0f);
        go.transform.localRotation = Quaternion.identity;

        var tmp = go.GetComponentInChildren<TextMeshPro>();
        if (tmp != null) tmp.text = labelText;

        if (go.GetComponent<BillboardLabel>() == null)
            go.AddComponent<BillboardLabel>();

        return go;
    }
}
