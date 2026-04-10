using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameUIManager : MonoBehaviour
{
    [Header("Screens")]
    public GameObject startScreen;
    public GameObject gameEndScreen;

    [Header("Game End UI")]
    public TextMeshProUGUI gameEndScoreText;

    [Header("HUD (always visible during game)")]
    public GameObject hudPanel;
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI scoreText;

    [Header("MQTT Status")]
    public TextMeshProUGUI mqttStatusText;

    [Header("Idle Info")]
    public GameObject idleInfoPanel;
    public TextMeshProUGUI idleInfoText;

    [Header("References")]
    public MQTTManager mqttManager;

    [Header("Start Screen Mode Boxes")]
    public Image normalModeBox;
    public Image tutorialModeBox;
    public Image cheatModeBox;
    public Color modeHighlightColor = new Color(0.2f, 0.85f, 0.2f, 1f); // green
    public Color modeDefaultColor   = new Color(1f, 1f, 1f, 0.15f);     // dim white

    [Header("Testing")]
    [Tooltip("Hide all game UI on boot. Use alongside MQTTManager.testingMode.")]
    public bool testingMode = false;

    void Awake()
    {
        if (testingMode)
        {
            startScreen?.SetActive(false);
            gameEndScreen?.SetActive(false);
            hudPanel?.SetActive(false);
            idleInfoPanel?.SetActive(false);
            if (mqttStatusText != null) mqttStatusText.gameObject.SetActive(false);
            return;
        }

        startScreen?.SetActive(true);
        gameEndScreen?.SetActive(false);
        hudPanel?.SetActive(false);
        idleInfoPanel?.SetActive(false);
        ResetModeBoxes();

    }

    public void SetMQTTStatus(bool connected, string brokerAddress)
    {
        if (mqttStatusText == null) return;
        mqttStatusText.gameObject.SetActive(true);
        mqttStatusText.text = connected
            ? $"MQTT: Connected to {brokerAddress}"
            : $"MQTT: Connecting to {brokerAddress}...";

        if (connected)
        {
            StopCoroutine("HideMQTTStatus");
            StartCoroutine("HideMQTTStatus");
        }
    }

    System.Collections.IEnumerator HideMQTTStatus()
    {
        yield return new WaitForSeconds(5f);
        if (mqttStatusText != null)
            mqttStatusText.gameObject.SetActive(false);
    }

    public void OnStartScreen()
    {
        startScreen?.SetActive(true);
        gameEndScreen?.SetActive(false);
        hudPanel?.SetActive(false);
        idleInfoPanel?.SetActive(false);
        ResetModeBoxes();
    }

    public void OnIdle(int mode = 0, int round = 0)
    {
        HighlightModeBox(mode);
        StartCoroutine(TransitionToIdle(mode, round));
    }

    System.Collections.IEnumerator TransitionToIdle(int mode, int round)
    {
        yield return new WaitForSeconds(0.5f);
        ResetModeBoxes();

        if (mode == 1)
        {
            startScreen?.SetActive(false);
            yield break;
        }

        startScreen?.SetActive(false);
        gameEndScreen?.SetActive(false);
        if (idleInfoText != null)
        {
            idleInfoText.text = round <= 1
                ? "Move closer to the QR codes until bottles appear.\nThen, the customer can make an order."
                : "Customer can make an order.";
        }
        idleInfoPanel?.SetActive(true);
        if (mqttStatusText != null) mqttStatusText.gameObject.SetActive(false);
    }

    void HighlightModeBox(int mode)
    {
        ResetModeBoxes();
        Image box = mode == 0 ? normalModeBox : mode == 1 ? tutorialModeBox : cheatModeBox;
        if (box != null) box.color = modeHighlightColor;
    }

    void ResetModeBoxes()
    {
        if (normalModeBox   != null) normalModeBox.color   = modeDefaultColor;
        if (tutorialModeBox != null) tutorialModeBox.color = modeDefaultColor;
        if (cheatModeBox    != null) cheatModeBox.color    = modeDefaultColor;
    }

    public void OnNewOrder(int round, int score)
    {
        gameEndScreen?.SetActive(false);
        idleInfoPanel?.SetActive(false);
        if (hudPanel != null && !hudPanel.activeSelf)
            hudPanel.SetActive(true);
        UpdateHUD(round, score);
    }

    public void UpdateHUD(int round, int score)
    {
        if (roundText != null) roundText.text = $"Round {round}";
        if (scoreText != null) scoreText.text = $"Score: {score}";
    }


    public void OnGameEnd(int totalScore)
    {
        if (gameEndScoreText != null)
            gameEndScoreText.text = $"Total Score: {totalScore}/3";

        hudPanel?.SetActive(false);
        gameEndScreen?.SetActive(true);
    }
}
