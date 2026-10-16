using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bölge tabanlı KNN tehlike + konum puanı sistemi.
/// Her bölgenin (Bodrum Kat, Nehir Kenarı, Cam Dibi...) kendi noktaları, K değeri, eşiği ve cezaları vardır.
/// Her bölge için: karakter ile bölgenin noktaları arasındaki Öklid uzaklıkları hesaplanır,
/// en yakın K noktanın ortalaması alınır, eşikle karşılaştırılır.
/// Tüm bölgelerin cezaları toplanır ama toplam ceza maxPenalty'yi (örn. 60) geçemez.
/// </summary>
public class KnnDangerZoneSystem : MonoBehaviour
{
    public enum ThresholdMode
    {
        RawDistance,    // ortalamaMesafe <= distanceThreshold ise tehlike (örn. 4 m)
        NormalizedRisk  // risk = 1 - (ortalamaMesafe / maxDistance), risk >= riskThreshold ise tehlike
    }

    [Serializable]
    public class DangerZone
    {
        [Tooltip("Bölgenin adı (UI ve loglarda görünür).")]
        public string name = "Yeni Bölge";
        public bool active = true;

        [Header("Noktalar")]
        [Tooltip("Bu bölgeyi temsil eden noktaları buraya sürükle.")]
        public List<Transform> points = new List<Transform>();

        [Header("KNN")]
        [Tooltip("Hesaba katılacak en yakın komşu sayısı. Nokta sayısından büyükse tüm noktalar kullanılır.")]
        [Min(1)] public int k = 3;
        [Tooltip("Açıksa yükseklik (Y) farkı mesafeye katılmaz. Bodrum gibi yükseklik farkı önemliyse kapat.")]
        public bool ignoreHeight = true;

        [Header("Eşik")]
        public ThresholdMode thresholdMode = ThresholdMode.RawDistance;
        [Tooltip("RawDistance: Ortalama mesafe bu değerin altındaysa tehlike (metre).")]
        [Min(0.01f)] public float distanceThreshold = 4f;
        [Tooltip("NormalizedRisk: Bu mesafede ve ötesinde risk 0 olur (metre).")]
        [Min(0.01f)] public float maxDistance = 5f;
        [Tooltip("NormalizedRisk: Risk bu değere eşit veya büyükse tehlike.")]
        [Range(0f, 1f)] public float riskThreshold = 0.2f;

        [Header("Puan")]
        [Tooltip("Bu bölgeye her girişte düşülecek puan.")]
        [Min(0f)] public float entryPenalty = 5f;
        [Tooltip("Bu bölgede geçen her saniye düşülecek puan.")]
        [Min(0f)] public float penaltyPerSecond = 10f;
        [Tooltip("Açıksa saniye başı ceza risk ile çarpılır (yaklaştıkça daha çok düşer).")]
        public bool scaleByRisk = true;

        [Header("Debug")]
        public Color gizmoColor = new Color(1f, 0.5f, 0f);

        // Inspector'da yeni eklenen elemanlara varsayılan değerleri atamak için
        [SerializeField, HideInInspector] internal bool initialized;

        // --- Çalışma zamanı değerleri ---
        public float AverageDistance { get; internal set; } = float.PositiveInfinity;
        /// <summary>0 (güvenli) - 1 (noktanın tam üstünde) arası risk.</summary>
        public float Risk { get; internal set; }
        public bool IsInDanger { get; internal set; }
        public float TimeInDanger { get; internal set; }
        public int EntryCount { get; internal set; }
        /// <summary>Bu bölge yüzünden düşülen toplam puan (rapor için).</summary>
        public float PenaltyTaken { get; internal set; }
        public int NeighborCount => foundCount;

        [NonSerialized] internal float[] nearestDist;
        [NonSerialized] internal Transform[] nearestPoints;
        [NonSerialized] internal int foundCount;

