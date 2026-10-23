using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sınıf indeksleri. Python eğitim betiğiyle (GNN/train_gnn.py) aynı sırada olmalı.
/// </summary>
public enum GnnRiskClass
{
    Safe = 0,     // Güvenli: son su seviyesinin belirgin üstü (üst kat)
    Caution = 1,  // Dikkat: su seviyesine yakın (zemin kat, evin önü)
    Danger = 2    // Tehlikeli: su altında kalan yer (bodrum, nehir kenarı) veya köprü
}

/// <summary>Bir düğüme en yakın tehlike kaynağı.</summary>
public enum GnnHazardType
{
    None = -1,
    Flood = 0
}

/// <summary>
/// Grafın sahneden nasıl çıkarılacağını belirleyen ayarlar. Mesafeler metre cinsindendir.
/// Sahne çok katlıdır: aynı (x, z) hücresinde her yürünebilir kat ayrı düğüm olur.
/// </summary>
[Serializable]
public class GnnGraphSettings
{
    [Tooltip("Grid hücre boyu (m). Her yürünebilir hücre / kat grafta bir düğüm olur.")]
    public float cellSize = 0.5f;

    [Tooltip("Alan sınırı: ev, güvenli alan ve tehlike noktalarının kapsadığı alan bu kadar (m) genişletilir. Site Area atanmışsa kullanılmaz.")]
    public float siteMargin = 8f;

    [Header("Taşkın")]
    [Tooltip("Senaryo sonundaki su seviyesi (dünya Y, m). Bodrum bunun altında, zemin kat yaklaşık bu seviyede kalır.")]
    public float floodLevel = 14.8f;

    [Tooltip("Etiket: su seviyesinin bu kadar (m) altındaki düğümler 'Tehlikeli' (su derinliği)")]
    public float dangerDepth = 0.5f;

    [Tooltip("Etiket: su seviyesinin en fazla bu kadar (m) üstündeki düğümler 'Dikkat'; daha yükseği 'Güvenli'")]
    public float safeHeight = 1.5f;

    [Tooltip("Etiket: nehir / köprü noktalarına bu yatay mesafeden (m) yakın düğümler 'Tehlikeli'")]
    public float hazardPointRadius = 3f;

    [Tooltip("Etiket: nehir / köprü noktası yalnızca bu dikey mesafedeki (m) katı etkiler (bodrum noktası zemin katı işaretlemesin)")]
    public float hazardPointVerticalRange = 1.5f;

    [Header("Özellikler")]
    [Tooltip("Su / güvenli alan arama mesafesi (m)")]
    public float hazardProbeDistance = 30f;

    [Tooltip("Yükseklik farkı normalizasyonu (m)")]
    public float heightScale = 5f;

    [Tooltip("Tavan taraması (m): iç mekân / açık alan")]
    public float overheadProbeDistance = 10f;

    [Tooltip("Yatay tarama mesafesi (m): çevre açıklığı")]
    public float clearanceProbeDistance = 3f;

    [Header("Zemin")]
    [Tooltip("Aynı hücrede iki katın sayılması için en az kat yüksekliği (m)")]
    public float minLevelSeparation = 1.8f;

    [Tooltip("Hücrenin boş sayılması için gövde yarıçapı (m)")]
    public float bodyRadius = 0.25f;

    [Tooltip("Yürünebilir sayılan en dik eğim (derece; merdiven rampaları dahil)")]
    public float maxSlope = 45f;

    [Tooltip("Komşu hücreler arasında en fazla yükseklik farkı (m; merdiven basamağı)")]
    public float maxStep = 0.5f;

    [Tooltip("Su yüzeyi olarak yok sayılacak obje isim anahtar kelimeleri (zemin / engel sayılmaz)")]
    public string[] waterKeywords = { "ramspline", "lake polygon", "lakepolygon", "river", "water", "nehir" };

    public LayerMask layerMask = ~0;

    [Tooltip("Grid başlangıç kaydırması (hücre cinsinden). Eğitim verisi çoğaltmak için kullanılır.")]
    public Vector2 gridOffset = Vector2.zero;

    public GnnGraphSettings Clone() => (GnnGraphSettings)MemberwiseClone();
}

/// <summary>
/// Zeminden çıkarılmış graf: düğüm = yürünebilir grid hücresi (çok katlı sahnede hücre başına
/// birden fazla kat), kenar = engelsiz komşu hücreler / merdiven. Komşuluk CSR formatında tutulur.
/// </summary>
public class GnnGraph
{
    public int FeatureCount;
    public float CellSize;
    public Vector3 GridOrigin;
    public Vector2 GridOffset;

