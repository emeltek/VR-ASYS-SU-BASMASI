using UnityEngine;

public class VRMovementTracker : MonoBehaviour
{
    [Header("Zamanlama Ayarları")]
    [Tooltip("Hareketsiz kalma sınırı (saniye)")]
    public float immobilityThreshold = 15f; 
    
    [Header("Tolerans Ayarları")]
    [Tooltip("Çok küçük titremeleri (VR jitter) yok saymak için mesafe eşiği")]
    public float positionEpsilon = 0.05f;
    [Tooltip("Çok küçük baş hareketlerini yok saymak için açı eşiği")]
    public float rotationEpsilon = 1.0f;

    [Header("Ses ve Alan Ayarları")]
    public AudioSource WarningAudioSource;
    [Tooltip("Karakterin şu an güvenli alanda olup olmadığı")]
    public bool isInSafeArea = false;

    // Durum Takip Değişkenleri
    private Vector3 lastPosition;
    private float immobileTimer = 0f;
    private bool isAudioTriggered = false;

    void Start()
    {
        // Başlangıç pozisyon ve rotasyonlarını kaydet
        lastPosition = transform.position;
    }

    void Update()
    {
        // Eğer oyuncu safe area içindeyse zamanlayıcıyı sıfırla ve kontrol etme
        if (isInSafeArea)
        {
            ResetTimer();
            return;
        }

        // Karakter hareket etti mi veya kafasını çevirdi mi?
        bool hasMoved = Vector3.Distance(transform.position, lastPosition) > positionEpsilon;

        if (hasMoved)
        {
            // Eğer hareket varsa zamanlayıcıyı sıfırla
            ResetTimer();
            
            // Yeni pozisyonları güncelle
            lastPosition = transform.position;
        }
        else
        {
            // Hareket yoksa zamanı geriye say (arttır)
            immobileTimer += Time.deltaTime;

            // 15 saniye doldu mu ve ses daha önce çalmadı mı?
            if (immobileTimer >= immobilityThreshold && !isAudioTriggered)
            {
                TriggerWarningSound();
            }
        }
    }

    private void ResetTimer()
    {
        immobileTimer = 0f;
        if (isAudioTriggered)
        {
            // Oyuncu tekrar hareket ettiğinde sesi durdurmak istersen burayı açabilirsin:
            // if (WarningAudioSource.isPlaying) WarningAudioSource.Stop();
            isAudioTriggered = false;
        }
    }

    private void TriggerWarningSound()
    {
        if (WarningAudioSource != null && !WarningAudioSource.isPlaying && WaterRiseEffect.isRainStarted == true)
        {
            if (isInSafeArea)
            {
                return;
            }
            else
            {
                if (SafeAreaController.isinSafeAreaController == false)
                {
                    WarningAudioSource.Play();
                    isAudioTriggered = true;
                    
                    Debug.Log("Oyuncu 15 saniyedir safe area dışında ve hareketsiz! Ses çalınıyor.");
                }
               
            }
      
        }
    }

    // Dışarıdan tetiklemek için Safe Area giriş-çıkış fonksiyonları
    public void SetSafeAreaStatus(bool safe)
    {
        isInSafeArea = safe;
    }
}