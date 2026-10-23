using UnityEngine;

/// <summary>
/// Statik değişkenler sahne geçişinde (eğitim → sınav, yeniden başlat) sıfırlanmaz.
/// Örn. eğitimde başlayan yağmur (isRainStarted) sınavda senaryo başlamadan görevleri tetikliyordu.
/// Her sahne yüklenişinde sel ve puan durumunu sıfırlar.
/// </summary>
public static class StaticStateReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        WaterRiseEffect.isRainStarted = false;
        WaterRiseEffect.isWaterMaxHight = false;
        FirstFloorController.isCharacterEntered = false;
        SafeAreaController.isinSafeAreaController = false;
        ScoreManager.ResetState();
    }
}
