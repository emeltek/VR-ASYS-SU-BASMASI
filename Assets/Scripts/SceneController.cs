using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneController : MonoBehaviour
{
    [Header("VR Fade (Kararma) Ayarları")]
    [Tooltip("Sahneler arası geçişte ekranın kararma süresi")]
    public float fadeDuration = 1f;

    // VR projelerinde ekran kararmasını Canvas üzerindeki bir siyah görsel (Image) 
    // ile yapıyorsanız DOTween ile Alpha değerini değiştirebilirsiniz.
    // Eğer halihazırda bir VR Fade sistemi kullanmıyorsanız bu kısımlar opsiyoneldir.

    private void Start()
    {
        // Sahne ilk açıldığında ekranı yumuşakça aç
        TriggerFadeIn();
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. REFRESH BUTTON (Mevcut Sahneyi Yeniden Başlat)
    // ────────────────────────────────────────────────────────────────────────
    public void RefreshScene()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        StartCoroutine(LoadSceneRoutine(currentSceneIndex));
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. CHANGE SCENE BUTTON (Bir Sonraki Sahneye Geç - Index + 1)
    // ────────────────────────────────────────────────────────────────────────
    public void ChangeSceneNext()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = currentSceneIndex + 1;

        // Eğer bir sonraki index toplam sahne sayısından büyükse başa (0. sahneye) dön
        if (nextSceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            nextSceneIndex = 0; 
        }

        StartCoroutine(LoadSceneRoutine(nextSceneIndex));
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. EXIT BUTTON (Uygulamadan Çıkış)
    // ────────────────────────────────────────────────────────────────────────
    public void ExitGame()
    {
        Debug.Log("Uygulamadan çıkış yapılıyor...");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    // ────────────────────────────────────────────────────────────────────────
    // Sahne Geçiş Rutini (Fade Out -> Sahne Yükleme)
    // ────────────────────────────────────────────────────────────────────────
    private IEnumerator LoadSceneRoutine(int sceneIndex)
    {
        // 1. Varsa ekranı karart (VR konforu için)
        yield return StartCoroutine(FadeRoutine(1f)); // 1f = Tam siyah

        // 2. Sahneyi yükle
        SceneManager.LoadScene(sceneIndex);
    }

    private void TriggerFadeIn()
    {
        StartCoroutine(FadeRoutine(0f)); // 0f = Tam şeffaf (Görüntü gelir)
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        // Eğer projenizde bir Fade Image veya OVRScreenFade (Meta SDK) varsa 
        // buraya onun kodunu yazabilirsiniz. Şu anlık sadece süreyi bekletiyoruz:
        yield return new WaitForSeconds(fadeDuration);
    }
}