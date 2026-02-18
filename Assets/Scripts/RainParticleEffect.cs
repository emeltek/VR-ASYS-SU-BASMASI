using UnityEngine;

public class RainParticleEffect : MonoBehaviour
{
    private ParticleSystem rainParticles;
    
    [SerializeField] private float accelerationDuration = 120f; // 2 dakika
    [SerializeField] private int minParticles = 1;
    [SerializeField] private int maxParticles = 10000;
    
    private float elapsedTime = 0f;
    private bool isAccelerating = true;

    private void Start()
    {
        rainParticles = GetComponent<ParticleSystem>();
        
        if (rainParticles == null)
        {
            Debug.LogError("ParticleSystem component bulunamadı!");
            return;
        }

        // İlk ayarlamalar
        SetParticleCount(minParticles);
    }

    private void Update()
    {
        if (!isAccelerating) return;

        elapsedTime += Time.deltaTime;

        // 2 dakika tamamlandı mı kontrol et
        if (elapsedTime >= accelerationDuration)
        {
            elapsedTime = accelerationDuration;
            isAccelerating = false;
            Debug.Log("Yağmur particle accelerasyonu tamamlandı!");
        }

        // Şu anki particle sayısını hesapla (linear interpolation)
        float progress = elapsedTime / accelerationDuration;
        int currentParticleCount = Mathf.RoundToInt(Mathf.Lerp(minParticles, maxParticles, progress));
        
        SetParticleCount(currentParticleCount);
        
        // Debug log her 10 saniyede bir
        if (elapsedTime % 10f < Time.deltaTime)
        {
            Debug.Log($"Geçen Zaman: {elapsedTime:F1}s | Particle Sayısı: {currentParticleCount}");
        }
    }

    private void SetParticleCount(int count)
    {
        var main = rainParticles.main;
        main.maxParticles = count;
        
        var emission = rainParticles.emission;
       // emission.rateOverTime = count / accelerationDuration;
    }

    // İsteğe bağlı: Manuel başlat/durdur fonksiyonları
    public void StartAcceleration()
    {
        isAccelerating = true;
        elapsedTime = 0f;
    }

    public void StopAcceleration()
    {
        isAccelerating = false;
    }

    public void ResetAcceleration()
    {
        elapsedTime = 0f;
        isAccelerating = true;
        SetParticleCount(minParticles);
    }
}