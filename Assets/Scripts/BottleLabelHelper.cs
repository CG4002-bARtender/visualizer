using UnityEngine;
using TMPro;

public static class BottleLabelHelper
{
    public static float labelOffset = 0.01f;
    public static GameObject labelPrefab;

    public static GameObject AddLabel(Transform parent, string labelText, float extraYOffset = 0f, bool absoluteOffset = false, GameObject prefabOverride = null)
    {
        var prefab = prefabOverride != null ? prefabOverride : labelPrefab;
        if (prefab == null)
        {
            Debug.LogWarning("[BottleLabelHelper] labelPrefab is not assigned.");
            return new GameObject("BottleLabel_Empty");
        }

        var go = Object.Instantiate(prefab, parent);
        float yPos = absoluteOffset ? extraYOffset : labelOffset + extraYOffset;
        go.transform.localPosition = new Vector3(0f, yPos, -0.05f);
        go.transform.localRotation = Quaternion.identity;

        var tmp = go.GetComponentInChildren<TextMeshPro>();
        if (tmp != null)
        {
            tmp.text = labelText;
            // Render on top of geometry
            var mr = tmp.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 100;
        }

        if (go.GetComponent<BillboardLabel>() == null)
            go.AddComponent<BillboardLabel>();

        return go;
    }
}
