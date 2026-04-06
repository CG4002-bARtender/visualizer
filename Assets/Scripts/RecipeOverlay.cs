using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class RecipeOverlay : MonoBehaviour
{
    [Header("UI References")]
    public GameObject overlayPanel;
    public TextMeshProUGUI drinkNameText;
    public Transform stepsContainer;       // VerticalLayoutGroup parent
    public GameObject stepRowPrefab;       // Prefab: GameObject with TextMeshProUGUI

    [Header("Colors")]
    public Color defaultColor  = Color.white;
    public Color correctColor  = new Color(0.2f, 0.9f, 0.2f); // green
    public Color wrongColor    = new Color(0.9f, 0.2f, 0.2f); // red
    public Color completeColor = new Color(0.2f, 0.9f, 0.2f); // green (finishing pour / shake)

    private readonly List<TextMeshProUGUI> stepTexts = new List<TextMeshProUGUI>();
    private int currentStep = 0;
    private bool needsShake = false;

    static readonly string[] DRINK_NAMES =
    {
        "Aviation", "Godfather", "Irish Coffee", "Martini",
        "Midori Sour", "Old Fashioned", "Scotch Neat",
        "Tuxedo", "Vodka Neat", "Whiskey Neat"
    };

    void Awake()
    {
        overlayPanel?.SetActive(false);
    }

    // Called by MQTTManager on new order
    public void SetupRecipe(int drinkInt, string[] ingredients, bool shake)
    {
        // Clear previous steps
        for (int i = stepsContainer.childCount - 1; i >= 0; i--)
            DestroyImmediate(stepsContainer.GetChild(i).gameObject);
        stepTexts.Clear();
        currentStep = 0;
        needsShake  = shake;

        // Drink name
        string name = (drinkInt >= 0 && drinkInt < DRINK_NAMES.Length)
            ? DRINK_NAMES[drinkInt] : $"Drink {drinkInt}";
        drinkNameText.text = name;

        // Ingredient steps
        string target = shake ? "shaker" : "glass";
        for (int i = 0; i < ingredients.Length; i++)
            AddStep($"{i + 1}. Pour {ingredients[i]} into {target}");

        // Extra steps for shake drinks
        if (shake)
        {
            AddStep($"{ingredients.Length + 1}. Shake the shaker");
            AddStep($"{ingredients.Length + 2}. Pour shaker into glass");
        }

        overlayPanel?.SetActive(true);
    }

    // Called by MQTTManager on each ingredient pour (state 3 with pour_result)
    public void MarkIngredientStep(string pourResult)
    {
        if (currentStep >= stepTexts.Count) return;

        stepTexts[currentStep].color = pourResult == "correct" ? correctColor : wrongColor;
        currentStep++;
    }

    // Called by MQTTManager when shake completes (state 4 → state 2)
    public void MarkShakeStep()
    {
        if (!needsShake) return;

        int shakeStep = stepTexts.Count - 2;
        if (shakeStep >= 0 && currentStep <= shakeStep)
        {
            stepTexts[shakeStep].color = completeColor;
            currentStep = shakeStep + 1;
        }
    }

    // Called by MQTTManager on finishing pour (state 3, shaker → serving glass, no pour_result)
    public void MarkFinishingPour()
    {
        if (!needsShake) return;

        int pourStep = stepTexts.Count - 1;
        if (pourStep >= 0 && currentStep <= pourStep)
        {
            stepTexts[pourStep].color = completeColor;
            currentStep = pourStep + 1;
        }
    }

    public void Hide()
    {
        overlayPanel?.SetActive(false);
    }

    void AddStep(string label)
    {
        GameObject row = Instantiate(stepRowPrefab, stepsContainer);
        TextMeshProUGUI tmp = row.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = row.GetComponentInChildren<TextMeshProUGUI>();
        tmp.text  = label;
        tmp.color = defaultColor;
        stepTexts.Add(tmp);
    }
}
