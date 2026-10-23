using UnityEngine;

/// <summary>
/// Sınav sahnesinde konuma bağlı sel cezalarını GNN risk haritasından verir.
///
/// 1) Sel sürerken GNN'in "Tehlikeli" dediği yerde (bodrum, nehir kenarı, köprü) kalmak:
///    tehlikeli alanda geçen toplam süre dangerGraceTime'ı aşınca ceza ani düşüş olmadan artar:
///
///      ceza = bütçe × (1 − e^(−E / τ))      (bütçe 60, τ = 8 s)
///
///      E : tolerans süresinden sonra tehlikeli alanda geçen, GNN olasılığıyla ağırlıklı süre (s)
///
///    Tolerans süresi toplamdır (çıkıp girmek sıfırlamaz). "Dikkat" alanı yalnızca uyarıdır.
///
/// 2) Senaryo sonunda (ScoreManager.ShowFinalScore) oyuncunun konumu: Dikkat −30 (zemin kat),
///    Tehlikeli −55 (bodrum / nehir kenarı / köprü). GNN hazır değilse aynı sınıflar GnnGraphBuilder.LabelAt
///    kuralıyla (su derinliği + nehir / köprü noktaları) bulunur.
/// </summary>
public class GnnRiskScorer : MonoBehaviour
{
    private static GnnRiskScorer active;

    public static bool IsScoring => active != null && active.riskPredictor != null && active.riskPredictor.IsReady;

    [Header("Referanslar (boşsa otomatik bulunur)")]
    [SerializeField] private GnnRiskPredictor riskPredictor;
    [Tooltip("Konumu puanlanan obje. Boşsa ana kamera (oyuncunun başı)")]
    [SerializeField] private Transform trackedTarget;

    [Header("Tehlikeli Alan Bütçesi (en fazla ceza)")]
    [SerializeField] private float dangerBudget = 60f;

    [Header("Ceza Eğrisi")]
    [Tooltip("Tehlikeli alanda bu kadar (s, toplam) kalınana kadar puan düşmez; yalnızca uyarı verilir")]
    [SerializeField] private float dangerGraceTime = 5f;
    [Tooltip("Maruziyet zaman sabiti (s): tolerans süresinden bu kadar sonra bütçenin %63'ü düşülmüş olur")]
    [SerializeField] private float exposureTimeConstant = 8f;

    [Header("Senaryo Sonunda Konum Cezası")]
    [SerializeField] private int endCautionPenalty = 30;
    [SerializeField] private int endDangerPenalty = 55;

    [Header("Zamanlama")]
    [SerializeField] private float updateInterval = 0.5f;
    [Tooltip("Dikkat alanından çıkıp bu süreden (s) önce tekrar girilirse yeniden uyarı verilmez")]
    [SerializeField] private float reentryCooldown = 3f;

    [Header("Uyarı Sesi (isteğe bağlı)")]
    [SerializeField] private AudioSource warningSound;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private float dangerTime;    // tehlikeli alanda geçen toplam süre (s)
    private float exposure;
    private int deducted;
    private float nextUpdate;
    private float lastTick = float.NegativeInfinity;
    private GnnRiskClass currentClass = GnnRiskClass.Safe;
    private float cautionLeftAt = float.NegativeInfinity;

    private void Start()
    {
        if (riskPredictor == null) riskPredictor = GnnRiskPredictor.Instance;
        if (riskPredictor == null) riskPredictor = FindObjectOfType<GnnRiskPredictor>();
        if (riskPredictor == null) riskPredictor = gameObject.AddComponent<GnnRiskPredictor>();
        if (trackedTarget == null && Camera.main != null) trackedTarget = Camera.main.transform;
    }

    private void OnEnable() => active = this;

    private void OnDisable()
    {
        if (active == this) active = null;
    }

    private void Update()
    {
        if (Time.time < nextUpdate) return;
        nextUpdate = Time.time + updateInterval;

        float dt = Mathf.Min(Time.time - lastTick, updateInterval * 2f);
        lastTick = Time.time;

        if (!IsFloodActive() || trackedTarget == null || !riskPredictor.IsReady) return;

        GnnPrediction p = riskPredictor.GetPrediction(trackedTarget.position);
        if (!p.IsValid) return;

        GnnRiskClass previous = currentClass;
        currentClass = p.RiskClass;

        if (p.RiskClass == GnnRiskClass.Danger)
        {
            UpdateDanger(p, previous != GnnRiskClass.Danger, dt);
        }
        else if (p.RiskClass == GnnRiskClass.Caution && previous == GnnRiskClass.Safe)
        {
            if (Time.time - cautionLeftAt >= reentryCooldown && showDebugLogs)
                Debug.Log($"<color=orange>[GNN] DİKKAT ALANI (risk={p.RiskScore:F2}): yalnızca uyarı → Puan: {ScoreManager.CurrentScore}</color>");
        }

        if (previous == GnnRiskClass.Caution && p.RiskClass == GnnRiskClass.Safe) cautionLeftAt = Time.time;
    }

