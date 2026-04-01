using UnityEngine;
using TMPro;

public class GameUIManager : MonoBehaviour
{
    [Header("Screens")]
    public GameObject startScreen;
    public GameObject gameEndScreen;

    [Header("Game End UI")]
    public TextMeshProUGUI gameEndScoreText;

    void Awake()
    {
        startScreen?.SetActive(true);
        gameEndScreen?.SetActive(false);
    }

    public void OnIdle()
    {
        startScreen?.SetActive(false);
        gameEndScreen?.SetActive(false);
    }

    public void OnNewOrder()
    {
        gameEndScreen?.SetActive(false);
    }

    public void OnGameEnd(int totalScore)
    {
        if (gameEndScoreText != null)
            gameEndScoreText.text = totalScore.ToString();

        gameEndScreen?.SetActive(true);
    }
}
