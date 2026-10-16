using TMPro;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;
    public static float score = 0f;

    private const float MIN_SCORE = 0f;
    private const float MAX_SCORE = 100f;

    [SerializeField] private TextMeshProUGUI scoreText;

    [Tooltip("Konum puanı için. Bu sahnede tehlike bölgesi yoksa boş bırak.")]
    [SerializeField] private KnnDangerZoneSystem dangerSystem;

    private bool locationScoreAdded = false;

    private void Awake()
    {
        score = 0f;
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
        // Konum puanı (0 - 60) senaryo sonunda bir kere eklenir
        if (dangerSystem != null && !locationScoreAdded)
        {
            locationScoreAdded = true;
            AddScore(dangerSystem.LocationScore);
        }

        score = Mathf.Clamp(score, MIN_SCORE, MAX_SCORE);
        scoreText.text = "Toplam Puanınız: " + score.ToString("F0") + " / 100\n" +
                         "Risk Düzeyi: " + GetRiskLabel(score);
    }

    // Puana göre renkli risk etiketi döner
    private string GetRiskLabel(float value)
    {
        if (value >= 80f) return "<color=#2ECC71>Güvenli</color>";
        if (value >= 60f) return "<color=#F1C40F>Orta Riskli</color>";
        if (value >= 40f) return "<color=#E67E22>Riskli</color>";
        return "<color=#E74C3C>Çok Riskli</color>";
    }
}