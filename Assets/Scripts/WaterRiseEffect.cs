using UnityEngine;
using DG.Tweening;

public class WaterRiseEffect : MonoBehaviour
{
    [Header("Su Objeleri")]
    [SerializeField] private Transform[] waterTransforms = new Transform[6]; // 6 adet su objesi
    [SerializeField] private float totalDuration = 120f; // Toplam 2 dakika
    [SerializeField] private float riseStartTime = 60f;  // 1 dakika sonra başla

    [Header("Su Yükseklik Değerleri")]
    [SerializeField] private float initialWaterHeight = 7.227219f;
    [SerializeField] private float finalWaterHeight = 9f;

    [Header("Yükselme Oranları")]
    [SerializeField] [Range(0f, 1f)] private float firstPhaseRatio = 0.15f;  // İlk fazda %15
    [SerializeField] [Range(0f, 1f)] private float secondPhaseRatio = 0.85f; // İkinci fazda %85

    [Header("Wet Capsule Ayarları")]
    [SerializeField] private GameObject[] wetCapsules;
    [SerializeField] private float wetCapsuleDropDistance = 0.2f;
    [SerializeField] private float wetCapsuleAnimDuration = 10f;

    [Header("RamSpline Child Objeleri (Post-Total)")]
    [SerializeField] private GameObject ramSplineParent; // Parent objesi; child'ları RamSpline_2 (1) ve (2)
    [SerializeField] private float ramSplineTargetLocalY = -7.59f;
    [SerializeField] private float ramSplineAnimDuration = 60f; // 1 dakikada tamamla

    [Header("Ekstra Obje Ayarları (En Başta Başlar)")]
    [SerializeField] private GameObject extraObject;      // Ayrı obje
    [SerializeField] private float extraObjectTargetY = 3.3f;
    // Not: extraObjectAnimDuration kaldırıldı çünkü süre artık totalDuration + ramSplineAnimDuration olarak hesaplanıyor.

    // ── Özel hesaplanan yükseklikler ve pozisyonlar ──────────────────────────
    private float midWaterHeight;   // %15 yükseldikten sonraki seviye (60s sonunda)
    private Vector3 extraObjectInitialPos; // Reset için ekstra objenin ilk konumu

    // ── İç durum ─────────────────────────────────────────────────────────────
    private float elapsedTime = 0f;
    private bool isRising = false;
    private bool wetCapsulesAnimated = false;
    private bool postTotalAnimStarted = false; // totalDuration sonrası animasyonlar başladı mı

    // ────────────────────────────────────────────────────────────────────────
    private void Start()
    {
        if (waterTransforms == null || waterTransforms.Length == 0)
        {
            Debug.LogError("Water Transform'ları atanmamış!");
            return;
        }

        // Toplam yükselme mesafesi
        float totalRise = finalWaterHeight - initialWaterHeight;

        // %15'lik kısım → ilk 60 saniyede gidilecek yükseklik
        midWaterHeight = initialWaterHeight + totalRise * firstPhaseRatio;

        // Tüm su objelerini başlangıç yüksekliğine ayarla
        foreach (Transform water in waterTransforms)
        {
            if (water != null)
            {
                Vector3 pos = water.position;
                pos.y = initialWaterHeight;
                water.position = pos;
            }
        }

        // Ekstra objenin ilk pozisyonunu kaydet ve animasyonu EN BAŞTA başlat
        if (extraObject != null)
        {
            extraObjectInitialPos = extraObject.transform.position;
            StartExtraObjectAnimation();
        }

        Debug.Log($"Su başlangıç: {initialWaterHeight} | Ara hedef (%15): {midWaterHeight:F3} | Final (%85): {finalWaterHeight}");
    }

    // ────────────────────────────────────────────────────────────────────────
    private void Update()
    {
        elapsedTime += Time.deltaTime;

        // ── Faz 1: ilk 60 saniye ────────────────────────────────────────────
        if (elapsedTime < riseStartTime)
        {
            // Su yavaşça %15 seviyesine doğru yükseliyor
            float progress = Mathf.Clamp01(elapsedTime / riseStartTime);
            float currentHeight = Mathf.Lerp(initialWaterHeight, midWaterHeight, progress);
            SetWaterHeight(currentHeight);
        }
        // ── Faz 2: 60s–120s arası (%85 yükseliş) ───────────────────────────
        else if (elapsedTime < totalDuration)
        {
            isRising = true;

            // Wet Capsule animasyonunu tek sefer başlat
            if (!wetCapsulesAnimated)
            {
                AnimateWetCapsules();
                wetCapsulesAnimated = true;
            }

            float riseElapsed = elapsedTime - riseStartTime;
            float riseDuration = totalDuration - riseStartTime; // 60 saniye
            float progress = Mathf.Clamp01(riseElapsed / riseDuration);

            float currentHeight = Mathf.Lerp(midWaterHeight, finalWaterHeight, progress);
            SetWaterHeight(currentHeight);
        }
        // ── Faz 3: totalDuration (120s) sonrası ─────────────────────────────
        else
        {
            // Su nihai yükseklikte sabit kalsın
            SetWaterHeight(finalWaterHeight);

            // RamSpline animasyonlarını tek sefer başlat (Ekstra obje buradan kaldırıldı)
            if (!postTotalAnimStarted)
            {
                StartPostTotalAnimations();
                postTotalAnimStarted = true;
            }
        }
    }

