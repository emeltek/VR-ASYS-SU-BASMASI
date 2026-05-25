using UnityEngine;

public class RainManager : MonoBehaviour
{
    private ParticleSystem rainParticles;
    private ParticleSystem.EmissionModule emissionModule;

    [Header("Particle Limit Ayarları")]
    [SerializeField] private int maxSystemPool = 15000; // Sistemin hafızada açacağı maksimum particle limiti

    [Header("Zaman ve Yoğunluk Ayarları")]
    [SerializeField] private float accelerationDuration = 120f; // Yağmurun en yoğun hale gelme süresi (2 dakika)
    [SerializeField] private float startEmissionRate = 10f;     // Başlangıçtaki saniye başına damla sayısı
    [SerializeField] private float endEmissionRate = 2000f;     // En yoğun andaki saniye başına damla sayısı
    [SerializeField] private float prewarmTime = 3f;           // Yağmur başlarken kaç saniyedir yağıyormuş gibi havada asılı başlasın?
    private float elapsedTime = 0f;
    private bool isAccelerating = false;

    private void Start()
    {
        rainParticles = GetComponent<ParticleSystem>();
        
        if (rainParticles == null)
        {
            Debug.LogError("Bu objede ParticleSystem bileşeni bulunamadı!");
            return;
        }

        // Modülleri ve havuz limitini ilk başta bir kez ayarlıyoruz
        emissionModule = rainParticles.emission;
        
        var mainModule = rainParticles.main;
        mainModule.maxParticles = maxSystemPool;

        // Oyun ilk açıldığında yağmurun otomatik başlamasını engellemek için durduruyoruz
        SetEmissionRate(0f);
        rainParticles.Stop();
    }

    private void Update()
    {
        // Eğer start verilmediyse Update fonksiyonunu boşa çalıştırma
        if (!isAccelerating) return;

        elapsedTime += Time.deltaTime;

        // Belirlenen süre tamamlandı mı kontrolü
        if (elapsedTime >= accelerationDuration)
        {
            elapsedTime = accelerationDuration;
            isAccelerating = false; // Süre bittiğinde artışı durdur (Maksimumda sabit kalır)
            Debug.Log("Yağmur maksimum yoğunluğa ulaştı!");
        }

        // Zaman ilerledikçe lineer olarak emisyon hızını artır
        float progress = elapsedTime / accelerationDuration;
        float currentRate = Mathf.Lerp(startEmissionRate, endEmissionRate, progress);
        
        SetEmissionRate(currentRate);

        // Her 10 saniyede bir konsola durum raporu yazdırır (Performans dostu kontrol)
        if (Time.frameCount % 300 == 0 && elapsedTime < accelerationDuration)
        {
            Debug.Log($"Yağmur Süresi: {elapsedTime:F1}/{accelerationDuration}s | Güncel Damla Hızı: {currentRate:F0}/sn");
        }
    }

    // ── BAŞKA SCRIPT'LERDEN ÇAĞRILACAK METODLAR ─────────────────────────────

    /// <summary>
    /// Yağmuru prewarm ile başlatır ve kademeli artış sürecini devreye sokar.
    /// </summary>
    public void StartRainSequence()
    {
        if (isAccelerating || rainParticles.isPlaying)
        {
            Debug.LogWarning("Yağmur zaten aktif veya hızlanma sürecinde!");
            return;
        }

        elapsedTime = 0f;
        isAccelerating = true;

        // İlk saniye üretim hızını ayarla
        SetEmissionRate(startEmissionRate);

        // İlk kodundaki gibi gökten damla düşmesini beklemeden sahneyi yağmurla doldurur
        rainParticles.Simulate(prewarmTime, true, true);
        rainParticles.Play();

        Debug.Log("Yağmur simülasyonu BAŞLADI! Kademeli olarak hızlanıyor...");
    }

    /// <summary>
    /// Yağmurun artışını durdurur, yağmur o anki şiddetinde kalır.
    /// </summary>
    public void PauseAcceleration()
    {
        isAccelerating = false;
        Debug.Log("Yağmurun şiddetlenmesi durduruldu, mevcut seviyede sabitlendi.");
    }

    /// <summary>
    /// Yağmuru tamamen keser ve sistemi sıfırlar.
    /// </summary>
    public void StopAndResetRain()
    {
        isAccelerating = false;
        elapsedTime = 0f;
        SetEmissionRate(0f);
        rainParticles.Stop();
        rainParticles.Clear(); // Ekrandaki tüm particle'ları anında temizler
        Debug.Log("Yağmur tamamen durduruldu ve sıfırlandı.");
    }

    // ── İÇ YARDIMCI FONKSİYON ────────────────────────────────────────────────
    private void SetEmissionRate(float rate)
    {
        var rateOverTime = emissionModule.rateOverTime;
        rateOverTime.constant = rate;
        emissionModule.rateOverTime = rateOverTime;
    }
}