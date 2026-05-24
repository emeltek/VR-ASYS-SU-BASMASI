using UnityEngine;
using UnityEditor;

public class BuiltInMaterialFixer : EditorWindow
{
    [MenuItem("Tools/Pink Material Fixer (Built-in)")]
    public static void ShowWindow()
    {
        GetWindow<BuiltInMaterialFixer>("Material Fixer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Pembe (Error) Materyal Düzenleyici [Built-in]", EditorStyles.boldLabel);
        GUILayout.Label("Projedeki tüm bozuk/URP materyallerini klasik Standard Shader'a çevirir.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(15);

        if (GUILayout.Button("Projedeki Tüm Pembe Materyalleri Standart Yap", GUILayout.Height(40)))
        {
            FixPinkMaterialsToBuiltIn();
        }
    }

    private static void FixPinkMaterialsToBuiltIn()
    {
        // Projedeki tüm materyal assetlerini buluyoruz
        string[] materialGUIDs = AssetDatabase.FindAssets("t:Material");
        int fixedCount = 0;

        // Klasik Unity Standart Shader'ı
        Shader builtInStandardShader = Shader.Find("Standard");
        Shader errorShader = Shader.Find("Hidden/Internal-ErrorShader");

        if (builtInStandardShader == null)
        {
            Debug.LogError("<b>[Hata]</b> Klasik 'Standard' shader bulunamadı! Unity editöründe bir sorun olabilir.");
            return;
        }

        foreach (string guid in materialGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            // Shader error veriyorsa VEYA adı "Universal Render Pipeline" (URP) içeriyorsa yakala
            if (mat != null && (mat.shader == errorShader || mat.shader == null || mat.shader.name.Contains("Error") || mat.shader.name.Contains("Universal Render Pipeline") || mat.shader.name.Contains("URP")))
            {
                // URP materyalinden doku (Texture) ve renk (Color) bilgilerini çekmeye çalışıyoruz
                Texture mainTexture = null;
                Color mainColor = Color.white;
                Texture normalMap = null;

                // URP ana doku ismi genellikle _BaseMap'tir, klasik sistemde _MainTex'tir
                if (mat.HasProperty("_BaseMap")) mainTexture = mat.GetTexture("_BaseMap");
                else if (mat.HasProperty("_MainTex")) mainTexture = mat.GetTexture("_MainTex");

                // URP ana renk ismi _BaseColor veya _BaseMapColor'dır
                if (mat.HasProperty("_BaseColor")) mainColor = mat.GetColor("_BaseColor");
                else if (mat.HasProperty("_Color")) mainColor = mat.GetColor("_Color");

                // Normal Haritasını kontrol et
                if (mat.HasProperty("_BumpMap")) normalMap = mat.GetTexture("_BumpMap");

                // Shader'ı klasik Standard yapıyoruz
                mat.shader = builtInStandardShader;

                // Kurtardığımız verileri Built-in Standard shader özelliklerine geri yüklüyoruz
                if (mainTexture != null) mat.SetTexture("_MainTex", mainTexture);
                mat.SetColor("_Color", mainColor);
                
                if (normalMap != null)
                {
                    mat.SetTexture("_BumpMap", normalMap);
                    mat.EnableKeyword("_NORMALMAP"); // Normal map özelliğini shader'da aktif et
                }

                // Değişiklikleri kaydetmesi için Unity'yi tetikliyoruz
                EditorUtility.SetDirty(mat);
                fixedCount++;
                Debug.Log($"<b>[Düzeltildi]</b> Materyal Built-in Standard'a çevrildi: {path}", mat);
            }
        }

        if (fixedCount > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green><b>[İşlem Tamamlandı]</b></color> Toplam {fixedCount} adet materyal başarıyla klasik **Standard** shader'ına dönüştürüldü!");
        }
        else
        {
            Debug.Log("Çevrilecek pembe veya URP materyali bulunamadı.");
        }
    }
}