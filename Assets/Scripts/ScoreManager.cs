using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;
    public static float score = 0f;

    [SerializeField] private TextMeshProUGUI scoreText;

    private void Awake()
    {
        Instance = this;
    }

    public void AddScore(float value)
    {
        score += value;
        Debug.Log("Puan eklendi: " + value + " | Toplam: " + score);
    }

    public void SubtractScore(float value)
    {
        score -= value;
        Debug.Log("Puan düşüldü: " + value + " | Toplam: " + score);
    }

    public void ShowFinalScore()
    {
        scoreText.text = "Toplam Puanınız: " + score.ToString("F0");
    }
}