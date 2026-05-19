using UnityEngine;
using DG.Tweening;

public class WaterRiseEffect : MonoBehaviour
{
    [SerializeField] private Transform[] waterTransforms = new Transform[4]; // 4 adet su objesinin Transform'u
    [SerializeField] private float totalDuration = 120f; // Toplam 2 dakika
    [SerializeField] private float riseStartTime = 60f; // 1 dakika sonra başla
    [SerializeField] private float initialWaterHeight = 7.227219f;
    [SerializeField] private float finalWaterHeight = 9f;
    [SerializeField] private GameObject[] wetCapsules;
    [SerializeField] private float wetCapsuleDropDistance = 0.2f; // Aşağı inme mesafesi
    [SerializeField] private float wetCapsuleAnimDuration = 10f; // 10 saniyede aşağı git
    
    private float elapsedTime = 0f;
    private Vector3 initialPosition;
    private bool isRising = false;
    private bool wetCapsulesAnimated = false;

    private void Start()
    {
        if (waterTransforms == null || waterTransforms.Length == 0)
        {
            Debug.LogError("Water Transform'ları atanmamış!");
            return;
        }

        // Her bir su objesinin başlangıç Y değerini ayarla
        foreach (Transform water in waterTransforms)
        {
            if (water != null)
            {
                Vector3 newPos = water.position;
                newPos.y = initialWaterHeight;
                water.position = newPos;
            }
        }
        
        Debug.Log($"Su başlangıç yüksekliği: {initialWaterHeight} ({waterTransforms.Length} obje)");
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        // Toplam süre tamamlandı mı kontrol et
        if (elapsedTime >= totalDuration)
        {
            elapsedTime = totalDuration;
            if (!isRising)
            {
                Debug.Log("Su yükselmesi tamamlandı!");
            }
        }

        // 1 dakika sonra su yükselmesine başla
        if (elapsedTime >= riseStartTime)
        {
            isRising = true;
            
            // Wet Capsule animasyonunu başlat (sadece bir kez)
            if (!wetCapsulesAnimated)
            {
                AnimateWetCapsules();
                wetCapsulesAnimated = true;
            }
            
            // Kalan süreyü hesapla (60 saniye kaldı)
            float riseElapsedTime = elapsedTime - riseStartTime;
            float riseDuration = totalDuration - riseStartTime;
            float progress = Mathf.Clamp01(riseElapsedTime / riseDuration);
            
            // Su yüksekliğini hesapla (interpolation)
            float currentHeight = Mathf.Lerp(initialWaterHeight, finalWaterHeight, progress);
            
            // Her bir su objesinin Y değerini güncelle
            foreach (Transform water in waterTransforms)
            {
                if (water != null)
                {
                    Vector3 newPos = water.position;
                    newPos.y = currentHeight;
                    water.position = newPos;
                }
            }
            
            // Debug log her 10 saniyede bir
            if (Mathf.FloorToInt(riseElapsedTime) % 10 == 0 && riseElapsedTime < 1f)
            {
                Debug.Log($"Su yükseliyor! Yükseklik: {currentHeight:F3} | İlerleme: {progress * 100:F1}%");
            }
        }
        else
        {
            // İlk 60 saniye su sabit kalıyor
            foreach (Transform water in waterTransforms)
            {
                if (water != null)
                {
                    Vector3 newPos = water.position;
                    newPos.y = initialWaterHeight;
                    water.position = newPos;
                }
            }
        }

        // Toplam ilerleme
        if (Mathf.FloorToInt(elapsedTime) % 30 == 0 && elapsedTime < 1f)
        {
            Debug.Log($"Geçen Zaman: {elapsedTime:F1}s");
        }
    }

    // Wet Capsule'ları DOTween ile aşağı doğru hareket ettir
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
                // Mevcut pozisyonunu al
                Vector3 startPos = capsule.transform.position;
                // Hedef pozisyonu hesapla (Y ekseninde -0.2 birim)
                Vector3 endPos = startPos + Vector3.down * wetCapsuleDropDistance;
                
                // DOTween animasyonu: 10 saniyede aşağı git
                capsule.transform.DOMove(endPos, wetCapsuleAnimDuration)
                    .SetEase(Ease.InOutQuad)
                    .OnComplete(() => 
                    {
                        Debug.Log($"Wet Capsule '{capsule.name}' hareketi tamamlandı!");
                    });

                Debug.Log($"Wet Capsule '{capsule.name}' aşağı doğru hareket etmeye başladı!");
            }
        }
    }

    // İsteğe bağlı: Su yüksekliğini manuel olarak ayarla
    public void SetWaterHeight(float height)
    {
        foreach (Transform water in waterTransforms)
        {
            if (water != null)
            {
                Vector3 newPos = water.position;
                newPos.y = height;
                water.position = newPos;
            }
        }
    }

    // İsteğe bağlı: Süreci sıfırla
    public void ResetWater()
    {
        elapsedTime = 0f;
        isRising = false;
        wetCapsulesAnimated = false;
        
        foreach (Transform water in waterTransforms)
        {
            if (water != null)
            {
                Vector3 newPos = water.position;
                newPos.y = initialWaterHeight;
                water.position = newPos;
            }
        }
        
        // Wet Capsule'ları eski konumlarına geri al
        foreach (GameObject capsule in wetCapsules)
        {
            if (capsule != null)
            {
                capsule.transform.DOKill(); // Devam eden tüm DOTween animasyonlarını durdur
                Vector3 originalPos = capsule.transform.position + Vector3.up * wetCapsuleDropDistance;
                capsule.transform.position = originalPos;
            }
        }
        
        Debug.Log("Su süreci sıfırlandı!");
    }
}