    public readonly List<Vector3> Positions = new List<Vector3>(); // zemin noktası (dünya)
    public readonly List<Vector2Int> Cells = new List<Vector2Int>();
    public readonly Dictionary<Vector2Int, List<int>> CellToNodes = new Dictionary<Vector2Int, List<int>>();

    public float[] Features;   // N * FeatureCount
    public int[] Labels;       // N (yalnızca eğitim verisi dışa aktarılırken dolu)

    public int[] NeighborOffsets; // N + 1
    public int[] NeighborIndices; // toplam kenar

    public int NodeCount => Positions.Count;
    public int EdgeCount => NeighborIndices?.Length ?? 0;

    public void AddToCell(Vector2Int cell, int node)
    {
        if (!CellToNodes.TryGetValue(cell, out List<int> list)) CellToNodes[cell] = list = new List<int>(2);
        list.Add(node);
    }

    public Vector2Int CellOf(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt((worldPosition.x - GridOrigin.x) / CellSize - GridOffset.x),
            Mathf.FloorToInt((worldPosition.z - GridOrigin.z) / CellSize - GridOffset.y));
    }

    /// <summary>Dışa aktarılmış JSON'dan grafı geri kurar (runtime'da sahne taranmaz).</summary>
    public static GnnGraph FromExport(GnnGraphExport e)
    {
        var g = new GnnGraph
        {
            FeatureCount = e.numFeatures,
            CellSize = e.cellSize,
            GridOrigin = new Vector3(e.gridOrigin[0], e.gridOrigin[1], e.gridOrigin[2]),
            GridOffset = new Vector2(e.gridOffset[0], e.gridOffset[1]),
            Features = e.features,
            Labels = e.labels
        };

        for (int i = 0; i < e.numNodes; i++)
        {
            g.Positions.Add(new Vector3(e.positions[i * 3], e.positions[i * 3 + 1], e.positions[i * 3 + 2]));
            var cell = new Vector2Int(e.cells[i * 2], e.cells[i * 2 + 1]);
            g.Cells.Add(cell);
            g.AddToCell(cell, i);
        }

        // edgeSrc kaynak düğüme göre sıralı yazılır (bkz. ToExport), CSR doğrudan çıkarılır
        g.NeighborOffsets = new int[e.numNodes + 1];
        foreach (int s in e.edgeSrc) g.NeighborOffsets[s + 1]++;
        for (int i = 0; i < e.numNodes; i++) g.NeighborOffsets[i + 1] += g.NeighborOffsets[i];
        g.NeighborIndices = e.edgeDst;
        return g;
    }

    public GnnGraphExport ToExport(string sceneName, string[] featureNames, string[] classNames)
    {
        int n = NodeCount;
        var export = new GnnGraphExport
        {
            sceneName = sceneName,
            featureNames = featureNames,
            classNames = classNames,
            worldScale = 1f,
            cellSize = CellSize,
            gridOrigin = new[] { GridOrigin.x, GridOrigin.y, GridOrigin.z },
            gridOffset = new[] { GridOffset.x, GridOffset.y },
            numNodes = n,
            numFeatures = FeatureCount,
            positions = new float[n * 3],
            cells = new int[n * 2],
            features = Features,
            labels = Labels ?? new int[0],
            edgeSrc = new int[EdgeCount],
            edgeDst = new int[EdgeCount]
        };

        for (int i = 0; i < n; i++)
        {
            Vector3 p = Positions[i];
            export.positions[i * 3] = p.x;
            export.positions[i * 3 + 1] = p.y;
            export.positions[i * 3 + 2] = p.z;
            export.cells[i * 2] = Cells[i].x;
            export.cells[i * 2 + 1] = Cells[i].y;

            for (int e = NeighborOffsets[i]; e < NeighborOffsets[i + 1]; e++)
            {
                export.edgeSrc[e] = i;
                export.edgeDst[e] = NeighborIndices[e];
            }
        }

        return export;
    }
}

/// <summary>
/// Dışa aktarılan graf JSON'u. Python eğitimi bunu okur; runtime da aynı dosyadan grafı yükler.
/// </summary>
[Serializable]
public class GnnGraphExport
{
    public string sceneName;
    public string[] featureNames;
    public string[] classNames;
    public float worldScale;
    public float cellSize;
    public float[] gridOrigin; // 3
    public float[] gridOffset; // 2
    public int numNodes;
    public int numFeatures;
    public float[] positions; // N * 3
    public int[] cells;       // N * 2
    public float[] features;  // N * F
    public int[] labels;      // N
    public int[] edgeSrc;
    public int[] edgeDst;
}
