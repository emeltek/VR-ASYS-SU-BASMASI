using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Açık sahneden GNN verisini üretir:
///   - GNN/data/&lt;sahne&gt;_graph_{0..3}.json : etiketli eğitim grafları (grid yarım hücre kaydırılarak 4 kopya)
///   - Assets/Resources/GNN/gnn_graph_&lt;sahne&gt;.json : oyunda kullanılan graf (runtime sahne taramaz)
/// Etiketler GnnGraphBuilder.ComputeLabels'tan gelir: evde su derinliği, dışarıda nehir yatağı / kenarı ve köprü.
/// </summary>
public static class GnnDatasetExporter
{
    private const string ExamScenePath = "Assets/Scenes/VR-ASYS-SU-BASMASI-Exam.unity";

    private static readonly Vector2[] GridOffsets =
    {
        new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f)
    };

    public static string DataDirectory => Path.Combine(Path.GetDirectoryName(Application.dataPath), "GNN", "data");
    private static string ResourcesDirectory => Path.Combine(Application.dataPath, "Resources", "GNN");

    [MenuItem("Tools/GNN/Eğitim Verisini ve Grafı Dışa Aktar (Açık Sahne)")]
    public static void ExportOpenScene()
    {
        string summary = Export();
        EditorUtility.DisplayDialog("GNN Dışa Aktarım", summary, "Tamam");
    }

    /// <summary>
    /// Komut satırı: Unity -batchmode -quit -projectPath . -executeMethod GnnDatasetExporter.ExportFromCommandLine
    /// </summary>
    public static void ExportFromCommandLine()
    {
        EditorSceneManager.OpenScene(ExamScenePath, OpenSceneMode.Single);
        Debug.Log(Export());
    }

    private static string Export()
    {
        GnnRiskPredictor predictor = Object.FindObjectOfType<GnnRiskPredictor>();
        GameObject temp = null;
        if (predictor == null)
        {
            temp = new GameObject("GNN Exporter (temp)") { hideFlags = HideFlags.HideAndDontSave };
            predictor = temp.AddComponent<GnnRiskPredictor>();
        }

        try
        {
            string sceneName = EditorSceneManager.GetActiveScene().name;
            string fileStem = GnnRiskPredictor.GraphResourceName(sceneName).Substring(GnnRiskPredictor.GraphResourcePrefix.Length);
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(ResourcesDirectory);
            foreach (string old in Directory.GetFiles(DataDirectory, fileStem + "_graph_*.json")) File.Delete(old);

            var log = new System.Text.StringBuilder();
            List<List<Vector3>> hazards = predictor.HazardLines();
            Bounds building = predictor.BuildingBounds();
            if (building.size == Vector3.zero)
                return "Ev alanı bulunamadı (FirstFloorController / SafeAreaController kutusu ya da GnnRiskPredictor > Building Area).";
            log.AppendLine($"Ev alanı: x {building.min.x:F1}–{building.max.x:F1}, z {building.min.z:F1}–{building.max.z:F1}");

            for (int k = 0; k < GridOffsets.Length; k++)
            {
                GnnGraphSettings settings = predictor.GraphSettings.Clone();
                settings.gridOffset = GridOffsets[k];

                GnnGraphBuilder builder = predictor.CreateBuilder(settings);
                if (!builder.HasArea) return "Saha alanı hesaplanamadı.";
                if (k == 0)
                {
                    Bounds a = builder.Area;
                    log.AppendLine($"Alan: x {a.min.x:F0}–{a.max.x:F0}, y {a.min.y:F1}–{a.max.y:F1}, z {a.min.z:F0}–{a.max.z:F0} | " +
                                   $"su seviyesi {settings.floodLevel:F2} m | tehlike noktası {builder.HazardPointCount}, güvenli alan {builder.SafeAreaCount}, " +
                                   $"yok sayılan su collider'ı {builder.IgnoredWaterColliders}");
                    if (builder.HazardPointCount == 0)
                        return "Tehlike noktası bulunamadı. KnnDangerZoneSystem bölgelerinde nokta olmalı ya da GnnRiskPredictor > Hazard Points doldurulmalı.";
                    if (builder.SafeAreaCount == 0)
                        return "Güvenli alan bulunamadı (SafeAreaController bileşenli BoxCollider).";
                }

                GnnGraph graph = builder.BuildImmediate();
                graph.Labels = GnnGraphBuilder.ComputeLabels(graph, settings, hazards, building);
                GnnGraphExport export = graph.ToExport(sceneName, GnnGraphBuilder.FeatureNames, GnnGraphBuilder.ClassNames);
                string json = JsonUtility.ToJson(export);

                File.WriteAllText(Path.Combine(DataDirectory, $"{fileStem}_graph_{k}.json"), json);
                if (k == 0) File.WriteAllText(Path.Combine(ResourcesDirectory, $"gnn_graph_{fileStem}.json"), json);

                int[] counts = new int[3];
                foreach (int l in graph.Labels) counts[l]++;
                log.AppendLine($"graph_{k}: {graph.NodeCount} düğüm ({builder.DroppedNodes} ulaşılamayan atıldı), {graph.EdgeCount / 2} kenar | " +
                               $"Güvenli {counts[0]}, Dikkat {counts[1]}, Tehlikeli {counts[2]}");
                if (k == 0) log.AppendLine(LevelSummary(graph));
            }

            AssetDatabase.Refresh();
            log.AppendLine("Eğitim verisi: " + DataDirectory);
            log.AppendLine($"Oyun grafı: Assets/Resources/GNN/gnn_graph_{fileStem}.json");
            return log.ToString();
        }
        finally
        {
            if (temp != null) Object.DestroyImmediate(temp);
        }
    }

    /// <summary>Düğüm yüksekliklerinin 0.5 m'lik dağılımı: katların (bodrum / zemin / üst) doğru bulunduğunu görmek için.</summary>
    private static string LevelSummary(GnnGraph graph)
    {
        var bins = new SortedDictionary<int, int>();
        foreach (Vector3 p in graph.Positions)
        {
            int key = Mathf.FloorToInt(p.y * 2f);
            bins[key] = bins.TryGetValue(key, out int c) ? c + 1 : 1;
        }
        var sb = new System.Text.StringBuilder("Yükseklik dağılımı (m: düğüm): ");
        foreach (var kv in bins)
            if (kv.Value >= 10) sb.Append($"{kv.Key * 0.5f:F1}: {kv.Value}  ");
        return sb.ToString();
    }
}
