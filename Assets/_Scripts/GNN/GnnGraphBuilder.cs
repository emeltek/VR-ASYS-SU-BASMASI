using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evin ve çevresinin yürünebilir yüzeylerinden çok katlı GNN grafı kurar (bodrum, zemin kat, üst kat,
/// arazi). Aynı (x, z) hücresinde her kat ayrı düğümdür; katlar merdivenle bağlanır. Eğitim verisi ve
/// runtime grafı aynı kodla Editor'de üretilir; böylece özellikler birebir aynı hesaplanır.
///
/// Su yüzeyleri (RamSpline, Lake Polygon, Water etiketli objeler) zemin ve engel sayılmaz.
/// Etiketler (yalnızca eğitimde) son su seviyesine göre su derinliğinden ve nehir / köprü noktalarından gelir.
/// </summary>
public sealed class GnnGraphBuilder
{
    public static readonly string[] FeatureNames =
    {
        "relative_height",        // 0: son su seviyesine göre yükseklik (su derinliği)
        "water_distance",         // 1: nehir / köprü noktasına yatay uzaklık
        "safe_path_distance",     // 2: güvenli alana graf üzerinden yürüme mesafesi (kaçış yolu)
        "overhead_clearance",     // 3: tavan (1 = açık hava; iç mekân / dış mekân)
        "min_clearance",          // 4: 8 yönde en yakın engel
        "mean_clearance",         // 5: 8 yönde ortalama engel uzaklığı
        "openness",               // 6: açık yön oranı
        "levels_in_column"        // 7: aynı hücredeki kat sayısı (çok katlı yapı içi)
    };

    public static readonly string[] ClassNames = { "Safe", "Caution", "Danger" };
    public static int FeatureCount => FeatureNames.Length;

    private const float WaistHeight = 0.8f;
    private const float KneeHeight = 0.4f;
    private const float HeadHeight = 2.0f;
    private const float MaxLevels = 4f;

    private static readonly Vector3[] Dirs8 =
    {
        new Vector3(1, 0, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1),
        new Vector3(0.7071f, 0, 0.7071f), new Vector3(-0.7071f, 0, 0.7071f),
        new Vector3(0.7071f, 0, -0.7071f), new Vector3(-0.7071f, 0, -0.7071f)
    };

    private static readonly Vector2Int[] ForwardNeighbors =
    {
        new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, -1)
    };

    private readonly GnnGraphSettings settings;
    private readonly HashSet<Collider> ignored = new HashSet<Collider>();
    private readonly List<Vector3> hazardPoints;
    private readonly List<Bounds> safeAreas;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[128];
    private readonly Collider[] colliderBuffer = new Collider[512];
    private readonly List<float> levelBuffer = new List<float>(8);
    private readonly Bounds area;
    private readonly float minWalkableNormalY;

    public GnnGraph Graph { get; } = new GnnGraph();
    public bool HasArea { get; }
    public int HazardPointCount => hazardPoints.Count;
    public int SafeAreaCount => safeAreas.Count;
    public int IgnoredWaterColliders { get; }
    public Bounds Area => area;

    /// <param name="siteArea">Grafın kurulacağı alan (dünya, yükseklik dahil). Boyutu sıfırsa graf kurulmaz.</param>
    /// <param name="hazardPoints">Nehir / köprü / bodrum noktaları (KnnDangerZoneSystem bölgeleri)</param>
    /// <param name="safeAreas">Güvenli alan kutuları (SafeAreaController)</param>
    /// <param name="ignoreRoots">Collider'ları yok sayılacak objeler (oyuncu, tetik kutuları)</param>
    public GnnGraphBuilder(GnnGraphSettings settings, Bounds siteArea, List<Vector3> hazardPoints, List<Bounds> safeAreas,
        IEnumerable<Transform> ignoreRoots)
    {
        this.settings = settings;
        this.hazardPoints = hazardPoints;
        this.safeAreas = safeAreas;
        area = siteArea;
        HasArea = siteArea.size.x > 0f && siteArea.size.z > 0f;
        minWalkableNormalY = Mathf.Cos(settings.maxSlope * Mathf.Deg2Rad);

        foreach (Transform root in ignoreRoots)
        {
            if (root == null) continue;
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                ignored.Add(c);
        }

        // Su yüzeyleri yükseldikçe yer değiştirir; zemin / duvar sayılmaz
        int water = 0;
        foreach (Collider c in Object.FindObjectsOfType<Collider>(true))
        {
            if (IsWater(c.transform, settings.waterKeywords) && ignored.Add(c)) water++;
        }
        IgnoredWaterColliders = water;

        Graph.FeatureCount = FeatureCount;
        Graph.CellSize = settings.cellSize;
        Graph.GridOrigin = new Vector3(area.min.x, area.min.y, area.min.z);
        Graph.GridOffset = settings.gridOffset;
    }

    /// <summary>Grafı birkaç kareye yayarak kurar.</summary>
    public IEnumerator Build(int nodesPerStep)
    {
        if (!HasArea) yield break;
        Physics.SyncTransforms();

        float cs = Graph.CellSize;
        int countX = Mathf.CeilToInt(area.size.x / cs);
        int countZ = Mathf.CeilToInt(area.size.z / cs);
        int budget = 0;

        // 1) Düğümler: her hücrede yürünebilir ve boş her kat
        for (int ix = 0; ix < countX; ix++)
        {
            for (int iz = 0; iz < countZ; iz++)
            {
                var cell = new Vector2Int(ix, iz);
                SampleLevels(CellCenter(cell));
                foreach (float y in levelBuffer)
                {
                    Vector3 c = CellCenter(cell);
                    Graph.AddToCell(cell, Graph.Positions.Count);
                    Graph.Positions.Add(new Vector3(c.x, y, c.z));
                    Graph.Cells.Add(cell);
                }

                if (++budget >= nodesPerStep * 2) { budget = 0; yield return null; }
            }
        }

        // 2) Kenarlar: arada engel olmayan, basamak yüksekliğini aşmayan 8-komşular (katlar arası merdiven)
        int n = Graph.NodeCount;
        var adjacency = new List<int>[n];
        for (int i = 0; i < n; i++) adjacency[i] = new List<int>(8);
        for (int i = 0; i < n; i++)
        {
            Vector2Int cell = Graph.Cells[i];
            foreach (Vector2Int d in ForwardNeighbors)
            {
                if (!Graph.CellToNodes.TryGetValue(cell + d, out List<int> others)) continue;
                foreach (int j in others)
                {
                    if (!IsConnected(Graph.Positions[i], Graph.Positions[j])) continue;
                    adjacency[i].Add(j);
                    adjacency[j].Add(i);
                }
            }
            if (++budget >= nodesPerStep * 2) { budget = 0; yield return null; }
        }

        // 3) Ulaşılamayan adacıkları (çatı, dolap üstü) at: oyuncu / güvenli alan / tehlike noktalarının bileşenleri kalır
        adjacency = KeepReachable(adjacency);
        n = Graph.NodeCount;

        Graph.NeighborOffsets = new int[n + 1];
        for (int i = 0; i < n; i++) Graph.NeighborOffsets[i + 1] = Graph.NeighborOffsets[i] + adjacency[i].Count;
        Graph.NeighborIndices = new int[Graph.NeighborOffsets[n]];
        for (int i = 0; i < n; i++) adjacency[i].CopyTo(Graph.NeighborIndices, Graph.NeighborOffsets[i]);

        // 4) Düğüm özellikleri
        int f = FeatureCount;
        Graph.Features = new float[n * f];
        for (int i = 0; i < n; i++)
        {
            ComputeFeatures(i, Graph.Features, i * f);
            if (++budget >= nodesPerStep) { budget = 0; yield return null; }
        }

        // 5) Güvenli alana yürüme mesafesi (merdivenler dahil)
        ComputeSafePathDistances();
    }

    /// <summary>Bileşen başına en az bu kadar düğüm yoksa (tohum içerse bile) gürültü sayılır.</summary>
    private const int MinComponentSize = 8;

    /// <summary>Tohum noktalarına (oyuncu, güvenli alan, tehlike noktaları) bağlı bileşenleri tutar, grafı yeniden numaralar.</summary>
    private List<int>[] KeepReachable(List<int>[] adjacency)
    {
        int n = Graph.NodeCount;
        var component = new int[n];
        for (int i = 0; i < n; i++) component[i] = -1;
        var sizes = new List<int>();
        var stack = new Stack<int>();
        for (int i = 0; i < n; i++)
        {
            if (component[i] >= 0) continue;
            int id = sizes.Count, size = 0;
            component[i] = id;
            stack.Push(i);
            while (stack.Count > 0)
            {
                int u = stack.Pop();
                size++;
                foreach (int v in adjacency[u])
                {
                    if (component[v] >= 0) continue;
                    component[v] = id;
                    stack.Push(v);
                }
            }
            sizes.Add(size);
        }

        var keep = new HashSet<int>();
        foreach (Vector3 seed in SeedPoints)
        {
            int node = NearestNode(seed, 2.5f);
            if (node >= 0 && sizes[component[node]] >= MinComponentSize) keep.Add(component[node]);
        }
        for (int i = 0; i < n; i++)
            if (InSafeArea(Graph.Positions[i]) && sizes[component[i]] >= MinComponentSize) keep.Add(component[i]);
        DroppedNodes = 0;
        if (keep.Count == 0) return adjacency; // tohum bulunamadı: hiçbir şey atılmaz

        var remap = new int[n];
        var positions = new List<Vector3>();
        var cells = new List<Vector2Int>();
        for (int i = 0; i < n; i++)
        {
            if (!keep.Contains(component[i])) { remap[i] = -1; DroppedNodes++; continue; }
            remap[i] = positions.Count;
            positions.Add(Graph.Positions[i]);
            cells.Add(Graph.Cells[i]);
        }

        var result = new List<int>[positions.Count];
        for (int i = 0; i < n; i++)
        {
            if (remap[i] < 0) continue;
            var list = new List<int>(adjacency[i].Count);
            foreach (int j in adjacency[i]) list.Add(remap[j]);
            result[remap[i]] = list;
        }

        Graph.Positions.Clear();
        Graph.Positions.AddRange(positions);
        Graph.Cells.Clear();
        Graph.Cells.AddRange(cells);
        Graph.CellToNodes.Clear();
        for (int i = 0; i < cells.Count; i++) Graph.AddToCell(cells[i], i);
        return result;
    }

    /// <summary>Oyuncu başlangıcı ve tehlike noktaları (KeepReachable tohumları).</summary>
    public List<Vector3> SeedPoints { get; } = new List<Vector3>();
    public int DroppedNodes { get; private set; }

    /// <summary>Noktanın altındaki en yakın düğüm (yatay + dikey, en fazla maxDistance).</summary>
    private int NearestNode(Vector3 p, float maxDistance)
    {
        int best = -1;
        float bestDist = maxDistance * maxDistance;
        for (int i = 0; i < Graph.NodeCount; i++)
        {
            Vector3 q = Graph.Positions[i];
            float dy = p.y - q.y;
            if (dy < -0.5f) continue; // noktanın üstündeki kat sayılmaz
            float d = (q.x - p.x) * (q.x - p.x) + (q.z - p.z) * (q.z - p.z) + 0.25f * dy * dy;
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    public GnnGraph BuildImmediate()
    {
        IEnumerator routine = Build(int.MaxValue);
        while (routine.MoveNext()) { }
        return Graph;
    }

    private Vector3 CellCenter(Vector2Int cell)
    {
        float cs = Graph.CellSize;
        return new Vector3(
            Graph.GridOrigin.x + (cell.x + 0.5f + settings.gridOffset.x) * cs,
            area.max.y,
            Graph.GridOrigin.z + (cell.y + 0.5f + settings.gridOffset.y) * cs);
    }

    /// <summary>
    /// Hücredeki yürünebilir katlar (yukarıdan aşağı). Aşağı doğru tüm çarpışmalar alınır; birbirine
    /// minLevelSeparation'dan yakın yüzeylerde üstteki kalır (masa / basamak ayrı kat sayılmaz).
    /// Gövde kapsülü bir eşya / duvarla çakışan yüzey düğüm olmaz.
    /// </summary>
    private void SampleLevels(Vector3 top)
    {
        levelBuffer.Clear();
        Vector3 origin = new Vector3(top.x, area.max.y + 1f, top.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hitBuffer, area.size.y + 2f, settings.layerMask, QueryTriggerInteraction.Ignore);

        var heights = new List<float>(count);
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (ignored.Contains(h.collider) || h.normal.y < minWalkableNormalY) continue;
            if (h.point.y < area.min.y || h.point.y > area.max.y) continue;
            heights.Add(h.point.y);
        }
        heights.Sort((a, b) => b.CompareTo(a));

        float lastAccepted = float.PositiveInfinity;
        foreach (float y in heights)
        {
            if (lastAccepted - y < settings.minLevelSeparation) continue;

            float r = settings.bodyRadius;
            Vector3 floor = new Vector3(top.x, y, top.z);
            if (OverlapCapsule(floor + Vector3.up * (r + 0.15f), floor + Vector3.up * 1.6f, r) > 0)
            {
                // Eşya üstü / duvar içi: bu yüzeyin altındaki zemin de ayrıca denenir
                lastAccepted = y;
                continue;
            }
            levelBuffer.Add(y);
            lastAccepted = y;
        }
    }

    private void ComputeFeatures(int node, float[] output, int offset)
    {
        Vector3 fp = Graph.Positions[node];
        Vector3 up = Vector3.up;
        float probe = settings.hazardProbeDistance;

        // 0: Su seviyesine göre yükseklik
        float rel = Mathf.Clamp((fp.y - settings.floodLevel) / settings.heightScale, -1f, 1f);

        // 1: Nehir / köprü noktalarına yatay uzaklık
        float water = probe;
        foreach (Vector3 p in hazardPoints)
            water = Mathf.Min(water, new Vector2(p.x - fp.x, p.z - fp.z).magnitude);

        // 3: Tavan
        float upLength = settings.overheadProbeDistance;
        float overhead = NearestHit(fp + up * HeadHeight, up, upLength, out RaycastHit upHit) ? upHit.distance / upLength : 1f;

        // 4-6: Çevre açıklığı
        Vector3 waist = fp + up * WaistHeight;
        float clearProbe = settings.clearanceProbeDistance;
        float minClear = clearProbe, sumClear = 0f;
        int open = 0;
        foreach (Vector3 dir in Dirs8)
        {
            float d = NearestHit(waist, dir, clearProbe, out RaycastHit h) ? h.distance : clearProbe;
            minClear = Mathf.Min(minClear, d);
            sumClear += d;
            if (d >= clearProbe * 0.5f) open++;
        }

        // 7: Aynı hücredeki kat sayısı
        int levels = Graph.CellToNodes.TryGetValue(Graph.Cells[node], out List<int> list) ? list.Count : 1;

        output[offset + 0] = rel;
        output[offset + 1] = water / probe;
        output[offset + 2] = 1f; // ComputeSafePathDistances doldurur
        output[offset + 3] = overhead;
        output[offset + 4] = minClear / clearProbe;
        output[offset + 5] = sumClear / Dirs8.Length / clearProbe;
        output[offset + 6] = open / (float)Dirs8.Length;
        output[offset + 7] = Mathf.Min(levels, MaxLevels) / MaxLevels;
    }

    /// <summary>Çok kaynaklı Dijkstra: güvenli alan kutusunun içindeki düğümlerden graf kenarları boyunca uzaklık.</summary>
    private void ComputeSafePathDistances()
    {
        int n = Graph.NodeCount;
        if (n == 0) return;

        float probe = settings.hazardProbeDistance;
        var dist = new float[n];
        var queue = new SortedSet<(float d, int node)>();
        for (int i = 0; i < n; i++)
        {
            dist[i] = float.MaxValue;
            if (InSafeArea(Graph.Positions[i])) { dist[i] = 0f; queue.Add((0f, i)); }
        }

        while (queue.Count > 0)
        {
            var (d, i) = queue.Min;
            queue.Remove(queue.Min);
            if (d > probe) break;
            for (int e = Graph.NeighborOffsets[i]; e < Graph.NeighborOffsets[i + 1]; e++)
            {
                int j = Graph.NeighborIndices[e];
                float nd = d + Vector3.Distance(Graph.Positions[i], Graph.Positions[j]);
                if (nd >= dist[j]) continue;
                if (dist[j] < float.MaxValue) queue.Remove((dist[j], j));
                dist[j] = nd;
                queue.Add((nd, j));
            }
        }

        int f = FeatureCount;
        for (int i = 0; i < n; i++)
            Graph.Features[i * f + 2] = Mathf.Min(dist[i], probe) / probe;
    }

    private bool InSafeArea(Vector3 floor)
    {
        Vector3 p = floor + Vector3.up * 0.5f;
        foreach (Bounds b in safeAreas)
        {
            Bounds e = b;
            e.Expand(new Vector3(0f, 1f, 0f));
            if (e.Contains(p)) return true;
        }
        return false;
    }

    private bool IsConnected(Vector3 a, Vector3 b)
    {
        if (Mathf.Abs(a.y - b.y) > settings.maxStep) return false;
        // Basamakta alttaki noktanın dizinden üstteki noktanın dizine
        Vector3 from = a + Vector3.up * KneeHeight;
        Vector3 to = b + Vector3.up * KneeHeight;
        Vector3 delta = to - from;
        return !NearestHit(from, delta.normalized, delta.magnitude, out _);
    }

    private bool NearestHit(Vector3 origin, Vector3 dir, float length, out RaycastHit best)
    {
        int count = Physics.RaycastNonAlloc(origin, dir, hitBuffer, length, settings.layerMask, QueryTriggerInteraction.Ignore);
        best = default;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (ignored.Contains(h.collider)) continue;
            if (!found || h.distance < best.distance) { best = h; found = true; }
        }
        return found;
    }

    private int OverlapCapsule(Vector3 a, Vector3 b, float radius)
    {
        int count = Physics.OverlapCapsuleNonAlloc(a, b, radius, colliderBuffer, settings.layerMask, QueryTriggerInteraction.Ignore);
        int kept = 0;
        for (int i = 0; i < count; i++)
        {
            if (ignored.Contains(colliderBuffer[i])) continue;
            colliderBuffer[kept++] = colliderBuffer[i];
        }
        return kept;
    }

    /// <summary>Obje (veya üst objesi) Water etiketli ya da isminde su anahtar kelimesi geçiyor mu.</summary>
    private static bool IsWater(Transform t, string[] keywords)
    {
        for (; t != null; t = t.parent)
        {
            if (t.CompareTag("Water")) return true;
            string lower = t.name.ToLowerInvariant();
            foreach (string k in keywords)
            {
                if (!string.IsNullOrEmpty(k) && lower.Contains(k.ToLowerInvariant())) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Bir zemin noktasının etiketi. Eğitim etiketleri ve GNN hazır değilken puanlayıcının yedeği aynı kuralı kullanır:
    ///   - son su seviyesinin dangerDepth'ten fazla altı → Tehlikeli (bodrum, nehir kenarı)
    ///   - nehir / köprü noktasına hazardPointRadius'tan yakın → Tehlikeli
    ///   - su seviyesinin en fazla safeHeight üstü → Dikkat (zemin kat)
    ///   - daha yüksek → Güvenli (üst kat)
    /// </summary>
    public static GnnRiskClass LabelAt(Vector3 floor, GnnGraphSettings settings, List<Vector3> hazardPoints)
    {
        float depth = settings.floodLevel - floor.y;
        if (depth > settings.dangerDepth) return GnnRiskClass.Danger;

        float r2 = settings.hazardPointRadius * settings.hazardPointRadius;
        foreach (Vector3 p in hazardPoints)
        {
            if (Mathf.Abs(p.y - floor.y) > 4f) continue;
            if ((p.x - floor.x) * (p.x - floor.x) + (p.z - floor.z) * (p.z - floor.z) <= r2) return GnnRiskClass.Danger;
        }

        return -depth <= settings.safeHeight ? GnnRiskClass.Caution : GnnRiskClass.Safe;
    }

    public static int[] ComputeLabels(GnnGraph graph, GnnGraphSettings settings, List<Vector3> hazardPoints)
    {
        var labels = new int[graph.NodeCount];
        for (int i = 0; i < graph.NodeCount; i++)
            labels[i] = (int)LabelAt(graph.Positions[i], settings, hazardPoints);
        return labels;
    }
}
