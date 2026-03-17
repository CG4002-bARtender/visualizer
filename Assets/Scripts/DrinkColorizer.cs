using UnityEngine;

public class DrinkColorizer : MonoBehaviour
{
    public Color drinkColor = Color.white;

    void Start()
    {
        GetComponent<MeshRenderer>().material.color = drinkColor;
    }

    void OnValidate()
    {
        var r = GetComponent<MeshRenderer>();
        if (r != null) r.material.color = drinkColor;
    }
}