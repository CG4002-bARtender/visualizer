using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameUIManager : MonoBehaviour
{
    [Header("Screens")]
    public GameObject startScreen;
    public GameObject roundEndScreen;
    public GameObject gameEndScreen;

    [Header("Round End UI")]
    public TextMeshProUGUI roundEndTitleText;   // e.g. "Round 2 Complete!"
    public TextMeshProUGUI roundEndResultText;  // "PASS" or "FAIL"
    public TextMeshProUGUI roundEndScoreText;   // e.g. "Score: 2 / 3"

    [Header("Game End UI")]
    public TextMeshProUGUI gameEndScoreText;    // e.g. "3 / 3"
    public TextMeshProUGUI gameEndMessageText;  // "Perfect Bartender!" etc.

    [Header("Settings")]
    public int totalRounds = 3;

    private bool gameStarted = false;

    void Awake()
    {
        startScreen?.SetActive(true);
        roundEndScreen?.SetActive(false);
        gameEndScreen?.SetActive(false);
    }

    // Called by the Start button
    public void OnStartButtonPressed()
    {
        gameStarted = true;
        startScreen?.SetActive(false);
    }

    // Called by MQTTManager when a new order arrives (state 1 with bottle_map)
    public void OnNewOrder()
    {
        roundEndScreen?.SetActive(false);
        gameEndScreen?.SetActive(false);
    }

    // Called by MQTTManager on state 0 (round ended)
    public void OnRoundEnd(int round, int roundScore, int totalScore)
    {
        if (round >= totalRounds)
            ShowGameEnd(totalScore);
        else
            ShowRoundEnd(round, roundScore, totalScore);
    }

    void ShowRoundEnd(int round, int roundScore, int totalScore)
    {
        roundEndTitleText.text  = $"Round {round} Complete!";
        roundEndResultText.text = roundScore == 1 ? "PASS" : "FAIL";
        roundEndScoreText.text  = $"Score: {totalScore} / {totalRounds}";
        roundEndScreen?.SetActive(true);
    }

    // Called by the Next Round button
    public void OnNextRoundPressed()
    {
        roundEndScreen?.SetActive(false);
    }

    void ShowGameEnd(int totalScore)
    {
        StopAllCoroutines();
        roundEndScreen?.SetActive(false);

        gameEndScoreText.text   = $"{totalScore} / {totalRounds}";
        gameEndMessageText.text = GetEndMessage(totalScore);
        gameEndScreen?.SetActive(true);

        gameStarted = false;
    }

    // Called by the Restart button on game end screen
    public void OnRestartPressed()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    string GetEndMessage(int score)
    {
        if (score == totalRounds)          return "Perfect Bartender!";
        if (score >= totalRounds - 1)      return "Great Job!";
        if (score > 0)                     return "Keep Practicing!";
        return "Better Luck Next Time...";
    }
}
