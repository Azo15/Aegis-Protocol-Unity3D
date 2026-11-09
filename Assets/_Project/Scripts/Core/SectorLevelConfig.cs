using UnityEngine;

namespace AegisProtocol.Core
{
    /// <summary>Immutable descriptor for one of the 5 locked campaign sector levels.</summary>
    public readonly struct SectorLevelData
    {
        public int LevelNumber { get; }
        public string SectorName { get; }
        public string Subtitle { get; }
        public string WeaponEvolutionTitle { get; }
        public string SpecialPerkTitle { get; }
        public int PrimaryProjectileCount { get; }
        public float PrimarySpreadAngle { get; }
        public float PrimaryExplosionRadius { get; }
        public int MissileSalvoCount { get; }
        public float WeaponDamageMultiplier { get; }
        public float CoolingMultiplier { get; }
        public float MoveSpeedMultiplier { get; }
        public float PickupRadiusBonus { get; }
        public float PassiveRegenPerSecond { get; }
        public float TurretFireRateBonus { get; }
        public int StartingCrystals { get; }
        public float EnemyHealthMultiplier { get; }
        public float EnemySpeedMultiplier { get; }
        public int ExtraEnemiesPerGroup { get; }
        public bool PersistentNightAtmosphere { get; }
        public Color FloorColor { get; }
        public Color GridColor { get; }
        public Color ReactorColor { get; }
        public Color ProjectileColor { get; }
        public Color ExplosionColor { get; }

        public SectorLevelData(
            int levelNumber,
            string sectorName,
            string subtitle,
            string weaponEvolutionTitle,
            string specialPerkTitle,
            int primaryProjectileCount,
            float primarySpreadAngle,
            float primaryExplosionRadius,
            int missileSalvoCount,
            float weaponDamageMultiplier,
            float coolingMultiplier,
            float moveSpeedMultiplier,
            float pickupRadiusBonus,
            float passiveRegenPerSecond,
            float turretFireRateBonus,
            int startingCrystals,
            float enemyHealthMultiplier,
            float enemySpeedMultiplier,
            int extraEnemiesPerGroup,
            bool persistentNightAtmosphere,
            Color floorColor,
            Color gridColor,
            Color reactorColor,
            Color projectileColor,
            Color explosionColor)
        {
            LevelNumber = levelNumber;
            SectorName = sectorName;
            Subtitle = subtitle;
            WeaponEvolutionTitle = weaponEvolutionTitle;
            SpecialPerkTitle = specialPerkTitle;
            PrimaryProjectileCount = primaryProjectileCount;
            PrimarySpreadAngle = primarySpreadAngle;
            PrimaryExplosionRadius = primaryExplosionRadius;
            MissileSalvoCount = missileSalvoCount;
            WeaponDamageMultiplier = weaponDamageMultiplier;
            CoolingMultiplier = coolingMultiplier;
            MoveSpeedMultiplier = moveSpeedMultiplier;
            PickupRadiusBonus = pickupRadiusBonus;
            PassiveRegenPerSecond = passiveRegenPerSecond;
            TurretFireRateBonus = turretFireRateBonus;
            StartingCrystals = startingCrystals;
            EnemyHealthMultiplier = enemyHealthMultiplier;
            EnemySpeedMultiplier = enemySpeedMultiplier;
            ExtraEnemiesPerGroup = extraEnemiesPerGroup;
            PersistentNightAtmosphere = persistentNightAtmosphere;
            FloorColor = floorColor;
            GridColor = gridColor;
            ReactorColor = reactorColor;
            ProjectileColor = projectileColor;
            ExplosionColor = explosionColor;
        }
    }

    /// <summary>Provides the 5 campaign sector configurations with progressive weapon evolutions and perks.</summary>
    public static class SectorLevelConfig
    {
        public const int TotalLevels = 5;