    // ── Ekstra Obje Animasyonunu Başlatma Fonksiyonu ─────────────────────────
    private void StartExtraObjectAnimation()
    {
        if (extraObject == null) return;

        // Önce çalışan bir tween varsa durdur ve güvenli pozisyona al
        extraObject.transform.DOKill();
        extraObject.transform.position = extraObjectInitialPos;

        Vector3 targetPos = extraObject.transform.position;
        targetPos.y = extraObjectTargetY;

        // Süre dinamik hesaplanıyor: totalDuration + ramSplineAnimDuration
        float calculatedDuration = totalDuration + ramSplineAnimDuration;

        extraObject.transform.DOMove(targetPos, calculatedDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() => Debug.Log($"Ekstra obje Y={extraObjectTargetY} hedefine ulaştı!"));

        Debug.Log($"Ekstra obje '{extraObject.name}' en başta hareketine başladı. Hedef Süre: {calculatedDuration}s");
    }

    // ── Faz 3 animasyonlarını başlat ─────────────────────────────────────────
    private void StartPostTotalAnimations()
    {
        // --- RamSpline Child'ları ---
        if (ramSplineParent != null)
        {
            Transform child1 = ramSplineParent.transform.Find("RamSpline_2 (1)");
            Transform child2 = ramSplineParent.transform.Find("RamSpline_2 (2)");

            AnimateRamSplineChild(child1);
            AnimateRamSplineChild(child2);
        }
        else
        {
            Debug.LogWarning("RamSpline Parent objesi atanmamış!");
        }
    }

    private void AnimateRamSplineChild(Transform child)
    {
        if (child == null)
        {
            Debug.LogWarning("RamSpline child objesi bulunamadı!");
            return;
        }

        Vector3 targetLocalPos = child.localPosition;
        targetLocalPos.y = ramSplineTargetLocalY;

        child.DOLocalMove(targetLocalPos, ramSplineAnimDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() => Debug.Log($"'{child.name}' local Y={ramSplineTargetLocalY} hedefine ulaştı!"));

        Debug.Log($"'{child.name}' local Y={ramSplineTargetLocalY} hedefine doğru hareket başladı.");
    }

    // ── Wet Capsule Animasyonu ────────────────────────────────────────────────
    private void AnimateWetCapsules()
    {
        if (wetCapsules == null || wetCapsules.Length == 0)
        {
            Debug.LogWarning("Wet Capsule'lar atanmamış!");
            return;
        }

        foreach (GameObject capsule in wetCapsules)
        {
            if (capsule != null)
            {
                Vector3 endPos = capsule.transform.position + Vector3.down * wetCapsuleDropDistance;
                capsule.transform.DOMove(endPos, wetCapsuleAnimDuration)
                    .SetEase(Ease.InOutQuad)
                    .OnComplete(() => Debug.Log($"'{capsule.name}' hareketi tamamlandı!"));

                Debug.Log($"Wet Capsule '{capsule.name}' aşağı hareket başladı.");
            }
        }
    }

    // ── Public Yardımcı Metotlar ─────────────────────────────────────────────
    public void SetWaterHeight(float height)
    {
        foreach (Transform water in waterTransforms)
        {
            if (water != null)
            {
                Vector3 pos = water.position;
                pos.y = height;
                water.position = pos;
            }
        }
    }

    public void ResetWater()
    {
        elapsedTime = 0f;
        isRising = false;
        wetCapsulesAnimated = false;
        postTotalAnimStarted = false;

        SetWaterHeight(initialWaterHeight);

        // Wet Capsule'ları eski konumlarına geri al
        if (wetCapsules != null)
        {
            foreach (GameObject capsule in wetCapsules)
            {
                if (capsule != null)
                {
                    capsule.transform.DOKill();
                    capsule.transform.position += Vector3.up * wetCapsuleDropDistance;
                }
            }
        }

        // RamSpline child'larını sıfırla
        if (ramSplineParent != null)
        {
            ResetRamSplineChild(ramSplineParent.transform.Find("RamSpline_2 (1)"));
            ResetRamSplineChild(ramSplineParent.transform.Find("RamSpline_2 (2)"));
        }

        // Ekstra objeyi sıfırla ve en baştan tekrar oynat
        StartExtraObjectAnimation();

        Debug.Log("Su süreci sıfırlandı!");
    }

    private void ResetRamSplineChild(Transform child)
    {
        if (child == null) return;
        child.DOKill();
        Vector3 localPos = child.localPosition;
        localPos.y = 0f; 
        child.localPosition = localPos;
    }
}