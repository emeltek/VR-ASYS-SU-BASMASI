using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;
    public static float score = 0f;

    private const float MIN_SCORE = 0f;
    private const float MAX_SCORE = 100f;

    [SerializeField] private TextMeshProUGUI scoreText;

    private void Awake()
    {
        Instance = this;
    }

    public void AddScore(float value)
    {
        score = Mathf.Clamp(score + value, MIN_SCORE, MAX_SCORE);
        Debug.Log("Puan eklendi: " + value + " | Toplam: " + score);
    }

    public void SubtractScore(float value)
    {
        score = Mathf.Clamp(score - value, MIN_SCORE, MAX_SCORE);
        Debug.Log("Puan düşüldü: " + value + " | Toplam: " + score);
    }

    public void ShowFinalScore()
    {
        scoreText.text = "Toplam Puanınız: " + score.ToString("F0") + " / 100";
    }
}