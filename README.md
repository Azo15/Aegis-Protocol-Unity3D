# Aegis Protocol

> **3D Sci-Fi Tactical Mech & Base Defense** — Reaktörünü savun, savunma hattını kur ve tahliye sinyalini etkinleştir.

[![Unity](https://img.shields.io/badge/Unity-6000.0.2f1-black?logo=unity)](ProjectSettings/ProjectVersion.txt)
[![Platform](https://img.shields.io/badge/Platform-PC%20%7C%20WebGL-2ea44f)](#platform-ve-kurulum)
[![Project](https://img.shields.io/badge/Status-Core%20%2B%20Movement%20Prototype-orange)](#geli%C5%9Ftirme-yol-haritas%C4%B1)

**Aegis Protocol**, uzak bir maden gezegenindeki kuantum reaktörünü korumakla görevli bir savaş mech’ini yönettiğin, dalga tabanlı tek oyunculu bir aksiyon ve üs savunma oyunudur. Oyuncu hareket yönünden bağımsız nişan alır, düşmanlardan enerji kristali toplar, harita düğümlerine savunmalar kurar ve uydu terminallerini etkinleştirerek tahliye sinyalini hazırlar.

Bu depo, YAZ20405 Oyun Programlama dersi kapsamında geliştirilen Unity 6 projesinin kodunu, proje iskeletini ve tasarım dokümanını içerir. **Mevcut aşama:** çekirdek mimari ile mech hareketi, nişan, dash, etkileşim tarama ve kamera kodu hazır. Bu kodların sahne/prefab bağlantısı ve oynanış doğrulaması ile silah, dalga, kule ve terminal sistemleri sonraki üretim adımlarıdır.

## Oyun Özeti

- **Tür:** 3B üstten görünümlü bilim kurgu aksiyonu, kule savunması ve hafif roguelite ilerleme
- **Oturum hedefi:** Yaklaşık 12–15 dakika
- **Ana hedef:** Beş düşman dalgası boyunca mech’i ve reaktörü hayatta tutmak, üç uydu terminalini tamamlamak ve final boss’u yenmek
- **Platform hedefi:** PC geliştirme sürümü ve WebGL (HTML5/WebAssembly) tarayıcı derlemesi
- **Motor:** Unity 6 `6000.0.2f1`

## Temel ve Yan Mekanikler

| Mekanik | Tasarım hedefi | Kod durumu |
| --- | --- | --- |
| 3B bağımsız gövde/kule savaşı | WASD ile mech gövdesini hareket ettir; fare ile kuleyi ayrı yönde hedefle. | `MechController` ve `PlayerInputReader` hazır; prefab/sahne bağlantısı bekliyor. |
| Jet-itici dash | Space ile hareket yönüne, hareket yoksa kule yönüne kısa dash at. | Cooldown ve UI event altyapısı hazır. |
| Isı yönetimli çift silah | Plazma topu ve mikro-füze arasında geçiş yap; aşırı ısınmadan önce ateş ritmini yönet. | Sonraki savaş sistemi aşaması. |
| Taktiksel grid kule inşası | Enerji kristallerini BuildNode yuvalarında taret, EMP kulesi veya onarım istasyonu için harca. | E etkileşim algılama hazır; inşa/ekonomi sistemleri planlı. |
| Uydu terminali görevleri | Tehdit altındaki alanlarda kalarak üç terminalin ilerlemesini tamamla. | Etkileşim sözleşmesi ve tarayıcı hazır; terminal oynanışı planlı. |
| Gece / güç kesintisi fazı | Görüş azalırken siluet, ışık ve ses ipuçlarıyla tehditleri takip et. | Taktik fener kontrol bağlantısı hazır; faz yönetimi planlı. |
| Roguelite modül seçimi | Dalgalar arasında sunulan yükseltmelerden birini seçerek mech veya savunma hattını özelleştir. | Planlı. |
| Dalga ve boss karşılaşması | Düşman kompozisyonu ve baskı artar; son dalgada Overlord Titan ile karşılaşılır. | Planlı. |

## Yazılım Mimarisi

Çekirdek kod `Assets/_Project/Scripts` altında sorumluluklarına göre ayrılır. Sahne nesneleri arası bağımlılıkları sınırlamak, oyun durumunu tek merkezden yönetmek ve sonraki sistemlerin aynı sözleşmeleri kullanmasını sağlamak hedeflenir.

```text
Assets/_Project/Scripts/
├── Core/       Oyun durumu, health, input okuyucu, hareket ayarları, bootstrap, kamera rig’i
├── Combat/     MechController; sonraki aşamada silah, mermi, hasar ve çarpışma sistemleri
├── AI/         Düşman kararları ve NavMesh tabanlı hareket
├── Systems/    MechInteractionScanner; sonraki aşamada dalga, inşa, terminal ve yükseltmeler
├── UI/         HUD, menüler ve durum geri bildirimi
└── Audio/      Müzik, efekt ve ses ayarları
```

### Mimari ilkeler

- **C# katmanları:** Core oyun durumunu ve ortak sözleşmeleri yönetir; Combat, AI ve Systems alan sistemlerini; UI ve Audio ise sunum/geri bildirimi üstlenir.
- **Ortak hasar sözleşmesi:** `IDamageable`, mech, merkez reaktör ve düşman sağlığının aynı API üzerinden kullanılmasını sağlar. `HealthComponent` sağlık değişimi, hasar ve ölüm olaylarını yayınlar.
- **Etkileşim sözleşmesi:** `IInteractable`, terminal ve BuildNode etkileşimleri için ortak bir giriş noktası sunar.
- **Olay tabanlı akış:** Sağlık ve oyun durumu değişiklikleri C# event’leriyle bildirilir. Dash başlangıcı/cooldown ve etkileşim hedefi değişimleri de UI/Audio sistemlerinin abone olabileceği event’ler yayınlar.
- **Test edilebilir input:** `PlayerInputReader`, klasik Unity Input Manager’ı veya `IPlayerInputSource`/`PlayerInputSnapshot` enjeksiyonunu kullanabilir.
- **Hareket ayarları:** `MechMovementConfig` hız, ivme, dönüş, dash, arena sınırı ve nişan menzilini ScriptableObject üzerinden sunar.
- **Kamera:** `IsometricCameraRig` 50° eğimli takip, aim lead, sarsıntı ve taktik fener geçişini sağlar. `MechInteractionScanner`, 3.5 birim çevrede uygun en yakın hedefi bulur.
- **Silah ve ısı:** `WeaponSystem`, sürekli ateşlenen plazma topunu ve bekleme süreli güdümlü mikro-füzeyi yönetir; HUD/Ses event’leri ısı, aşırı ısınma ve cooldown durumlarını bildirir. Parametreler `WeaponConfig` ScriptableObject’inde; atanmadığında güvenli varsayılanlar üretilir.
- **Mermi yaşam döngüsü:** `ProjectileController` Instantiate edilen plazma/mikro-füze nesnelerini hareket ettirir, hedef/alan hasarını dost grupları koruyarak uygular ve çarpma/ömür sonunda Destroy ile temizler. `TimedEffectDestroyer` geçici görsel efektleri toplar.
- **Düşman AI:** `EnemyArchetypeConfig` beş arketipin istatistiklerini ve görsel ipuçlarını taşır. `EnemyAIController`, reaktör/mech hedef seçimini, NavMeshAgent varsa NavMesh hareketini, yoksa XZ steering fallback’ini, arketip saldırılarını, EMP yavaşlatmasını, skor ve kristal ödüllerini yönetir.
- **Dalga ve kaynak akışı:** `WaveSpawnerManager` beş kuşatma dalgasını, spawn koridorlarını, hasarsız dalga bonusunu, beşinci dalga reaktör HP bonusunu ve üçüncü dalganın karanlık/taktik fener geçişini yönetir. `CrystalResourceBank` kristal bakiyesini; `EnergyCrystalPickup` manyetik toplama davranışını sağlar.
- **Taktik inşa:** `BuildNode`/`TacticalBuildManager`, E etkileşimi ve 1/2/3 seçimleriyle kristal maliyetli lazer taret, EMP kulesi ve onarım istasyonu kurar. Yapı prefabı yoksa `DefenseTowerController` ilkel 3B görsel üretir.
- **Uydu görevleri:** `SatelliteTerminalObjective` üç benzersiz terminal kimliğiyle 30 saniyelik sinyal alanı hack’ini yürütür; alandan çıkmak ilerlemeyi duraklatır, tamamlanınca 500 puan ve görev kaydı verir.
- **Roguelite yükseltmeleri:** `RogueliteUpgradeManager`, her dalga sonu altı modülden üç farklı kart sunar; seçim ısı, zırh, taret, manyetik çekim, silah hasarı veya itici hızını geliştirip sıradaki dalgayı başlatır.
- **ScriptableObject yaklaşımı:** Düşman, dalga, yapı ve modül ayarları içerik verisi olarak tanımlanabilir; çalışma anındaki durum paylaşılan asset’lere yazılmaz.
- **Nesne yaşam döngüsü:** Prototipte dinamik düşman, mermi ve enerji kristali üretimi `Instantiate`, sahneden kaldırılması `Destroy` ile gösterilir. Nesne havuzlama, performans profili ihtiyaç gösterirse eklenir.
- **Yol bulma:** Düşman hareketi için Unity AI Navigation / NavMesh hedeflenir. Unity 6.0 için paket dokümantasyonu: [AI Navigation 2.0.9](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.ai.navigation.html).

## Kontroller

| Girdi | Eylem |
| --- | --- |
| `W` `A` `S` `D` | Mech’i hareket ettir |
| Fare | Silah kulesini hedefle |
| Sol tık (basılı) | Seri plazma topu |
| Sağ tık | Mikro-füze ateşle |
| `Space` | Dash / kaçınma |
| `E` | Yakındaki terminal veya BuildNode ile etkileşim |
| `1` `2` `3` | Lazer taret / EMP kulesi / Onarım istasyonu seç |
| `Esc` / `P` | Pause girdi sinyali; menü bağlantısı sonraki UI aşamasında |
| `F` | Taktik feneri aç/kapat |

`PlayerInputReader` test edilebilir input snapshot’ları yayınlar; sol/sağ tık silahlara, 1/2/3 yapı seçimine ve E etkileşim tarayıcısına bağlıdır. Pause menüsü ile yükseltme kartlarının görsel UI sunumu sonraki sunum aşamasında bağlanacaktır.

## Geliştirme Yol Haritası

1. **Repo ve çekirdek mimari — tamamlandı:** Git düzeni, Unity proje iskeleti, sağlık/etkileşim arayüzleri, oyun durumu ve bootstrap.
2. **Mech kontrol kodu — çekirdek hazır:** Input soyutlaması, hareket, bağımsız nişan, dash, etkileşim tarayıcısı ve kamera rig’i. Prefab/sahne bağlantısı ve oynanış ayarı sıradaki iş.
3. **Savaş döngüsü — kod hazır:** Çift modlu silah, aşırı ısınma/kilit, mermi güdümü/alan hasarı, düşman AI, kristal ekonomisi, beş dalga ve Instantiate/Destroy yaşam döngüsü. Sahne/prefab bağlantısı ve oynanış ayarı sıradaki işler.
4. **Taktik sistemler — kod hazır:** 12 BuildNode yuvası üzerinden inşa, üç terminal görevi ve dalga arası rastgele modül seçimi. UI/prefab bağlantıları ile oynanış dengelemesi sıradaki işler.
5. **Sunum ve dağıtım:** HUD/menüler, ses, dengeleme, WebGL performansı ve tarayıcı doğrulaması.

Her aşama önce oynanabilir temel akışı sağlamlaştırır, sonra kapsamı genişletir. Dikey dilim ve performans ölçümleri tamamlandıkça bu yol haritası güncellenir.

## Platform ve Kurulum

1. Unity Hub üzerinden **Unity 6 `6000.0.2f1`** kurulumunu seç. WebGL Build Support modülünü de ekle.
2. Unity Hub’da `Add` / `Open` seçeneğiyle bu depo klasörünü aç.
3. Package Manager `Packages/manifest.json` içindeki bağımlılıkları çözer. İlk açılışta Unity’nin `Library` klasörünü oluşturması beklenir.
4. `Assets/_Project/Scenes` altında açılacak oyun sahnesi geliştirme aşamasında eklenecektir. `SceneBootstrapper` bileşenini başlangıç sahnesine koy.
5. WebGL için `File > Build Profiles` üzerinden Web platformunu seç; gerçek tarayıcı performansını hedef cihazda ölç.

> Depoda henüz başlangıç sahnesi, prefab bağlantısı veya tamamlanmış oynanabilir/WebGL build bulunmuyor. `SceneBootstrapper` bileşenini sahneye ekleyip Mech, Rigidbody, HealthComponent, PlayerInputReader, kamera, movement/weapon config ve namlu referanslarını bağla. `WaveSpawnerManager` üzerinde düşman prefabı + archetype eşleşmelerini, en az iki (Kuzey/Doğu) spawn noktası, Directional Light ve taktik Spotlight referanslarını ata. Haritaya 12 ayrı `BuildNode`, üç `SatelliteTerminalObjective` (`Terminal_North`, `Terminal_West`, `Terminal_South`) ve bir `CrystalResourceBank` yerleştir. `TacticalBuildManager` için üç yapı seçeneğini ve maliyet/config verilerini; varsa taret prefablarını; `RogueliteUpgradeManager` için de kart asset’lerini bağla. Bu asset’ler atanmadığında yapı ve kart sistemleri güvenli varsayılanlar kullanır. Düşman prefablarında `HealthComponent`, `EnemyAIController`, Collider ve isteğe bağlı `NavMeshAgent`; kristal prefabında `EnergyCrystalPickup` bulunmalı. Dalga bileşimleri ve yükseltme kartları atanmamışsa çalışma zamanı varsayılanları kullanılır.

## Depo İçeriği

- `Assets/_Project/` — Unity oyun içeriği, scriptler ve sahne/asset klasörleri
- `Packages/manifest.json` — Unity Package Manager bağımlılıkları
- `ProjectSettings/` — Unity sürümü ve proje ayarları
- `Azo_Ismail_5230505081_KS1_GDD_Odevi_final.docx` — Ders için hazırlanan GDD

## Lisans ve Asset Notu

Projenin kendi kaynak kodu için henüz bir açık kaynak lisansı belirtilmemiştir. Üçüncü taraf model, ses ve görseller eklendiğinde her dosyanın lisansı ve gerekli atıf bilgisi ayrıca kaydedilecektir.

