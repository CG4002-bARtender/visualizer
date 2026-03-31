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
    public Color completeColor = new Color(0.6f, 0.9f, 1.0f); // light blue (finishing pour)

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
        foreach (Transform child in stepsContainer)
            Destroy(child.gameObject);
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

    // Called by MQTTManager on finishing pour (state 3, no pour_result, shaker picked up)
    // Also advances the "Shake" step if not already done
    public void MarkFinishingPour()
    {
        if (!needsShake) return;

        // Mark "Shake" step complete if we haven't yet
        int shakeStep = stepTexts.Count - 2;
        if (shakeStep >= 0 && currentStep <= shakeStep)
        {
            stepTexts[shakeStep].color = completeColor;
            currentStep = shakeStep + 1;
        }

        // Mark "Pour to Glass" step
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
