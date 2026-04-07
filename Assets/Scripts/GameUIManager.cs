using UnityEngine;
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

    void Awake()
    {
        startScreen?.SetActive(true);
        gameEndScreen?.SetActive(false);
        hudPanel?.SetActive(false);
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
    }

    public void OnIdle()
    {
        startScreen?.SetActive(false);
        gameEndScreen?.SetActive(false);
        if (mqttStatusText != null) mqttStatusText.gameObject.SetActive(false);
    }

    public void OnNewOrder(int round, int score)
    {
        gameEndScreen?.SetActive(false);
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
