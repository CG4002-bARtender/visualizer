using System;
using UnityEngine;
using TMPro;

public class TutorialManager : MonoBehaviour
{
    [Header("References")]
    public QRCodeManager qrCodeManager;
    public SimpleHandSimulator handSimulator;
    public Transform arCamera;

    [Header("Tutorial Drink")]
    public GameObject finalDrinkPrefab;

    [Header("Prefabs")]
    public GameObject bottlePrefab;
    public GameObject shakerPrefab;
    public GameObject glassPrefab;

    [Header("Spawn Settings")]
    public float bottleHeight = 0.08f;
    public float shakerHeight = 0.06f;
    public float glassHeight  = 0.03f;

    [Header("UI")]
    public GameObject tutorialPanel;
    public TextMeshProUGUI instructionText;
    public GameObject tutorialEndPanel;

    private GameObject spawnedBottle;
    private GameObject spawnedShaker;
    private GameObject spawnedGlass;

    const string BOTTLE_SLOT = "qr1";
    const string SHAKER_SLOT = "qr2";
    const string GLASS_SLOT  = "qr4";

    static readonly string[] Instructions =
    {
        "Move hand to bottle to select it",         // 0
        "Grab to pick up bottle",                   // 1
        "Pour into shaker",                         // 2
        "Release the bottle",                       // 3
        "Move hand to shaker, and grab to pick up", // 4
        "Shake the shaker",                         // 5
        "Pour the shaker into glass",               // 6
        "Release the shaker",                       // 7
        "Thumbs up to serve",                       // 8
    };

