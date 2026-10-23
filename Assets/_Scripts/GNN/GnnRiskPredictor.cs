using System.Collections.Generic;
using UnityEngine;

public struct GnnPrediction
{
    public bool IsValid;
    public float SafeProbability;
    public float CautionProbability;
    public float DangerProbability;
    public GnnRiskClass RiskClass;
    public GnnHazardType Hazard;

    /// <summary>0 (tamamen güvenli) – 1 (tamamen tehlikeli) arası sürekli risk skoru.</summary>
    public float RiskScore => Mathf.Clamp01(DangerProbability + 0.5f * CautionProbability);
}

/// <summary>
/// Sel (su baskını) risk haritası. Ev ve arazi sabit olduğu için graf Editor'de bir kez çıkarılıp
/// Resources/GNN/gnn_graph_&lt;sahne&gt;.json olarak kaydedilir; runtime'da sahne taranmaz,
/// yalnızca eğitilmiş GNN sahne açılışında bir kez çalıştırılır (Quest'te fizik maliyeti yok).
/// Puanlama sırasında oyuncunun konumuna en yakın düğümün tahmini döndürülür.
/// </summary>
public class GnnRiskPredictor : MonoBehaviour
{
    public const string DefaultWeightsResource = "GNN/gnn_weights";
    public const string GraphResourcePrefix = "GNN/gnn_graph_";

    public static GnnRiskPredictor Instance { get; private set; }

    [Header("Model ve Graf")]
    [Tooltip("Boşsa Resources/GNN/gnn_weights.json yüklenir")]
    [SerializeField] private TextAsset weightsJson;
    [Tooltip("Boşsa Resources/GNN/gnn_graph_<sahne>.json yüklenir (Tools > GNN ile üretilir)")]
    [SerializeField] private TextAsset graphJson;

    [Header("Graf Çıkarma (yalnızca Editor dışa aktarımında kullanılır)")]
    [Tooltip("Boşsa alan; ev, güvenli alan, oyuncu ve tehlike noktalarının çevresi Site Margin kadar genişletilerek bulunur")]
    [SerializeField] private BoxCollider siteArea;
    [Tooltip("Nehir / köprü / bodrum noktaları. Boşsa KnnDangerZoneSystem bölgelerinin noktaları kullanılır.")]
    [SerializeField] private Transform[] hazardPoints;
    [Tooltip("Ev alanı (iç mekân etiketleri). Boşsa kat (FirstFloorController) ve güvenli alan kutularının birleşimi kullanılır.")]
    [SerializeField] private BoxCollider buildingArea;
    [SerializeField] private GnnGraphSettings graphSettings = new GnnGraphSettings();

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [Tooltip("Açık: oyunda kullanılan karar (en olası sınıf). Kapalı: olasılıkların karışımı (sarımsı = kararsız)")]
    [SerializeField] private bool gizmoShowsDecision = true;
    [SerializeField] private bool showDebugLogs = true;

    private GnnModel model;
    private GnnGraph graph;
    private float[] probabilities;
    private int[] previewLabels;