        internal void EnsureBuffers()
        {
            if (nearestDist == null || nearestDist.Length != k)
            {
                nearestDist = new float[k];
                nearestPoints = new Transform[k];
                foundCount = 0;
            }
        }

        internal void ApplyDefaults()
        {
            name = "Yeni Bölge";
            active = true;
            k = 3;
            ignoreHeight = true;
            thresholdMode = ThresholdMode.RawDistance;
            distanceThreshold = 4f;
            maxDistance = 5f;
            riskThreshold = 0.2f;
            entryPenalty = 5f;
            penaltyPerSecond = 10f;
            scaleByRisk = true;
            gizmoColor = new Color(1f, 0.5f, 0f);
            initialized = true;
        }

        internal void ResetStats()
        {
            TimeInDanger = 0f;
            EntryCount = 0;
            PenaltyTaken = 0f;
        }
    }

    [Header("Karakter")]
    [Tooltip("Boş bırakılırsa bu scriptin bağlı olduğu obje kullanılır.")]
    public Transform character;

    [Header("Bölgeler")]
    public List<DangerZone> zones = new List<DangerZone>();

    [Header("Puan")]
    [Tooltip("Konum (tehlike bölgeleri) yüzünden düşülebilecek en fazla puan. " +
             "Konum puanı bu değerden başlar ve en fazla 0'a kadar düşer.")]
    [Min(0f)] public float maxPenalty = 60f;

    [Header("Debug")]
    public bool showDebugUI = true;
    public bool drawGizmos = true;
    public bool logZoneChanges = true;

    /// <summary>Tüm bölgelerden düşülen toplam puan (0 - maxPenalty).</summary>
    public float TotalPenalty { get; private set; }
    /// <summary>Konumdan kalan puan (maxPenalty - TotalPenalty). Örn. 60 üzerinden.</summary>
    public float LocationScore => maxPenalty - TotalPenalty;
    /// <summary>Ceza sınırına ulaşıldıysa true (artık puan düşmez).</summary>
    public bool IsPenaltyCapReached => TotalPenalty >= maxPenalty;
    /// <summary>Karakter herhangi bir bölgede tehlikedeyse true.</summary>
    public bool IsInAnyDanger { get; private set; }
    /// <summary>Şu an tehlikede olunan bölgeler arasında riski en yüksek olan (yoksa null).</summary>
    public DangerZone MostDangerousZone { get; private set; }

    /// <summary>Konum puanı her değiştiğinde yeni LocationScore ile tetiklenir.</summary>
    public event Action<float> OnScoreChanged;
    /// <summary>Her ceza uygulandığında (bölge, uygulanan miktar) ile tetiklenir. Kendi ScoreManager'ına bağlamak için.</summary>
    public event Action<DangerZone, float> OnPenaltyApplied;
    /// <summary>Bir bölgeye girildiğinde tetiklenir.</summary>
    public event Action<DangerZone> OnZoneEnter;
    /// <summary>Bir bölgeden çıkıldığında tetiklenir.</summary>
    public event Action<DangerZone> OnZoneExit;

    void Reset()
    {
        // Script ilk eklendiğinde örnek bölgeler
        zones = new List<DangerZone>();

        var basement = new DangerZone();
        basement.ApplyDefaults();
        basement.name = "Bodrum Kat";
        basement.distanceThreshold = 4f;
        basement.gizmoColor = new Color(0.6f, 0.3f, 1f);
        zones.Add(basement);

        var river = new DangerZone();
        river.ApplyDefaults();
        river.name = "Nehir Kenarı";
        river.distanceThreshold = 6f;
        river.gizmoColor = new Color(0.2f, 0.6f, 1f);
        zones.Add(river);
    }

    void OnValidate()
    {
        if (zones == null) return;
        foreach (DangerZone zone in zones)
        {
            if (zone == null) continue;
            if (!zone.initialized) zone.ApplyDefaults();
            zone.EnsureBuffers();
        }
    }

    void Awake()
    {
        if (character == null) character = transform;
        ResetScore();
    }

