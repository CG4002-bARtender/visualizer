using UnityEngine;
using TMPro;

public class GameUIManager : MonoBehaviour
{
    [Header("Screens")]
    public GameObject startScreen;
    public GameObject gameEndScreen;

    [Header("Start Screen UI")]
    public TextMeshProUGUI startPromptText;

[Header("Game End UI")]
    public TextMeshProUGUI gameEndScoreText;
    public TextMeshProUGUI gameEndMessageText;
    public TextMeshProUGUI gameEndPromptText;

    [Header("Settings")]
    public int totalRounds = 3;

    void Awake()
    {
        startScreen?.SetActive(true);
        gameEndScreen?.SetActive(false);

        if (startPromptText != null)
            startPromptText.text = "Thumbs up to start the game!";
    }

    // Called by MQTTManager on state 0 — dismisses start/end screens if active
    public void OnIdle()
    {
        startScreen?.SetActive(false);
        gameEndScreen?.SetActive(false);
    }

    // Called by MQTTManager when a new order arrives (state 1 with bottle_map)
    public void OnNewOrder()
    {
        gameEndScreen?.SetActive(false);
    }

    // Called by MQTTManager on state 6 (game end)
    public void OnGameEnd(int totalScore)
    {
        gameEndScoreText.text   = $"{totalScore} / {totalRounds}";
        gameEndMessageText.text = GetEndMessage(totalScore);

        if (gameEndPromptText != null)
            gameEndPromptText.text = "Thumbs up to retry game!";

        gameEndScreen?.SetActive(true);
    }

    string GetEndMessage(int score)
    {
        if (score == totalRounds)          return "Perfect Bartender!";
        if (score >= totalRounds - 1)      return "Great Job!";
        if (score > 0)                     return "Keep Practicing!";
        return "Better Luck Next Time...";
    }
}