    public bool IsReady => graph != null && probabilities != null && probabilities.Length == graph.NodeCount * 3;
    public GnnGraph Graph => graph;
    public GnnGraphSettings GraphSettings => graphSettings;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        LoadModel();
        LoadGraph();
        PredictNow();
    }

    public void PredictNow()
    {
        probabilities = null;
        if (model == null || graph == null) return;

        float start = Time.realtimeSinceStartup;
        probabilities = model.Predict(graph);

        if (showDebugLogs)
        {
            int[] counts = new int[3];
            for (int i = 0; i < graph.NodeCount; i++) counts[(int)ArgMax(i)]++;
            Debug.Log($"<color=cyan>[GNN] Risk haritası hesaplandı ({(Time.realtimeSinceStartup - start) * 1000f:F1} ms, {graph.NodeCount} düğüm): " +
                      $"Güvenli {counts[0]}, Dikkat {counts[1]}, Tehlikeli {counts[2]}.</color>");
        }
    }

    /// <summary>Konumun risk tahmini. Graf dışındaysa (zemin dışı, yapının içi) IsValid = false.</summary>
    public GnnPrediction GetPrediction(Vector3 worldPosition)
    {
        var result = new GnnPrediction { Hazard = GnnHazardType.None };
        if (!IsReady) return result;

        int node = FindNearestNode(worldPosition);
        if (node < 0) return result;

        result.IsValid = true;
        result.SafeProbability = probabilities[node * 3 + 0];
        result.CautionProbability = probabilities[node * 3 + 1];
        result.DangerProbability = probabilities[node * 3 + 2];
        result.RiskClass = ArgMax(node);
        result.Hazard = NearestHazard(node);
        return result;
    }

    private GnnRiskClass ArgMax(int node)
    {
        int best = 0;
        for (int c = 1; c < 3; c++)
        {
            if (probabilities[node * 3 + c] > probabilities[node * 3 + best]) best = c;
        }
        return (GnnRiskClass)best;
    }

    /// <summary>Tek tehlike türü sel: düğüm su seviyesinin altındaysa ya da nehre yakınsa Flood.</summary>
    private GnnHazardType NearestHazard(int node)
    {
        int f = graph.FeatureCount;
        bool below = graph.Features[node * f] < 0f;
        bool nearWater = graph.Features[node * f + 1] < 1f;
        return below || nearWater ? GnnHazardType.Flood : GnnHazardType.None;
    }

    /// <summary>
    /// Konumun bulunduğu kattaki en yakın düğüm. Konum oyuncunun başı (kamera) ya da gövdesidir:
    /// düğüm konumun en fazla 3 m altında, en fazla 0.5 m üstünde olabilir; tercih başın ~1.5 m altındaki kattır.
    /// </summary>
    private int FindNearestNode(Vector3 worldPosition)
    {
        Vector2Int c = graph.CellOf(worldPosition);
        const int searchRadius = 2;
        int best = -1;
        float bestCost = float.MaxValue;
        for (int dx = -searchRadius; dx <= searchRadius; dx++)
        {
            for (int dz = -searchRadius; dz <= searchRadius; dz++)
            {
                if (!graph.CellToNodes.TryGetValue(new Vector2Int(c.x + dx, c.y + dz), out List<int> nodes)) continue;
                foreach (int node in nodes)
                {
                    Vector3 q = graph.Positions[node];
                    float dy = worldPosition.y - q.y;
                    if (dy < -0.5f || dy > 3f) continue;
                    float horizontal = (q.x - worldPosition.x) * (q.x - worldPosition.x) + (q.z - worldPosition.z) * (q.z - worldPosition.z);
                    float cost = horizontal + 0.25f * (dy - 1.5f) * (dy - 1.5f);
                    if (cost < bestCost) { bestCost = cost; best = node; }
                }
            }
        }
        return best;
    }

    private void LoadModel()
    {
        TextAsset asset = weightsJson != null ? weightsJson : Resources.Load<TextAsset>(DefaultWeightsResource);
        if (asset == null)
        {
            Debug.LogError($"[GNN] Model ağırlıkları bulunamadı (Resources/{DefaultWeightsResource}.json). Önce GNN/train_gnn.py ile modeli eğitin.");
            return;
        }

        model = GnnModel.FromJson(asset.text, out string error);
        if (model == null) Debug.LogError("[GNN] " + error);
    }

    private void LoadGraph()
    {
        string resource = GraphResourceName(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        TextAsset asset = graphJson != null ? graphJson : Resources.Load<TextAsset>(resource);
        if (asset == null)
        {
            Debug.LogError($"[GNN] Graf bulunamadı (Resources/{resource}.json). Unity'de Tools > GNN > Eğitim Verisini ve Grafı Dışa Aktar çalıştırın.");
            return;
        }

        var export = JsonUtility.FromJson<GnnGraphExport>(asset.text);
        if (export == null || export.numFeatures != GnnGraphBuilder.FeatureCount)
        {
            Debug.LogError("[GNN] Graf dosyası güncel değil (özellik sayısı uyuşmuyor). Grafı yeniden dışa aktarın.");
            return;
        }
        graph = GnnGraph.FromExport(export);
        probabilities = null; // eski grafın tahminleri yeni grafla kullanılmasın
    }

    /// <summary>Sahne adını Resources yolunda güvenli ASCII'ye çevirir (ör. "Sınav" → "Sinav").</summary>
    public static string GraphResourceName(string sceneName)
    {
        var sb = new System.Text.StringBuilder(GraphResourcePrefix);
        foreach (char ch in sceneName)
        {
            switch (ch)
            {
                case 'ı': sb.Append('i'); break;
                case 'İ': sb.Append('I'); break;
                case 'ş': sb.Append('s'); break;
                case 'Ş': sb.Append('S'); break;
                case 'ğ': sb.Append('g'); break;
                case 'Ğ': sb.Append('G'); break;
                case 'ü': sb.Append('u'); break;
                case 'Ü': sb.Append('U'); break;
                case 'ö': sb.Append('o'); break;
                case 'Ö': sb.Append('O'); break;
                case 'ç': sb.Append('c'); break;
                case 'Ç': sb.Append('C'); break;
                default: sb.Append(ch < 128 && (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_') ? ch : '_'); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>Editor dışa aktarıcısı da aynı builder'ı kullanır.</summary>
    public GnnGraphBuilder CreateBuilder(GnnGraphSettings settings)
    {
        var ignoreRoots = new List<Transform>();
        var players = new List<Vector3>();
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Player"))
        {
            ignoreRoots.Add(go.transform);
            players.Add(go.transform.position);
        }

        // Tetik kutuları zaten fizik sorgularına girmez; güvenli alan kutuları kaçış hedefi olarak alınır
        var safe = SafeAreaBounds();
        List<Vector3> hazards = HazardPointPositions();
        var builder = new GnnGraphBuilder(settings, ComputeSiteArea(settings, hazards, safe, players), HazardLines(), safe, ignoreRoots);
        builder.SeedPoints.AddRange(players);
        builder.SeedPoints.AddRange(hazards);
        return builder;
    }

    /// <summary>
    /// Tehlike çizgileri: her KnnDangerZoneSystem bölgesinin noktaları Inspector'daki sırayla birleştirilir
    /// (nehir kenarı boyunca kesintisiz şerit). Hazard Points doluysa her nokta tek başına alınır.
    /// </summary>
    public List<List<Vector3>> HazardLines()
    {
        var lines = new List<List<Vector3>>();
        if (hazardPoints != null)
        {
            foreach (Transform t in hazardPoints)
                if (t != null) lines.Add(new List<Vector3> { t.position });
        }
        if (lines.Count > 0) return lines;

        foreach (KnnDangerZoneSystem knn in FindObjectsOfType<KnnDangerZoneSystem>(true))
        {
            if (knn.zones == null) continue;
            foreach (KnnDangerZoneSystem.DangerZone zone in knn.zones)
            {
                if (zone?.points == null || !zone.active) continue;
                var line = new List<Vector3>();
                foreach (Transform t in zone.points)
                {
                    // Aynı nokta listede iki kez olabilir (sahnede Nehir Kenarı bölgesinde var)
                    if (t != null && (line.Count == 0 || line[line.Count - 1] != t.position)) line.Add(t.position);
                }
                if (line.Count > 0) lines.Add(line);
            }
        }
        return lines;
    }

    public List<Vector3> HazardPointPositions()
    {
        var result = new List<Vector3>();
        foreach (List<Vector3> line in HazardLines()) result.AddRange(line);
        return result;
    }

    /// <summary>Ev alanı: atanmış kutu ya da kat / güvenli alan / bodrum tetik kutularının birleşimi.</summary>
    public Bounds BuildingBounds()
    {
        if (buildingArea != null) return WorldBounds(buildingArea);

        bool any = false;
        var b = new Bounds();
        void Add(BoxCollider box)
        {
            Bounds w = WorldBounds(box);
            if (!any) { b = w; any = true; } else b.Encapsulate(w);
        }
        foreach (FirstFloorController f in FindObjectsOfType<FirstFloorController>(true))
            foreach (BoxCollider box in f.GetComponents<BoxCollider>()) Add(box);
        foreach (SafeAreaController s in FindObjectsOfType<SafeAreaController>(true))
            foreach (BoxCollider box in s.GetComponents<BoxCollider>()) Add(box);
        foreach (BaseMentFloorCheck m in FindObjectsOfType<BaseMentFloorCheck>(true))
            foreach (BoxCollider box in m.GetComponents<BoxCollider>()) Add(box);
        return any ? b : default;
    }

    private static List<Bounds> SafeAreaBounds()
    {
        var result = new List<Bounds>();
        foreach (SafeAreaController s in FindObjectsOfType<SafeAreaController>(true))
            foreach (BoxCollider box in s.GetComponents<BoxCollider>())
                result.Add(WorldBounds(box));
        return result;
    }

    private static Bounds WorldBounds(BoxCollider box)
    {
        var b = new Bounds(box.transform.TransformPoint(box.center), Vector3.zero);
        Vector3 h = box.size * 0.5f;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            b.Encapsulate(box.transform.TransformPoint(box.center + corner));
        }
        return b;
    }

    /// <summary>Tehlike noktaları + güvenli alan + oyuncu; yatayda Site Margin, dikeyde -3 / +3 m genişletilir.</summary>
    private Bounds ComputeSiteArea(GnnGraphSettings settings, List<Vector3> hazards, List<Bounds> safe, List<Vector3> players)
    {
        if (siteArea != null) return siteArea.bounds;

        bool any = false;
        var b = new Bounds();
        void Add(Vector3 p) { if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p); }
        foreach (Vector3 p in hazards) Add(p);
        foreach (Vector3 p in players) Add(p);
        foreach (Bounds s in safe) { Add(s.min); Add(s.max); }
        if (!any) return default;

        b.Expand(new Vector3(2f * settings.siteMargin, 6f, 2f * settings.siteMargin));
        return b;
    }

    [ContextMenu("GNN: Grafı kur ve etiketleri göster (Editor)")]
    private void PreviewLabels()
    {
        GnnGraphBuilder builder = CreateBuilder(graphSettings);
        graph = builder.BuildImmediate();
        previewLabels = GnnGraphBuilder.ComputeLabels(graph, graphSettings, HazardLines(), BuildingBounds());
        probabilities = null;
        Debug.Log($"[GNN] Önizleme: {graph.NodeCount} düğüm, {graph.EdgeCount / 2} kenar, {builder.HazardPointCount} tehlike noktası, " +
                  $"{builder.DroppedNodes} ulaşılamayan düğüm atıldı.");
    }

    [ContextMenu("GNN: Kayıtlı grafla modeli çalıştır (Editor)")]
    private void PreviewPrediction()
    {
        previewLabels = null;
        LoadModel();
        LoadGraph();
        PredictNow();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || graph == null) return;
        if (probabilities != null && probabilities.Length != graph.NodeCount * 3) probabilities = null;
        if (previewLabels != null && previewLabels.Length != graph.NodeCount) previewLabels = null;

        float size = graph.CellSize * 0.6f;
        Vector3 cube = new Vector3(size, size * 0.15f, size);
        for (int i = 0; i < graph.NodeCount; i++)
        {
            Color color;
            if (probabilities != null && gizmoShowsDecision)
            {
                GnnRiskClass c = ArgMax(i);
                color = c == GnnRiskClass.Safe ? Color.green : c == GnnRiskClass.Danger ? Color.red : Color.yellow;
            }
            else if (probabilities != null)
            {
                float safe = probabilities[i * 3], danger = probabilities[i * 3 + 2];
                color = new Color(danger, safe, 0.15f, 0.85f) + new Color(0.5f, 0.5f, 0f, 0f) * probabilities[i * 3 + 1];
            }
            else if (previewLabels != null)
            {
                color = previewLabels[i] == 0 ? Color.green : previewLabels[i] == 2 ? Color.red : Color.yellow;
            }
            else continue;

            Gizmos.color = color;
            Gizmos.DrawCube(graph.Positions[i] + Vector3.up * size * 0.1f, cube);
        }
    }
}
