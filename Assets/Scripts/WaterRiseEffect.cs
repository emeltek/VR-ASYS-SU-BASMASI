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

    [Header("Ekstra Obje Ayarları")]
    [SerializeField] private GameObject extraObject;      // Ayrı obje
    [SerializeField] private float extraObjectTargetY = 3.3f;

    // ── Özel hesaplanan yükseklikler ve pozisyonlar ──────────────────────────
    private float midWaterHeight;   // %15 yükseldikten sonraki seviye (60s sonunda)
    private Vector3 extraObjectInitialPos; // Reset için ekstra objenin ilk konumu

    // ── İç durum ─────────────────────────────────────────────────────────────
    private float elapsedTime = 0f;
    private bool isRising = false;
    private bool wetCapsulesAnimated = false;
    private bool postTotalAnimStarted = false; // totalDuration sonrası animasyonlar başladı mı
    private bool isAnimationActive = false;    // Butona basılıp basılmadığını kontrol eder

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

        // Tüm su objelerini başlangıç yüksekliğine ayarla (Sahne açılışında hazır beklerler)
        foreach (Transform water in waterTransforms)
        {
            if (water != null)
            {
                Vector3 pos = water.position;
                pos.y = initialWaterHeight;
                water.position = pos;
            }
        }

        // Ekstra objenin ilk pozisyonunu kaydet
        if (extraObject != null)
        {
            extraObjectInitialPos = extraObject.transform.position;
        }

        Debug.Log($"Sistem Hazır. Butona basılması bekleniyor... Su başlangıç: {initialWaterHeight}");
    }

    // ── Butona Bağlanacak Public Fonksiyon ────────────────────────────────────
    public void StartWaterSequence()
    {
        // Eğer zaten çalışıyorsa butona tekrar basılmasını engelle
        if (isAnimationActive) 
        {
            Debug.LogWarning("Animasyon süreci zaten çalışıyor!");
            return;
        }

        // Süreci tamamen sıfırla ve temiz bir başlangıç yap
        ResetWaterStateOnly();

        isAnimationActive = true;

        // Ekstra objenin DOTween animasyonunu buraya taşıdık (Butona basıldığı an başlar)
        if (extraObject != null)
        {
            StartExtraObjectAnimation();
        }

        Debug.Log("Su yükselme simülasyonu BUTON tetiklemesiyle BAŞLADI!");
    }

    // ────────────────────────────────────────────────────────────────────────
    private void Update()
    {
        // Butona basılmadıysa hiçbir şey yapma, Update'i boşa çalıştırma
        if (!isAnimationActive) return;

        elapsedTime += Time.deltaTime;

        // ── Faz 1: ilk 60 saniye ────────────────────────────────────────────
        if (elapsedTime < riseStartTime)
        {
            float progress = Mathf.Clamp01(elapsedTime / riseStartTime);
            float currentHeight = Mathf.Lerp(initialWaterHeight, midWaterHeight, progress);
            SetWaterHeight(currentHeight);
        }
        // ── Faz 2: 60s–120s arası (%85 yükseliş) ───────────────────────────
        else if (elapsedTime < totalDuration)
        {
            isRising = true;

            if (!wetCapsulesAnimated)
            {
                AnimateWetCapsules();
                wetCapsulesAnimated = true;
            }

            float riseElapsed = elapsedTime - riseStartTime;
            float riseDuration = totalDuration - riseStartTime;
            float progress = Mathf.Clamp01(riseElapsed / riseDuration);

            float currentHeight = Mathf.Lerp(midWaterHeight, finalWaterHeight, progress);
            SetWaterHeight(currentHeight);
        }
        // ── Faz 3: totalDuration (120s) sonrası ─────────────────────────────
        else
        {
            SetWaterHeight(finalWaterHeight);

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

        extraObject.transform.DOKill();
        extraObject.transform.position = extraObjectInitialPos;

        Vector3 targetPos = extraObject.transform.position;
        targetPos.y = extraObjectTargetY;

        float calculatedDuration = totalDuration + ramSplineAnimDuration;

        extraObject.transform.DOMove(targetPos, calculatedDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() => Debug.Log($"Ekstra obje Y={extraObjectTargetY} hedefine ulaştı!"));
    }

    // ── Faz 3 animasyonlarını başlat ─────────────────────────────────────────
    private void StartPostTotalAnimations()
    {
        if (ramSplineParent != null)
        {
            Transform child1 = ramSplineParent.transform.Find("RamSpline_2 (1)");
            Transform child2 = ramSplineParent.transform.Find("RamSpline_2 (2)");

            AnimateRamSplineChild(child1);
            AnimateRamSplineChild(child2);
        }
    }

    private void AnimateRamSplineChild(Transform child)
    {
        if (child == null) return;

        Vector3 targetLocalPos = child.localPosition;
        targetLocalPos.y = ramSplineTargetLocalY;

        child.DOLocalMove(targetLocalPos, ramSplineAnimDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() => Debug.Log($"'{child.name}' local Y={ramSplineTargetLocalY} hedefine ulaştı!"));
    }

    // ── Wet Capsule Animasyonu ────────────────────────────────────────────────
    private void AnimateWetCapsules()
    {
        if (wetCapsules == null || wetCapsules.Length == 0) return;

        foreach (GameObject capsule in wetCapsules)
        {
            if (capsule != null)
            {
                Vector3 endPos = capsule.transform.position + Vector3.down * wetCapsuleDropDistance;
                capsule.transform.DOMove(endPos, wetCapsuleAnimDuration)
                    .SetEase(Ease.InOutQuad);
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

    // Harici çağrılar için tam sıfırlama metodu (Süreci durdurur ve başa sarar)
    public void ResetWater()
    {
        isAnimationActive = false;
        ResetWaterStateOnly();
        Debug.Log("Su süreci tamamen sıfırlandı ve durduruldu!");
    }

    // İç durumsal reset mantığı
    private void ResetWaterStateOnly()
    {
        elapsedTime = 0f;
        isRising = false;
        wetCapsulesAnimated = false;
        postTotalAnimStarted = false;

        SetWaterHeight(initialWaterHeight);

        if (wetCapsules != null)
        {
            foreach (GameObject capsule in wetCapsules)
            {
                if (capsule != null)
                {
                    capsule.transform.DOKill();
                    capsule.transform.position = extraObjectInitialPos; // Veya orijinal konumu neyse
                }
            }
        }

        if (ramSplineParent != null)
        {
            ResetRamSplineChild(ramSplineParent.transform.Find("RamSpline_2 (1)"));
            ResetRamSplineChild(ramSplineParent.transform.Find("RamSpline_2 (2)"));
        }

        if (extraObject != null)
        {
            extraObject.transform.DOKill();
            extraObject.transform.position = extraObjectInitialPos;
        }
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