    void Update()
    {
        if (character == null || zones == null) return;

        Vector3 origin = character.position;
        bool anyDanger = false;
        DangerZone mostDangerous = null;
        float penaltyBefore = TotalPenalty;

        foreach (DangerZone zone in zones)
        {
            if (zone == null) continue;

            bool wasInDanger = zone.IsInDanger;
            Evaluate(zone, origin);

            if (zone.IsInDanger && !wasInDanger)
            {
                zone.EntryCount++;
                ApplyPenalty(zone, zone.entryPenalty);
                if (logZoneChanges) Debug.Log($"[{zone.name}] bölgesine girildi! (Giriş #{zone.EntryCount})");
                OnZoneEnter?.Invoke(zone);
            }
            else if (!zone.IsInDanger && wasInDanger)
            {
                if (logZoneChanges) Debug.Log($"[{zone.name}] bölgesinden çıkıldı.");
                OnZoneExit?.Invoke(zone);
            }

            if (zone.IsInDanger)
            {
                zone.TimeInDanger += Time.deltaTime;
                float multiplier = zone.scaleByRisk ? zone.Risk : 1f;
                ApplyPenalty(zone, zone.penaltyPerSecond * multiplier * Time.deltaTime);

                anyDanger = true;
                if (mostDangerous == null || zone.Risk > mostDangerous.Risk) mostDangerous = zone;
            }
        }

        IsInAnyDanger = anyDanger;
        MostDangerousZone = mostDangerous;

        // Tehlikede değilken ceza uygulanmaz, puan değişmez
        if (TotalPenalty > penaltyBefore)
        {
            OnScoreChanged?.Invoke(LocationScore);
            if (penaltyBefore < maxPenalty && IsPenaltyCapReached && logZoneChanges)
                Debug.Log($"Konum ceza sınırına ulaşıldı ({maxPenalty}). Artık konumdan puan düşmeyecek.");
        }
    }

    // Cezayı maxPenalty sınırını aşmayacak şekilde uygular
    void ApplyPenalty(DangerZone zone, float amount)
    {
        float applied = Mathf.Min(amount, maxPenalty - TotalPenalty);
        if (applied <= 0f) return;

        TotalPenalty += applied;
        zone.PenaltyTaken += applied;
        OnPenaltyApplied?.Invoke(zone, applied);
    }

    void Evaluate(DangerZone zone, Vector3 origin)
    {
        zone.EnsureBuffers();
        zone.foundCount = 0;

        if (zone.active && zone.points != null)
        {
            for (int i = 0; i < zone.points.Count; i++)
            {
                Transform point = zone.points[i];
                if (point == null) continue;
                Insert(zone, EuclideanDistance(origin, point.position, zone.ignoreHeight), point);
            }
        }

        if (zone.foundCount == 0)
        {
            zone.AverageDistance = float.PositiveInfinity;
            zone.Risk = 0f;
            zone.IsInDanger = false;
            return;
        }

        float sum = 0f;
        for (int i = 0; i < zone.foundCount; i++) sum += zone.nearestDist[i];
        zone.AverageDistance = sum / zone.foundCount;

        // RawDistance modunda risk eşik mesafesine göre normalize edilir,
        // böylece eşiğin içindeyken risk her zaman 0'dan büyük olur.
        float range = zone.thresholdMode == ThresholdMode.NormalizedRisk ? zone.maxDistance : zone.distanceThreshold;
        zone.Risk = 1f - Mathf.Clamp01(zone.AverageDistance / range);

        zone.IsInDanger = zone.thresholdMode == ThresholdMode.NormalizedRisk
            ? zone.Risk >= zone.riskThreshold
            : zone.AverageDistance <= zone.distanceThreshold;
    }