    void Awake()
    {
        tutorialPanel?.SetActive(false);
        tutorialEndPanel?.SetActive(false);
    }

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main?.transform;
    }

    public void StartTutorial()
    {
        tutorialPanel?.SetActive(true);
        tutorialEndPanel?.SetActive(false);
        ClearSpawned();

        if (qrCodeManager != null)
        {
            qrCodeManager.expectedQRCount = 3;
            qrCodeManager.trackedQRFilter = new System.Collections.Generic.HashSet<string> { "qr1", "qr2", "qr4" };

            // If QRs are already tracked from a previous session, skip scanning UI
            int alreadyTracked = qrCodeManager.CountTracked();
            if (alreadyTracked >= 3)
            {
                if (instructionText != null)
                    instructionText.text = Instructions[0];
                StartCoroutine(SpawnBottleWhenTracked());
            }
            else
            {
                if (instructionText != null)
                    instructionText.text = $"Scanning QR codes... ({alreadyTracked}/3)\nMove closer until all codes appear.";

                qrCodeManager.onQRCountChanged = (detected, total) =>
                {
                    if (instructionText != null)
                        instructionText.text = detected < total
                            ? $"Scanning QR codes... ({detected}/{total})\nMove closer until all codes appear."
                            : Instructions[0];
                };
                qrCodeManager.onAllQRDetected = () =>
                {
                    if (instructionText != null)
                        instructionText.text = Instructions[0];
                    StartCoroutine(SpawnBottleWhenTracked());
                };
            }
        }
        else
        {
            if (instructionText != null)
                instructionText.text = Instructions[0];
            StartCoroutine(SpawnBottleWhenTracked());
        }
    }

    System.Collections.IEnumerator SpawnBottleWhenTracked()
    {
        while (qrCodeManager?.GetQRTransform(BOTTLE_SLOT) == null)
            yield return null;
        SpawnObject(bottlePrefab, BOTTLE_SLOT, bottleHeight, ref spawnedBottle);
    }

    public void HandleStep(int step, int hallId = -1)
    {
        ApplyStep(step, hallId);
    }

    void ApplyStep(int step, int hallId = -1)
    {
        switch (step)
        {
            case 0:
                // Highlight wherever the hand is, but only advance instruction
                // once the hand is actually at the bottle slot.
                if (hallId >= 0)
                    handSimulator?.HighlightSlot(hallId);
                if (hallId == SlotIndex(BOTTLE_SLOT))
                    SetInstruction(1);
                break;

            case 1:
                // Correct bottle grabbed — spawn shaker and advance instruction.
                handSimulator?.ClearHighlight();
                handSimulator?.GrabObjectAtSlot(SlotIndex(BOTTLE_SLOT));
                SpawnObject(shakerPrefab, SHAKER_SLOT, shakerHeight, ref spawnedShaker);
                SetInstruction(2);
                break;

            case 2:
                // Pour started.
                StartCoroutine(DelayedAction(0.5f, () =>
                    handSimulator?.OnMQTTPour("shaker",
                        new Color(0.85f, 0.95f, 1f, 0.8f), null)));
                SetInstruction(3);
                break;

            case 3:
                // Bottle released — server-confirmed, always advance instruction.
                // Highlight shaker only if hand is already there.
                handSimulator?.OnReleaseCupButton();
                SetInstruction(4);
                if (hallId == SlotIndex(SHAKER_SLOT))
                    handSimulator?.HighlightSlot(SlotIndex(SHAKER_SLOT));
                break;

            case 4:
                // Shaker grabbed — spawn glass and advance instruction.
                handSimulator?.ClearHighlight();
                handSimulator?.GrabObjectAtSlot(SlotIndex(SHAKER_SLOT));
                SpawnObject(glassPrefab, GLASS_SLOT, glassHeight, ref spawnedGlass);
                SetInstruction(5);
                break;

            case 5:
                // Shake started.
                StartCoroutine(DelayedAction(0.3f, () =>
                    handSimulator?.OnMQTTShake(null)));
                SetInstruction(6);
                break;

            case 6:
                // Pour from shaker into glass.
                StartCoroutine(DelayedAction(0.5f, () =>
                    handSimulator?.OnMQTTPour("serving_glass",
                        new Color(0.7f, 0.6f, 0.5f, 0.8f), null)));
                SetInstruction(7);
                break;

            case 7:
                // Shaker released — serve prompt.
                handSimulator?.OnReleaseCupButton();
                SetInstruction(8);
                break;
        }
    }

    void SetInstruction(int index)
    {
        if (instructionText != null && index < Instructions.Length)
            instructionText.text = Instructions[index];
    }

    public void HandleComplete()
    {
        handSimulator?.OnReleaseCupButton();
        SpawnObject(finalDrinkPrefab, GLASS_SLOT, glassHeight, ref spawnedGlass);
        if (instructionText != null) instructionText.gameObject.SetActive(false);
        tutorialEndPanel?.SetActive(true);
        StartCoroutine(DelayedAction(5f, EndTutorial));
    }

    void EndTutorial()
    {
        ClearSpawned();
        if (qrCodeManager != null)
        {
            qrCodeManager.ShowLabels();
            qrCodeManager.expectedQRCount = 5;
            qrCodeManager.trackedQRFilter = null;
            qrCodeManager.onQRCountChanged = null;
            qrCodeManager.onAllQRDetected = null;
        }
        if (instructionText != null) instructionText.gameObject.SetActive(true);
        tutorialPanel?.SetActive(false);
        tutorialEndPanel?.SetActive(false);
        FindObjectOfType<GameUIManager>()?.OnStartScreen();
    }

    void ClearSpawned()
    {
        // Use RemoveBottleAtQR so QRCodeManager's registry stays in sync.
        // It handles the null check and Destroy internally.
        if (spawnedBottle != null) { qrCodeManager?.RemoveBottleAtQR(BOTTLE_SLOT); spawnedBottle = null; }
        if (spawnedShaker != null) { qrCodeManager?.RemoveBottleAtQR(SHAKER_SLOT); spawnedShaker = null; }
        if (spawnedGlass  != null) { qrCodeManager?.RemoveBottleAtQR(GLASS_SLOT);  spawnedGlass  = null; }
    }

    int SlotIndex(string qrName) => int.Parse(qrName.Replace("qr", ""));

    void SpawnObject(GameObject prefab, string qrName, float height, ref GameObject slot)
    {
        if (prefab == null) return;
        Transform parent = qrCodeManager?.GetQRTransform(qrName);
        if (parent == null)
        {
            Debug.LogWarning($"[Tutorial] QR '{qrName}' not tracked yet — cannot spawn");
            return;
        }

        // RegisterBottleAtQR handles destroying the old object at this slot
        slot = Instantiate(prefab, parent);
        slot.transform.localPosition = Vector3.up * height;
        slot.transform.localRotation = Quaternion.identity;
        qrCodeManager?.RegisterBottleAtQR(qrName, slot);
    }

    System.Collections.IEnumerator DelayedAction(float delay, Action action)
    {
        yield return new WaitForSeconds(delay);
        action?.Invoke();
    }
}
