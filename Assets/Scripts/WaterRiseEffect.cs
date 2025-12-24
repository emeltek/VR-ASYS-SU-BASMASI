using UnityEngine;

public class WaterRiseEffect : MonoBehaviour
{
    [SerializeField] private Transform[] waterTransforms = new Transform[4]; // 4 adet su objesinin Transform'u
    [SerializeField] private float totalDuration = 120f; // Toplam 2 dakika
    [SerializeField] private float riseStartTime = 60f; // 1 dakika sonra başla
    [SerializeField] private float initialWaterHeight = 7.227219f;
    [SerializeField] private float finalWaterHeight = 9f;
    
    private float elapsedTime = 0f;
    private Vector3 initialPosition;
    private bool isRising = false;

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
        foreach (Transform water in waterTransforms)
        {
            if (water != null)
            {
                Vector3 newPos = water.position;
                newPos.y = initialWaterHeight;
                water.position = newPos;
            }
        }
        Debug.Log("Su süreci sıfırlandı!");
    }
}