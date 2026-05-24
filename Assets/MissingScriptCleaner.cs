using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public class MissingScriptCleaner : EditorWindow
{
    [MenuItem("Tools/Missing Scripts Cleaner")]
    public static void ShowWindow()
    {
        GetWindow<MissingScriptCleaner>("Script Cleaner");
    }

    private void OnGUI()
    {
        GUILayout.Label("Missing Script Temizleyici", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Aktif Sahnedeki Missing Scriptleri Sil", GUILayout.Height(40)))
        {
            CleanActiveScene();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("Seçili Prefablerdeki Missing Scriptleri Sil", GUILayout.Height(40)))
        {
            CleanSelectedPrefabs();
        }
    }

    private static void CleanActiveScene()
    {
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        int totalRemoved = 0;

        foreach (GameObject rootObj in rootObjects)
        {
            // Root obje ve altındaki tüm çocukları tarar
            Transform[] allChildren = rootObj.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                if (count > 0)
                {
                    totalRemoved += count;
                    Debug.Log($"{child.name} objesinden {count} adet kayıp script silindi.", child.gameObject);
                }
            }
        }

        if (totalRemoved > 0)
        {
            // Sahnenin kaydedilmesi gerektiğini Unity'ye bildirir
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"<b>[Temizlik Tamamlandı]</b> Sahneden toplam {totalRemoved} adet missing script silindi!");
        }
        else
        {
            Debug.Log("Sahne temiz! Hiç missing script bulunamadı.");
        }
    }

    private static void CleanSelectedPrefabs()
    {
        // Project panelinde seçili olan tüm assetleri alır
        string[] guids = Selection.assetGUIDs;
        int totalRemoved = 0;

        foreach (string guid in guids)
            {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (prefab != null)
            {
                // Prefab'i düzenleme modunda açıp alt objelerini tarıyoruz
                Transform[] allChildren = prefab.GetComponentsInChildren<Transform>(true);
                bool isDirty = false;

                foreach (Transform child in allChildren)
                {
                    int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                    if (count > 0)
                    {
                        totalRemoved += count;
                        isDirty = true;
                        Debug.Log($"Prefab: {prefab.name} -> {child.name} objesinden {count} adet kayıp script silindi.", prefab);
                    }
                }

                if (isDirty)
                {
                    EditorUtility.SetDirty(prefab);
                }
            }
        }

        if (totalRemoved > 0)
            {
            AssetDatabase.SaveAssets();
            Debug.Log($"<b>[Temizlik Tamamlandı]</b> Prefablerden toplam {totalRemoved} adet missing script silindi!");
        }
        else
        {
            Debug.Log("Seçili prefablerde missing script bulunamadı. (Önce Project panelinden prefableri seçtiğinizden emin olun).");
        }
    }
}