# GNN Risk Haritası ve Puanlama — Su Baskını (Sel)

## Risk haritası (GraphSAGE, çok katlı)
Ev ve çevresi 0.5 m hücrelere bölünür. Aynı (x, z) hücresinde her yürünebilir kat (bodrum, zemin kat,
üst kat, arazi) ayrı düğümdür; katlar merdivenle bağlanır. Çatı / dolap üstü gibi ulaşılamayan adacıklar atılır.
Su yüzeyleri (RamSpline, Lake Polygon, `Water` etiketi) zemin ve engel sayılmaz.

Etiketler (yalnızca eğitimde, `GnnGraphBuilder.ComputeLabels`):

| Yer | Kural | Sınıf |
|---|---|---|
| Ev içi (kat / güvenli alan kutularının birleşimi + 1 m, üstü kapalı) | Son su seviyesinin (`Flood Level`, 14.8 m) 1.0 m'den fazla altı | Tehlikeli (bodrum) |
| | Su seviyesinin en fazla 1.5 m üstü | Dikkat (zemin kat, giriş) |
| | Daha yüksek | Güvenli (üst kat) |
| Dışarısı | Nehir yatağı (`River Level` 11.0 m altındaki zemin) ve 3 m çevresi | Tehlikeli (nehir kenarı) |
| | KNN köprü / nehir çizgisine ≤ 3 m | Tehlikeli (köprü) |
| | Kalanı (evin önü, arkası, arazi) | Dikkat |

Özellikler (8): `relative_height`, `water_distance`, `safe_path_distance` (merdivenlerle güvenli alana yürüme),
`overhead_clearance`, `min_clearance`, `mean_clearance`, `openness`, `levels_in_column`.

## Puanlama (100'den düşer)
| Hata | Ceza |
|---|---|
| Şalteri hiç kapatmamak | −30 |
| Şalteri su yükseldikten sonra kapatmak (ıslakken elektrik) | −35 |
| Telefonu almamak | −10 |
| İlk yardım çantasını almamak | −10 |
| Sonda Dikkat alanında (zemin kat) | −30 |
| Sonda Tehlikeli alanda (bodrum / dışarısı) | −55 |
| Tehlikeli alanda kalmak (GNN) | İlk 5 s (toplam) yalnızca uyarı; sonra kademeli `60 × (1 − e^(−E/8))`, en fazla −60 |
| Sel suyuna temas | Bir kez −20 |
| Güvenli alan dışında hareketsiz (her 15 s) | −5, en fazla −15 |

Su toplam 90 s'de en yüksek seviyeye ulaşır (Total Duration 55 s + Ram Spline 35 s). Görev ve konum cezaları ~10 s sonra (100. saniye) uygulanır; şalter ~78. saniyeden sonra "ıslak" sayılır.
Etiket: 75+ Güvenli, 50–74 Orta Riskli, 25–49 Riskli, 25 altı Yüksek Riskli.

| Durum | Puan | Etiket |
|---|---|---|
| Hepsini doğru yaptı | 100 | Güvenli |
| Çantayı unuttu | 90 | Güvenli |
| Şalteri hiç kapatmadı | 70 | Orta Riskli |
| Zemin katta kaldı | 70 | Orta Riskli |
| Bodrumda 15 s | 56 | Orta Riskli |
| Nehir kenarında 30 s + suya girdi | 22 | Yüksek Riskli |
| Hiçbir şey yapmadı, sonda bodrumda | 0 | Yüksek Riskli |

## Unity'de yapılacaklar
1. Sınav sahnesinde (`VR-ASYS-SU-BASMASI-Exam`) boş bir obje `GNN` oluştur; `GnnRiskPredictor` ve `GnnRiskScorer` ekle. Sahneyi kaydet.
2. `GnnRiskPredictor > Graph Settings > Flood Level`: senaryo sonundaki su yüzeyinin dünya Y değeri (zemin kat seviyesi civarı). Gerekirse düzelt.
3. **Tools > GNN > Eğitim Verisini ve Grafı Dışa Aktar** çalıştır; çıktıdaki yükseklik dağılımında bodrum / zemin / üst kat görünmeli.
4. Eğitim:
   ```
   python3 -m venv GNN/.venv && GNN/.venv/bin/pip install -r GNN/requirements.txt && GNN/.venv/bin/python GNN/train_gnn.py
   ```
5. Haritayı kontrol et: `GNN` seçiliyken bileşen menüsünden *Kayıtlı grafla modeli çalıştır*.

Eski `KnnDangerZoneSystem` sahnede kalıyor ama puana etki etmiyor; noktaları GNN'e etiket kaynağı. Ekran içi debug kutusu kapatıldı.
