# Aegis Protocol

> **3D Sci-Fi Tactical Mech & Base Defense** — Reaktörünü savun, savunma hattını kur ve tahliye sinyalini etkinleştir.

[![Unity](https://img.shields.io/badge/Unity-6000.0.2f1-black?logo=unity)](ProjectSettings/ProjectVersion.txt)
[![Platform](https://img.shields.io/badge/Platform-PC%20%7C%20WebGL-2ea44f)](#platform-ve-kurulum)
[![Project](https://img.shields.io/badge/Status-Core%20Architecture%20Stage-orange)](#geli%C5%9Ftirme-yol-haritas%C4%B1)

**Aegis Protocol**, uzak bir maden gezegenindeki kuantum reaktörünü korumakla görevli bir savaş mech’ini yönettiğin, dalga tabanlı tek oyunculu bir aksiyon ve üs savunma oyunudur. Oyuncu hareket yönünden bağımsız nişan alır, düşmanlardan enerji kristali toplar, harita düğümlerine savunmalar kurar ve uydu terminallerini etkinleştirerek tahliye sinyalini hazırlar.

Bu depo, YAZ20405 Oyun Programlama dersi kapsamında geliştirilen Unity 6 projesinin kodunu, proje iskeletini ve tasarım dokümanını içerir. Proje aşamalı geliştirilmektedir; aşağıdaki oyun özellikleri tasarım hedeflerini belirtir ve henüz tamamlanmış oynanış olarak değerlendirilmemelidir.

## Oyun Özeti

- **Tür:** 3B üstten görünümlü bilim kurgu aksiyonu, kule savunması ve hafif roguelite ilerleme
- **Oturum hedefi:** Yaklaşık 12–15 dakika
- **Ana hedef:** Beş düşman dalgası boyunca mech’i ve reaktörü hayatta tutmak, üç uydu terminalini tamamlamak ve final boss’u yenmek
- **Platform hedefi:** PC geliştirme sürümü ve WebGL (HTML5/WebAssembly) tarayıcı derlemesi
- **Motor:** Unity 6 `6000.0.2f1`

## Temel ve Yan Mekanikler

| Mekanik | Tasarım hedefi |
| --- | --- |
| 3B bağımsız gövde/kule savaşı | WASD ile mech gövdesini hareket ettir; fare ile kuleyi ayrı yönde hedefle. |
| Isı yönetimli çift silah | Plazma topu ve mikro-füze arasında geçiş yap; aşırı ısınmadan önce ateş ritmini yönet. |
| Taktiksel grid kule inşası | Enerji kristallerini BuildNode yuvalarında taret, EMP kulesi veya onarım istasyonu için harca. |
| Uydu terminali görevleri | Tehdit altındaki alanlarda kalarak üç terminalin ilerlemesini tamamla. |
| Gece / güç kesintisi fazı | Görüş azalırken siluet, ışık ve ses ipuçlarıyla tehditleri takip et. |
| Roguelite modül seçimi | Dalgalar arasında sunulan yükseltmelerden birini seçerek mech veya savunma hattını özelleştir. |
| Dalga ve boss karşılaşması | Düşman kompozisyonu ve baskı artar; son dalgada Overlord Titan ile karşılaşılır. |

## Yazılım Mimarisi

Çekirdek kod `Assets/_Project/Scripts` altında sorumluluklarına göre ayrılır. Sahne nesneleri arası bağımlılıkları sınırlamak, oyun durumunu tek merkezden yönetmek ve sonraki sistemlerin aynı sözleşmeleri kullanmasını sağlamak hedeflenir.

```text
Assets/_Project/Scripts/
├── Core/       Oyun durum makinesi, yaşam ve etkileşim sözleşmeleri, bootstrap
├── Combat/     Silahlar, mermiler, hasar ve çarpışma akışı
├── AI/         Düşman kararları ve NavMesh tabanlı hareket
├── Systems/    Dalga, inşa, terminal, skor ve yükseltme sistemleri
├── UI/         HUD, menüler ve durum geri bildirimi
└── Audio/      Müzik, efekt ve ses ayarları
```

### Mimari ilkeler

- **C# katmanları:** Core oyun durumunu ve ortak sözleşmeleri yönetir; Combat, AI ve Systems alan sistemlerini; UI ve Audio ise sunum/geri bildirimi üstlenir.
- **Ortak hasar sözleşmesi:** `IDamageable`, mech, merkez reaktör ve düşman sağlığının aynı API üzerinden kullanılmasını sağlar. `HealthComponent` sağlık değişimi, hasar ve ölüm olaylarını yayınlar.
- **Etkileşim sözleşmesi:** `IInteractable`, terminal ve BuildNode etkileşimleri için ortak bir giriş noktası sunar.
- **Olay tabanlı akış:** Sağlık ve oyun durumu değişiklikleri C# event’leriyle bildirilir; UI bu olaylara abone olabilir.
- **ScriptableObject yaklaşımı:** Düşman, dalga, yapı ve modül ayarları içerik verisi olarak tanımlanabilir; çalışma anındaki durum paylaşılan asset’lere yazılmaz.
- **Nesne yaşam döngüsü:** Prototipte dinamik düşman, mermi ve enerji kristali üretimi `Instantiate`, sahneden kaldırılması `Destroy` ile gösterilir. Nesne havuzlama, performans profili ihtiyaç gösterirse eklenir.
- **Yol bulma:** Düşman hareketi için Unity AI Navigation / NavMesh hedeflenir. Unity 6.0 için paket dokümantasyonu: [AI Navigation 2.0.9](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.ai.navigation.html).

## Kontroller

| Girdi | Eylem |
| --- | --- |
| `W` `A` `S` `D` | Mech’i hareket ettir |
| Fare | Silah kulesini hedefle |
| Sol tık | Plazma topunu ateşle |
| Sağ tık | Mikro-füze ateşle |
| `Space` | Dash / kaçınma |
| `E` | Yakındaki terminal veya BuildNode ile etkileşim |
| `1` `2` `3` | İnşa arayüzündeyken yapı seç |
| `Enter` / sol tık | Seçili yapıyı onayla |
| `Esc` / `P` | Durdurma menüsünü aç veya kapat |

Kontroller tasarım dokümanındaki hedef şemadır; Unity sahnesine bağlandıkça doğrulanıp güncellenecektir.

## Geliştirme Yol Haritası

1. **Repo ve çekirdek mimari:** Git düzeni, Unity proje iskeleti, sağlık/etkileşim arayüzleri, oyun durumu ve başlangıç bileşeni.
2. **Oyuncu prototipi:** Kamera, mech hareketi, bağımsız nişan, ateş, ısı ve dash.
3. **Savaş döngüsü:** Düşman üretimi, NavMesh takibi, hasar, kristal düşüşü ve reaktör savunması.
4. **Taktik sistemler:** BuildNode yerleşimi, kuleler, terminal görevleri, dalga akışı ve modül seçimi.
5. **Sunum ve dağıtım:** HUD/menüler, ses, dengeleme, WebGL performansı ve tarayıcı doğrulaması.

Her aşama önce oynanabilir temel akışı sağlamlaştırır, sonra kapsamı genişletir. Dikey dilim ve performans ölçümleri tamamlandıkça bu yol haritası güncellenir.

## Platform ve Kurulum

1. Unity Hub üzerinden **Unity 6 `6000.0.2f1`** kurulumunu seç. WebGL Build Support modülünü de ekle.
2. Unity Hub’da `Add` / `Open` seçeneğiyle bu depo klasörünü aç.
3. Package Manager `Packages/manifest.json` içindeki bağımlılıkları çözer. İlk açılışta Unity’nin `Library` klasörünü oluşturması beklenir.
4. `Assets/_Project/Scenes` altında açılacak oyun sahnesi geliştirme aşamasında eklenecektir. `SceneBootstrapper` bileşenini başlangıç sahnesine koy.
5. WebGL için `File > Build Profiles` üzerinden Web platformunu seç; gerçek tarayıcı performansını hedef cihazda ölç.

> Bu aşamada depo çekirdek mimari iskeletini kurar; henüz oynanabilir sahne veya tamamlanmış WebGL build’i içermez.

## Depo İçeriği

- `Assets/_Project/` — Unity oyun içeriği, scriptler ve sahne/asset klasörleri
- `Packages/manifest.json` — Unity Package Manager bağımlılıkları
- `ProjectSettings/` — Unity sürümü ve proje ayarları
- `Azo_Ismail_5230505081_KS1_GDD_Odevi_final.docx` — Ders için hazırlanan GDD

## Lisans ve Asset Notu

Projenin kendi kaynak kodu için henüz bir açık kaynak lisansı belirtilmemiştir. Üçüncü taraf model, ses ve görseller eklendiğinde her dosyanın lisansı ve gerekli atıf bilgisi ayrıca kaydedilecektir.