    private void UpdateDanger(GnnPrediction p, bool entering, float dt)
    {
        float w = p.DangerProbability;
        if (entering && warningSound != null) warningSound.Play();
        if (!entering) dangerTime += dt;

        if (dangerTime < dangerGraceTime)
        {
            if (entering && showDebugLogs)
                Debug.Log($"<color=orange>[GNN] TEHLİKELİ ALAN (p={w:F2}): uyarı, {dangerGraceTime - dangerTime:F1} s içinde uzaklaşılmazsa puan düşer</color>");
            return;
        }

        if (!entering) exposure += w * dt;

        float target = dangerBudget * (1f - Mathf.Exp(-exposure / exposureTimeConstant));
        int penalty = Mathf.RoundToInt(target) - deducted;
        if (penalty <= 0 || ScoreManager.Instance == null) return;

        deducted += penalty;
        ScoreManager.Instance.Deduct(penalty, null);
        if (showDebugLogs)
        {
            Debug.Log($"<color=red>[GNN] TEHLİKELİ ALAN (p={w:F2}, maruziyet={exposure:F1} s): -{penalty} " +
                      $"(tehlike toplam {deducted}/{Mathf.RoundToInt(dangerBudget)}) → Puan: {ScoreManager.CurrentScore}</color>");
        }
    }

    /// <summary>Senaryo sonunda oyuncunun konumuna göre ceza (ScoreManager.ShowFinalScore çağırır).</summary>
    public static void ApplyEndPositionPenalty(ScoreManager score)
    {
        GnnRiskScorer scorer = active != null ? active : FindObjectOfType<GnnRiskScorer>();
        Transform target = scorer != null && scorer.trackedTarget != null ? scorer.trackedTarget
            : Camera.main != null ? Camera.main.transform : null;
        if (target == null || score == null) return;

        GnnRiskClass zone = ClassifyAt(scorer, target.position, out string source);
        int caution = scorer != null ? scorer.endCautionPenalty : 30;
        int danger = scorer != null ? scorer.endDangerPenalty : 55;

        if (zone == GnnRiskClass.Danger)
            score.Deduct(danger, $"Sel sonunda tehlikeli alandaydı ({source})");
        else if (zone == GnnRiskClass.Caution)
            score.Deduct(caution, $"Sel sonunda zemin katta / su seviyesindeydi ({source})");
        else
            Debug.Log($"[Puan] Sel sonunda güvenli alandaydı ({source}): ek ceza yok");
    }

    private static GnnRiskClass ClassifyAt(GnnRiskScorer scorer, Vector3 position, out string source)
    {
        GnnRiskPredictor predictor = scorer != null ? scorer.riskPredictor : GnnRiskPredictor.Instance;
        if (predictor != null && predictor.IsReady)
        {
            GnnPrediction p = predictor.GetPrediction(position);
            if (p.IsValid)
            {
                source = $"GNN p_tehlike={p.DangerProbability:F2}";
                return p.RiskClass;
            }
        }

        if (SafeAreaController.isinSafeAreaController)
        {
            source = "güvenli alan kutusu";
            return GnnRiskClass.Safe;
        }

        // Yedek: başın altındaki zemini tahmin edip aynı etiket kuralını uygula
        GnnGraphSettings settings = predictor != null ? predictor.GraphSettings : new GnnGraphSettings();
        var hazards = predictor != null ? predictor.HazardLines() : new System.Collections.Generic.List<System.Collections.Generic.List<Vector3>>();
        Vector3 floor = position + Vector3.down * 1.5f;
        bool indoor = predictor != null && GnnGraphBuilder.InBuilding(floor, predictor.BuildingBounds(), settings)
                      && Physics.Raycast(floor + Vector3.up * 2f, Vector3.up, settings.overheadProbeDistance, settings.layerMask, QueryTriggerInteraction.Ignore);
        bool nearRiver = !indoor && floor.y < settings.riverLevel + settings.hazardVerticalRange;
        source = $"kural, zemin y={floor.y:F1}, {(indoor ? "iç" : "dış")} mekân";
        return GnnGraphBuilder.LabelAt(floor, indoor, nearRiver, settings, hazards);
    }

    private static bool IsFloodActive() => WaterRiseEffect.isRainStarted && !ScoreManager.IsFinalized;
}