    static float EuclideanDistance(Vector3 a, Vector3 b, bool ignoreHeight)
    {
        float dx = a.x - b.x;
        float dy = ignoreHeight ? 0f : a.y - b.y;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    // Mesafeye göre sıralı tutulan K elemanlı listeye ekleme (insertion sort)
    static void Insert(DangerZone zone, float d, Transform point)
    {
        int k = zone.k;
        int i;
        if (zone.foundCount < k) { i = zone.foundCount; zone.foundCount++; }
        else if (d < zone.nearestDist[k - 1]) i = k - 1;
        else return;

        while (i > 0 && zone.nearestDist[i - 1] > d)
        {
            zone.nearestDist[i] = zone.nearestDist[i - 1];
            zone.nearestPoints[i] = zone.nearestPoints[i - 1];
            i--;
        }
        zone.nearestDist[i] = d;
        zone.nearestPoints[i] = point;
    }

    // --- Dışarıdan kullanılabilecek fonksiyonlar ---

    public void ResetScore()
    {
        TotalPenalty = 0f;
        if (zones != null)
        {
            foreach (DangerZone zone in zones) zone?.ResetStats();
        }
        OnScoreChanged?.Invoke(LocationScore);
    }

    /// <summary>İsme göre bölge bulur (yoksa null).</summary>
    public DangerZone GetZone(string zoneName)
    {
        return zones?.Find(z => z != null && z.name == zoneName);
    }

    /// <summary>Bir bölgeyi çalışma sırasında açar/kapatır (örn. sel başladığında nehir bölgesini aktif et).</summary>
    public void SetZoneActive(string zoneName, bool active)
    {
        DangerZone zone = GetZone(zoneName);
        if (zone != null) zone.active = active;
    }

    void OnGUI()
    {
        if (!showDebugUI || zones == null) return;

        float lineHeight = 20f;
        float height = 50f + zones.Count * lineHeight;
        GUI.Box(new Rect(10, 10, 420, height), "Tehlike Bölgeleri");
        string cap = IsPenaltyCapReached ? "  (SINIRA ULAŞILDI)" : "";
        GUI.Label(new Rect(20, 32, 400, lineHeight),
            $"Konum puanı: {LocationScore:F1} / {maxPenalty:F0}  (düşülen: {TotalPenalty:F1}){cap}");

        for (int i = 0; i < zones.Count; i++)
        {
            DangerZone zone = zones[i];
            if (zone == null) continue;
            string state = !zone.active ? "kapalı" : zone.IsInDanger ? "TEHLİKEDE!" : "güvenli";
            string dist = float.IsInfinity(zone.AverageDistance) ? "-" : zone.AverageDistance.ToString("F1");
            GUI.Label(new Rect(20, 52 + i * lineHeight, 400, lineHeight),
                $"{zone.name}: {state} | mesafe {dist} m | risk {zone.Risk:F2} | giriş {zone.EntryCount} | -{zone.PenaltyTaken:F1}");
        }
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || zones == null) return;
        Transform c = character != null ? character : transform;

        foreach (DangerZone zone in zones)
        {
            if (zone == null || zone.points == null) continue;

            Color color = zone.gizmoColor;
            if (!zone.active) color.a = 0.25f;
            Gizmos.color = color;

            Transform firstPoint = null;
            foreach (Transform point in zone.points)
            {
                if (point == null) continue;
                if (firstPoint == null) firstPoint = point;
                Gizmos.DrawSphere(point.position, 0.15f);
            }

#if UNITY_EDITOR
            if (firstPoint != null)
                UnityEditor.Handles.Label(firstPoint.position + Vector3.up * 0.5f, zone.name);
#endif

            if (Application.isPlaying && zone.IsInDanger && zone.nearestPoints != null)
            {
                for (int i = 0; i < zone.foundCount; i++)
                {
                    if (zone.nearestPoints[i] != null) Gizmos.DrawLine(c.position, zone.nearestPoints[i].position);
                }
            }
        }

        Gizmos.color = Application.isPlaying && IsInAnyDanger ? Color.red : Color.green;
        Gizmos.DrawWireSphere(c.position, 0.3f);
    }
}
