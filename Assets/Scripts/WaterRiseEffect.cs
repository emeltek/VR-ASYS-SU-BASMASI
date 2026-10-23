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

    // ── RamSpline Orijinal Konum Değişkenleri (YENİ) ─────────────────────────
    private Vector3 child1InitialLocalPos;
    private Vector3 child2InitialLocalPos;
    private Transform child1;
    private Transform child2;

    // ── İç durum ─────────────────────────────────────────────────────────────
    private float elapsedTime = 0f;
    private bool isRising = false;
    private bool wetCapsulesAnimated = false;
    private bool postTotalAnimStarted = false; // totalDuration sonrası animasyonlar başladı mı
    private bool isAnimationActive = false;    // Butona basılıp basılmadığını kontrol eder

    // ────────────────────────────────────────────────────────────────────────

    public static bool isRainStarted = false;
    public static bool isWaterMaxHight = false;
    public FirstFloorController firstFloorController;
    public GameObject secondUI;
    public AudioSource ramSplineAudioSource;
    public bool isExamScene = false;
    public GameObject scoreUI;
    private void Start()
    {
        secondUI.SetActive(false);

        // Puan paneli (ScoreMesaj) sahnede açık başlıyor; yalnızca senaryo sonunda gösterilir
        if (isExamScene && scoreUI != null) scoreUI.SetActive(false);
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

        // RamSpline child referanslarını ve mevcut (başlangıç) konumlarını kaydet (YENİ)
        if (ramSplineParent != null)
        {
            child1 = ramSplineParent.transform.Find("RamSpline_2 (1)");
            child2 = ramSplineParent.transform.Find("RamSpline_2 (2)");

            if (child1 != null) child1InitialLocalPos = child1.localPosition;
            if (child2 != null) child2InitialLocalPos = child2.localPosition;
        }

        Debug.Log($"Sistem Hazır. Butona basılması bekleniyor... Su başlangıç: {initialWaterHeight}");
    }

    // ── Butona Bağlanacak Public Fonksiyon ────────────────────────────────────
    public void StartWaterSequence()
    {
        isRainStarted = true;
        if (isAnimationActive) 
        {
            Debug.LogWarning("Animasyon süreci zaten çalışıyor!");
            return;
        }

        // Süreci tamamen sıfırla ve temiz bir başlangıç yap
        ResetWaterStateOnly();

        isAnimationActive = true;

        if (extraObject != null)
        {
            StartExtraObjectAnimation();
        }

        Debug.Log("Su yükselme simülasyonu BUTON tetiklemesiyle BAŞLADI!");
    }

    // ────────────────────────────────────────────────────────────────────────
    private void Update()
    {
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
        // Artık sıfırdan değil, bulundukları mevcut local konumlardan hedefe doğru tween yapacaklar
        AnimateRamSplineChild(child1);
        AnimateRamSplineChild(child2);
    }

    private void AnimateRamSplineChild(Transform child)
    {
        if (child == null) return;

        child.DOKill(); 

        Vector3 targetLocalPos = child.localPosition; 
        targetLocalPos.y = ramSplineTargetLocalY;

        child.DOLocalMove(targetLocalPos, ramSplineAnimDuration)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() => {
                firstFloorController.FirstFloorCheck();
                isWaterMaxHight = true;
                secondUI.SetActive(true);
                Debug.Log($"'{child.name}' local Y={ramSplineTargetLocalY} hedefine ulaştı!");

                // ── YENİ: OnComplete olduktan sonra 10 saniye sayar ve sesi çalar
                // Çift ses çalmasını engellemek için sadece ilk tamamlanan child üzerinden kontrol sağlıyoruz
                if (child.name == "RamSpline_2 (1)" && ramSplineAudioSource != null)
                {
                    DOVirtual.DelayedCall(10f, () => 
                    {
                        if (ramSplineAudioSource != null && !ramSplineAudioSource.isPlaying)
                        {
                            if (isExamScene == false)
                            {
                                ramSplineAudioSource.Play();

                            }
                            else
                            {
                                ScoreManager.Instance.ShowFinalScore();
                                if (scoreUI != null) scoreUI.SetActive(true);
                            }
                            Debug.Log("Animasyon bittikten 10 saniye geçti, ses çalınıyor.");
                        }
                    });
                }
            });
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

    public void ResetWater()
    {
        isAnimationActive = false;
        ResetWaterStateOnly();
        Debug.Log("Su süreci tamamen sıfırlandı ve durduruldu!");
    }

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
                    // Not: Kapsüllerin kendi orijinal pozisyon reset mantığını projenize göre buraya kurabilirsiniz.
                }
            }
        }

        // Objeleri 0'a değil, Start fonksiyonunda kaydettiğimiz kendi orijinal konumlarına sıfırlıyoruz (YENİ)
        ResetRamSplineChild(child1, child1InitialLocalPos);
        ResetRamSplineChild(child2, child2InitialLocalPos);

        if (extraObject != null)
        {
            extraObject.transform.DOKill();
            extraObject.transform.position = extraObjectInitialPos;
        }
    }

    // Reset fonksiyonunu orijinal pozisyonu parametre alacak şekilde güncelledik (YENİ)
    private void ResetRamSplineChild(Transform child, Vector3 initialLocalPos)
    {
        if (child == null) return;
        child.DOKill();
        child.localPosition = initialLocalPos;
    }
}