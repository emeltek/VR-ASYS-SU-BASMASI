using TMPro;
using UnityEngine;

/// <summary>
/// Sel sınavı puanı. 100'den başlar, hatalarda düşer (en az 0); sonuçta standart risk etiketi gösterilir:
/// 75+ Güvenli, 50–74 Orta Riskli, 25–49 Riskli, 25 altı Yüksek Riskli.
///
/// Senaryo sırasında: tehlikeli alanda kalma (GnnRiskScorer), suya temas, hareketsiz kalma.
/// Senaryo sonunda (ShowFinalScore): yapılmayan görevler (şalter, telefon, ilk yardım çantası) ve
/// oyuncunun bulunduğu yer (GnnRiskScorer: zemin kat / tehlikeli alan).
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public enum SwitchResult
    {
        NotDone,     // şalter hiç kapatılmadı
        Done,        // su yükselmeden kapatıldı
        DoneWet      // su yükseldikten sonra kapatıldı (ıslakken elektriğe dokunmak)
    }

    public static ScoreManager Instance;
    public static float score = 100f;

    private const float MIN_SCORE = 0f;
    private const float MAX_SCORE = 100f;

    [SerializeField] private TextMeshProUGUI scoreText;

    [Tooltip("Eski KNN konum puanı. Konum cezası artık GNN'den gelir (GnnRiskScorer); bu alan kullanılmıyor.")]
    [SerializeField] private KnnDangerZoneSystem dangerSystem;

    [Header("Görev Cezaları (senaryo sonunda)")]
    [SerializeField] private int switchNotDonePenalty = 30;
    [SerializeField] private int switchWetPenalty = 35;
    [SerializeField] private int phoneMissedPenalty = 10;
    [SerializeField] private int firstAidMissedPenalty = 10;

    [Header("Senaryo Sırasında")]
    [Tooltip("Suya ilk temas (bir kez)")]
    [SerializeField] private int waterContactPenalty = 20;
    [Tooltip("Güvenli alan dışında her 15 s hareketsiz kalış")]
    [SerializeField] private int immobilityPenalty = 5;
    [SerializeField] private int immobilityPenaltyCap = 15;

    private static bool finalized;
    private static bool phoneTaken;
    private static bool firstAidTaken;
    private static bool waterContacted;
    private static int immobilityDeducted;
    private static SwitchResult switchResult = SwitchResult.NotDone;

    public static int CurrentScore => Mathf.RoundToInt(Mathf.Clamp(score, MIN_SCORE, MAX_SCORE));
    public static bool IsFinalized => finalized;

    /// <summary>Sahne yüklenişinde statik durum (StaticStateReset çağırır).</summary>
    public static void ResetState()
    {
        score = MAX_SCORE;
        finalized = false;
        phoneTaken = false;
        firstAidTaken = false;
        waterContacted = false;
        immobilityDeducted = 0;
        switchResult = SwitchResult.NotDone;
    }

    private void Awake()
    {
        ResetState();
        Instance = this;
        if (scoreText != null) scoreText.text = ""; // sahnedeki geçici yazı ("puıan") görünmesin
    }

    /// <summary>Puanı düşürür (amount pozitif). Senaryo bittiyse etkisizdir.</summary>
    public void Deduct(float amount, string reason)
    {
        if (finalized || amount <= 0f) return;
        score = Mathf.Max(MIN_SCORE, score - amount);
        if (!string.IsNullOrEmpty(reason))
            Debug.Log($"<color=red>[Puan] {reason}: -{amount:F0} → Puan: {CurrentScore} ({GetRiskName(CurrentScore)})</color>");
    }

    /// <summary>Eski API: puan düşürür.</summary>
    public void SubtractScore(float value) => Deduct(value, "Puan düşüldü");

    /// <summary>Eski API: puan eklemek artık yok (puan 100'den düşer); görevler Mark* ile işaretlenir.</summary>
    public void AddScore(float value)
    {
        Debug.Log($"[Puan] AddScore({value}) yok sayıldı: puan 100'den düşer, görevler senaryo sonunda değerlendirilir.");
    }

    public void MarkPhoneTaken()
    {
        if (phoneTaken) return;
        phoneTaken = true;
        Debug.Log("[Puan] Telefon alındı");
    }

    public void MarkFirstAidTaken()
    {
        if (firstAidTaken) return;
        firstAidTaken = true;
        Debug.Log("[Puan] İlk yardım çantası alındı");
    }

    /// <summary>Şalter ilk kez kapatıldığında; sonraki aç / kapa sonucu değiştirmez.</summary>
    public void MarkSwitchOff(bool waterTooHigh)
    {
        if (switchResult != SwitchResult.NotDone || finalized) return;
        switchResult = waterTooHigh ? SwitchResult.DoneWet : SwitchResult.Done;
        Debug.Log(waterTooHigh
            ? "<color=red>[Puan] Şalter su yükseldikten sonra kapatıldı (ıslakken elektriğe dokunma)</color>"
            : "[Puan] Şalter zamanında kapatıldı");
    }

    public void WaterContact()
    {
        if (waterContacted) return;
        waterContacted = true;
        Deduct(waterContactPenalty, "Sel suyuna temas");
    }

    public void ImmobilityWarning()
    {
        int penalty = Mathf.Min(immobilityPenalty, immobilityPenaltyCap - immobilityDeducted);
        if (penalty <= 0 || finalized) return;
        immobilityDeducted += penalty;
        Deduct(penalty, $"Güvenli alan dışında hareketsiz kaldı ({immobilityDeducted}/{immobilityPenaltyCap})");
    }

    public void ShowFinalScore()
    {
        if (!finalized)
        {
            if (switchResult == SwitchResult.NotDone) Deduct(switchNotDonePenalty, "Şalter kapatılmadı");
            else if (switchResult == SwitchResult.DoneWet) Deduct(switchWetPenalty, "Şalter ıslakken kapatıldı");
            if (!phoneTaken) Deduct(phoneMissedPenalty, "Telefon alınmadı");
            if (!firstAidTaken) Deduct(firstAidMissedPenalty, "İlk yardım çantası alınmadı");
            GnnRiskScorer.ApplyEndPositionPenalty(this);

            finalized = true;
            score = Mathf.Clamp(score, MIN_SCORE, MAX_SCORE);
            Debug.Log($"<color=cyan>[Puan] Sonuç: {CurrentScore} ({GetRiskName(CurrentScore)})</color>");
        }

        if (scoreText != null)
            scoreText.text = "Toplam Puanınız: " + CurrentScore + " / 100\n" +
                             "Risk Düzeyi: " + GetRiskLabel(CurrentScore);
    }

    public static string GetRiskName(int value)
    {
        if (value >= 75) return "Güvenli";
        if (value >= 50) return "Orta Riskli";
        if (value >= 25) return "Riskli";
        return "Yüksek Riskli";
    }

    // Puana göre renkli risk etiketi döner
    private static string GetRiskLabel(int value)
    {
        string color = value >= 75 ? "#2ECC71" : value >= 50 ? "#F1C40F" : value >= 25 ? "#E67E22" : "#E74C3C";
        return $"<color={color}>{GetRiskName(value)}</color>";
    }
}