        private static readonly SectorLevelData[] Levels =
        {
            new SectorLevelData(
                levelNumber: 1,
                sectorName: "SEKTÖR 1: ALFA KARAKOLU",
                subtitle: "Öncü Savunma Hattı",
                weaponEvolutionTitle: "Silah: Tekli Mavi Lazer Tüfeği (1x) + Tekli Güdümlü Bomba (1x)",
                specialPerkTitle: "Özel Eklenti: Başlangıç Rezervi (150 Enerji Kristali)",
                primaryProjectileCount: 1,
                primarySpreadAngle: 0f,
                primaryExplosionRadius: 1.1f,
                missileSalvoCount: 1,
                weaponDamageMultiplier: 1.0f,
                coolingMultiplier: 1.0f,
                moveSpeedMultiplier: 1.0f,
                pickupRadiusBonus: 1.0f,
                passiveRegenPerSecond: 0f,
                turretFireRateBonus: 1.0f,
                startingCrystals: 150,
                enemyHealthMultiplier: 0.9f,
                enemySpeedMultiplier: 0.95f,
                extraEnemiesPerGroup: 0,
                persistentNightAtmosphere: false,
                floorColor: new Color(0.16f, 0.22f, 0.30f),
                gridColor: new Color(0.15f, 0.85f, 1.0f),
                reactorColor: new Color(0.15f, 0.9f, 1.0f),
                projectileColor: new Color(0.2f, 0.95f, 1.0f),
                explosionColor: new Color(0.2f, 0.85f, 1.0f)),

            new SectorLevelData(
                levelNumber: 2,
                sectorName: "SEKTÖR 2: BETA VADİSİ",
                subtitle: "İkiz Koridor Kuşatması",
                weaponEvolutionTitle: "Silah Evrimi: İKİLİ ZÜMRÜT TÜFEK (2x Mermi!) + ÇİFT GÜDÜMLÜ BOMBA (2x!)",
                specialPerkTitle: "Özel Eklenti: Turbo İtici (+%20 Hız) & 2x Kristal Mıknatısı (180 Kristal)",
                primaryProjectileCount: 2,
                primarySpreadAngle: 7f,
                primaryExplosionRadius: 1.8f,
                missileSalvoCount: 2,
                weaponDamageMultiplier: 1.18f,
                coolingMultiplier: 1.25f,
                moveSpeedMultiplier: 1.2f,
                pickupRadiusBonus: 2.0f,
                passiveRegenPerSecond: 1.5f,
                turretFireRateBonus: 1.15f,
                startingCrystals: 180,
                enemyHealthMultiplier: 1.05f,
                enemySpeedMultiplier: 1.0f,
                extraEnemiesPerGroup: 2,
                persistentNightAtmosphere: false,
                floorColor: new Color(0.12f, 0.26f, 0.22f),
                gridColor: new Color(0.2f, 1.0f, 0.65f),
                reactorColor: new Color(0.2f, 1.0f, 0.55f),
                projectileColor: new Color(0.2f, 1.0f, 0.45f),
                explosionColor: new Color(0.3f, 1.0f, 0.55f)),

            new SectorLevelData(
                levelNumber: 3,
                sectorName: "SEKTÖR 3: GAMA KRATERİ",
                subtitle: "Gece Fırtınası & Elektronik Karartma",
                weaponEvolutionTitle: "Silah Evrimi: ÜÇLÜ YILDIRIM TÜFEĞİ (3x Mermi!) + 3'LÜ BOMBA SALVOSU (3x!)",
                specialPerkTitle: "Özel Eklenti: Kriyo-Soğutucu (-%40 Isınma) & Taktik Gece Projektörü (210 Kristal)",
                primaryProjectileCount: 3,
                primarySpreadAngle: 14f,
                primaryExplosionRadius: 2.4f,
                missileSalvoCount: 3,
                weaponDamageMultiplier: 1.3f,
                coolingMultiplier: 1.5f,
                moveSpeedMultiplier: 1.25f,
                pickupRadiusBonus: 2.2f,
                passiveRegenPerSecond: 3f,
                turretFireRateBonus: 1.25f,
                startingCrystals: 210,
                enemyHealthMultiplier: 1.18f,
                enemySpeedMultiplier: 1.05f,
                extraEnemiesPerGroup: 3,
                persistentNightAtmosphere: true,
                floorColor: new Color(0.18f, 0.14f, 0.28f),
                gridColor: new Color(0.75f, 0.35f, 1.0f),
                reactorColor: new Color(0.8f, 0.3f, 1.0f),
                projectileColor: new Color(0.85f, 0.35f, 1.0f),
                explosionColor: new Color(0.9f, 0.4f, 1.0f)),

            new SectorLevelData(
                levelNumber: 4,
                sectorName: "SEKTÖR 4: DELTA KALESİ",
                subtitle: "Kızıl Zırhlı Tümen Cephesi",
                weaponEvolutionTitle: "Silah Evrimi: DÖRTLÜ KIZIL MAGMA TOPU (4x Mermi!) + 4'LÜ AĞIR BOMBA (4x!)",
                specialPerkTitle: "Özel Eklenti: Nano-Onarım Aurası (Saniyede +6 Can Yenileme & 250 Kristal)",
                primaryProjectileCount: 4,
                primarySpreadAngle: 18f,
                primaryExplosionRadius: 3.1f,
                missileSalvoCount: 4,
                weaponDamageMultiplier: 1.45f,
                coolingMultiplier: 1.75f,
                moveSpeedMultiplier: 1.3f,
                pickupRadiusBonus: 2.5f,
                passiveRegenPerSecond: 6f,
                turretFireRateBonus: 1.35f,
                startingCrystals: 250,
                enemyHealthMultiplier: 1.32f,
                enemySpeedMultiplier: 1.08f,
                extraEnemiesPerGroup: 4,
                persistentNightAtmosphere: false,
                floorColor: new Color(0.28f, 0.16f, 0.14f),
                gridColor: new Color(1.0f, 0.55f, 0.18f),
                reactorColor: new Color(1.0f, 0.45f, 0.15f),
                projectileColor: new Color(1.0f, 0.42f, 0.12f),
                explosionColor: new Color(1.0f, 0.35f, 0.1f)),

            new SectorLevelData(
                levelNumber: 5,
                sectorName: "SEKTÖR 5: OMEGA MERKEZ ÜSSÜ",
                subtitle: "Overlord Ana Çekirdek Kuşatması",
                weaponEvolutionTitle: "Silah Evrimi: OMEGA 5'Lİ HYPER-CANNON (5x Mermi!) + 5'Lİ NÜKLEER BOMBA (5x!)",
                specialPerkTitle: "Özel Eklenti: Komuta Aurası (Kulelere +%50 Ateş Hızı, Oto-Onarım & 300 Kristal)",
                primaryProjectileCount: 5,
                primarySpreadAngle: 24f,
                primaryExplosionRadius: 4.0f,
                missileSalvoCount: 5,
                weaponDamageMultiplier: 1.65f,
                coolingMultiplier: 2.0f,
                moveSpeedMultiplier: 1.38f,
                pickupRadiusBonus: 3.0f,
                passiveRegenPerSecond: 9f,
                turretFireRateBonus: 1.5f,
                startingCrystals: 300,
                enemyHealthMultiplier: 1.48f,
                enemySpeedMultiplier: 1.12f,
                extraEnemiesPerGroup: 5,
                persistentNightAtmosphere: false,
                floorColor: new Color(0.24f, 0.14f, 0.26f),
                gridColor: new Color(1.0f, 0.82f, 0.2f),
                reactorColor: new Color(1.0f, 0.78f, 0.15f),
                projectileColor: new Color(1.0f, 0.88f, 0.22f),
                explosionColor: new Color(1.0f, 0.75f, 0.15f))
        };

        public static SectorLevelData GetLevel(int levelNumber)
        {
            int index = Mathf.Clamp(levelNumber, 1, TotalLevels) - 1;
            return Levels[index];
        }
    }
}
