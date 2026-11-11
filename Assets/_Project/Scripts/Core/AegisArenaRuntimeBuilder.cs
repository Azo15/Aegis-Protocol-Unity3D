using System.Collections.Generic;
using UnityEngine;
using AegisProtocol.AI;
using AegisProtocol.Audio;
using AegisProtocol.Combat;
using AegisProtocol.Core;
using AegisProtocol.Systems;
using AegisProtocol.UI;

namespace AegisProtocol.Core
{
    /// <summary>Builds a complete playable 3D arena and connects every runtime gameplay system.</summary>
    [DisallowMultipleComponent]
    public sealed class AegisArenaRuntimeBuilder : MonoBehaviour
    {
        private readonly List<Material> runtimeMaterials = new List<Material>();
        private readonly List<ScriptableObject> runtimeConfigs = new List<ScriptableObject>();
        private readonly List<Mesh> runtimeMeshes = new List<Mesh>();
        private GameObject runtimeRoot;
        private Transform reactorRingA;
        private Transform reactorRingB;
        private bool hasBuilt;

        public HealthComponent MechHealth { get; private set; }
        public HealthComponent ReactorHealth { get; private set; }
        public Transform ArenaRoot => runtimeRoot != null ? runtimeRoot.transform : null;

        public void Build(GameManager manager)
        {
            if (hasBuilt)
                return;

            GameObject existing = GameObject.Find("Aegis Runtime Arena");
            if (existing != null)
            {
                runtimeRoot = existing;
                hasBuilt = true;
                return;
            }

            hasBuilt = true;
            SectorLevelData levelData = manager != null ? manager.CurrentLevelData : SectorLevelConfig.GetLevel(1);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = levelData.PersistentNightAtmosphere
                ? new Color(0.22f, 0.20f, 0.36f)
                : new Color(0.38f, 0.46f, 0.58f);
            runtimeRoot = new GameObject("Aegis Runtime Arena");
            Material floorMaterial = CreateMaterial(levelData.FloorColor, levelData.FloorColor * 0.45f);
            Material gridMaterial = CreateMaterial(levelData.GridColor, levelData.GridColor * 0.9f);
            Material wallMaterial = CreateMaterial(new Color(0.28f, 0.36f, 0.48f), new Color(0.06f, 0.12f, 0.20f));
            Material reactorMaterial = CreateMaterial(levelData.ReactorColor, levelData.ReactorColor * 1.4f);
            Material mechMaterial = CreateMaterial(new Color(0.25f, 0.55f, 0.78f), new Color(0.08f, 0.25f, 0.40f));
            Material accentMaterial = CreateMaterial(levelData.GridColor, levelData.GridColor * 1.35f);

            BuildArenaFloor(floorMaterial, gridMaterial, wallMaterial);
            BuildReactor(reactorMaterial, out HealthComponent reactorHealth);
            ReactorHealth = reactorHealth;
            Transform mech = BuildPlayerMech(mechMaterial, accentMaterial, out HealthComponent mechHealth,
                out MechController mechController,
                out PlayerInputReader inputReader, out WeaponSystem weaponSystem,
                out Light tacticalSpotlight,
                out ProjectileController plasmaTemplate, out ProjectileController missileTemplate,
                out EnergyCrystalPickup crystalTemplate);

            MechHealth = mechHealth;
            Camera mainCamera = EnsureMainCamera();
            IsometricCameraRig cameraRig = mainCamera.GetComponent<IsometricCameraRig>();
            if (cameraRig == null)
                cameraRig = mainCamera.gameObject.AddComponent<IsometricCameraRig>();
            cameraRig.ConfigureRuntime(mech, mechController, inputReader, tacticalSpotlight);
            if (levelData.PersistentNightAtmosphere)
                cameraRig.SetTacticalLightEnabled(true);
            mechController.ConfigureRuntimeReferences(mech.Find("Chassis"), mech.Find("Chassis/Turret"), mainCamera);
            mechController.ApplyMoveSpeedMultiplier(levelData.MoveSpeedMultiplier);
            weaponSystem.ConfigureRuntimeReferences(
                mech.Find("Chassis/Turret/Plasma Muzzle"), plasmaTemplate, missileTemplate, cameraRig);
            weaponSystem.ConfigureLevelLoadout(levelData);

            CrystalResourceBank bank = mech.GetComponent<CrystalResourceBank>();
            if (bank != null)
                bank.ConfigureStartingBalance(levelData.StartingCrystals, levelData.PickupRadiusBonus);
            TacticalBuildManager buildManager = mech.GetComponent<TacticalBuildManager>();
            if (buildManager != null)
                buildManager.ApplyGlobalTurretFireRateMultiplier(levelData.TurretFireRateBonus);

            CreateBuildNodes(reactorMaterial);
            CreateSatelliteTerminals(accentMaterial);
            Transform[] spawnPoints = CreateSpawnPoints(accentMaterial);
            EnemyPrefabDefinition[] enemyDefinitions = CreateEnemyTemplates(crystalTemplate, accentMaterial, levelData);

            if (manager == null)
            {
                GameObject managerObject = new GameObject("GameManager");
                manager = managerObject.AddComponent<GameManager>();
            }
            manager.ConfigureObjectives(MechHealth, ReactorHealth);

            WaveSpawnerManager waveSpawner = runtimeRoot.AddComponent<WaveSpawnerManager>();
            waveSpawner.ConfigureRuntime(enemyDefinitions, spawnPoints, FindDirectionalLight(), cameraRig);
            runtimeRoot.AddComponent<RogueliteUpgradeManager>();

            if (FindFirstObjectByType<AudioManager>() == null)
                runtimeRoot.AddComponent<AudioManager>();
            if (FindFirstObjectByType<UIManager>() == null)
                runtimeRoot.AddComponent<UIManager>();
        }

        private void Update()
        {
            if (reactorRingA != null)
                reactorRingA.Rotate(Vector3.up, 18f * Time.deltaTime, Space.Self);
            if (reactorRingB != null)
                reactorRingB.Rotate(Vector3.up, -27f * Time.deltaTime, Space.Self);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < runtimeMaterials.Count; i++)
            {
                if (runtimeMaterials[i] != null)
                    Destroy(runtimeMaterials[i]);
            }
            runtimeMaterials.Clear();
            for (int i = 0; i < runtimeConfigs.Count; i++)
            {
                if (runtimeConfigs[i] != null)
                    Destroy(runtimeConfigs[i]);
            }
            runtimeConfigs.Clear();
            for (int i = 0; i < runtimeMeshes.Count; i++)
            {
                if (runtimeMeshes[i] != null)
                    Destroy(runtimeMeshes[i]);
            }
            runtimeMeshes.Clear();
        }

        private void BuildArenaFloor(Material floorMaterial, Material gridMaterial, Material wallMaterial)
        {
            CreatePrimitive("Foundation", PrimitiveType.Cube, runtimeRoot.transform,
                new Vector3(0f, -0.6f, 0f), new Vector3(100f, 1.2f, 100f), floorMaterial);

            for (int coordinate = -45; coordinate <= 45; coordinate += 5)
            {
                GameObject gridX = CreatePrimitive("Grid Line X", PrimitiveType.Cube, runtimeRoot.transform,
                    new Vector3(coordinate, 0.015f, 0f), new Vector3(0.035f, 0.025f, 94f), gridMaterial);
                GameObject gridZ = CreatePrimitive("Grid Line Z", PrimitiveType.Cube, runtimeRoot.transform,
                    new Vector3(0f, 0.016f, coordinate), new Vector3(94f, 0.025f, 0.035f), gridMaterial);
                gridX.GetComponent<Collider>().enabled = false;
                gridZ.GetComponent<Collider>().enabled = false;
            }

            CreatePrimitive("North Boundary", PrimitiveType.Cube, runtimeRoot.transform,
                new Vector3(0f, 2f, 49f), new Vector3(100f, 4f, 2f), wallMaterial);
            CreatePrimitive("South Boundary", PrimitiveType.Cube, runtimeRoot.transform,
                new Vector3(0f, 2f, -49f), new Vector3(100f, 4f, 2f), wallMaterial);
            CreatePrimitive("East Boundary", PrimitiveType.Cube, runtimeRoot.transform,
                new Vector3(49f, 2f, 0f), new Vector3(2f, 4f, 100f), wallMaterial);
            CreatePrimitive("West Boundary", PrimitiveType.Cube, runtimeRoot.transform,
                new Vector3(-49f, 2f, 0f), new Vector3(2f, 4f, 100f), wallMaterial);

            for (int corridor = -1; corridor <= 1; corridor++)
            {
                CreatePrimitive("North Corridor Cover", PrimitiveType.Cube, runtimeRoot.transform,
                    new Vector3(corridor * 11f, 0.7f, 31f), new Vector3(5f, 1.4f, 2f), wallMaterial);
                CreatePrimitive("South Corridor Cover", PrimitiveType.Cube, runtimeRoot.transform,
                    new Vector3(corridor * 11f, 0.7f, -31f), new Vector3(5f, 1.4f, 2f), wallMaterial);
                CreatePrimitive("East Corridor Cover", PrimitiveType.Cube, runtimeRoot.transform,
                    new Vector3(31f, 0.7f, corridor * 11f), new Vector3(2f, 1.4f, 5f), wallMaterial);
                CreatePrimitive("West Corridor Cover", PrimitiveType.Cube, runtimeRoot.transform,
                    new Vector3(-31f, 0.7f, corridor * 11f), new Vector3(2f, 1.4f, 5f), wallMaterial);
            }
        }

        private Transform BuildReactor(Material reactorMaterial, out HealthComponent health)
        {
            GameObject reactorObject = new GameObject("Central Quantum Reactor");
            reactorObject.transform.SetParent(runtimeRoot.transform, false);
            reactorObject.transform.position = new Vector3(0f, 0f, 0f);
            health = reactorObject.AddComponent<HealthComponent>();
            health.Configure(HealthRole.Reactor, HealthComponent.ReactorDefaultMaxHealth, true);

            CapsuleCollider castleCollider = reactorObject.AddComponent<CapsuleCollider>();
            castleCollider.radius = 2.8f;
            castleCollider.height = 4.5f;
            castleCollider.center = new Vector3(0f, 2.2f, 0f);

            Material stoneWallMat = CreateMaterial(new Color(0.24f, 0.32f, 0.44f), new Color(0.05f, 0.10f, 0.16f));
            Material darkTrimMat = CreateMaterial(new Color(0.16f, 0.22f, 0.32f), new Color(0.03f, 0.07f, 0.12f));

            // Base Castle Platform
            DisableCollider(CreatePrimitive("Castle Courtyard Base", PrimitiveType.Cube, reactorObject.transform,
                new Vector3(0f, 0.25f, 0f), new Vector3(6.8f, 0.5f, 6.8f), darkTrimMat));

            // 4 Castle Curtain Walls
            DisableCollider(CreatePrimitive("Castle Wall North", PrimitiveType.Cube, reactorObject.transform,
                new Vector3(0f, 1.35f, 2.85f), new Vector3(5.6f, 2.2f, 0.65f), stoneWallMat));
            DisableCollider(CreatePrimitive("Castle Wall South", PrimitiveType.Cube, reactorObject.transform,
                new Vector3(0f, 1.35f, -2.85f), new Vector3(5.6f, 2.2f, 0.65f), stoneWallMat));
            DisableCollider(CreatePrimitive("Castle Wall East", PrimitiveType.Cube, reactorObject.transform,
                new Vector3(2.85f, 1.35f, 0f), new Vector3(0.65f, 2.2f, 5.6f), stoneWallMat));
            DisableCollider(CreatePrimitive("Castle Wall West", PrimitiveType.Cube, reactorObject.transform,
                new Vector3(-2.85f, 1.35f, 0f), new Vector3(0.65f, 2.2f, 5.6f), stoneWallMat));

            // Castle Wall Battlements (Merlons / Kale Burcu Dişleri)
            for (int m = -1; m <= 1; m++)
            {
                float offset = m * 1.45f;
                DisableCollider(CreatePrimitive("Battlement N", PrimitiveType.Cube, reactorObject.transform,
                    new Vector3(offset, 2.7f, 2.85f), new Vector3(0.75f, 0.55f, 0.75f), darkTrimMat));
                DisableCollider(CreatePrimitive("Battlement S", PrimitiveType.Cube, reactorObject.transform,
                    new Vector3(offset, 2.7f, -2.85f), new Vector3(0.75f, 0.55f, 0.75f), darkTrimMat));
                DisableCollider(CreatePrimitive("Battlement E", PrimitiveType.Cube, reactorObject.transform,
                    new Vector3(2.85f, 2.7f, offset), new Vector3(0.75f, 0.55f, 0.75f), darkTrimMat));
                DisableCollider(CreatePrimitive("Battlement W", PrimitiveType.Cube, reactorObject.transform,
                    new Vector3(-2.85f, 2.7f, offset), new Vector3(0.75f, 0.55f, 0.75f), darkTrimMat));
            }

            // 4 Corner Castle Watchtowers (Köşe Kale Kuleleri)
            Vector3[] towerCorners =
            {
                new Vector3(2.95f, 0f, 2.95f),
                new Vector3(-2.95f, 0f, 2.95f),
                new Vector3(2.95f, 0f, -2.95f),
                new Vector3(-2.95f, 0f, -2.95f)
            };
            for (int t = 0; t < towerCorners.Length; t++)
            {
                Vector3 c = towerCorners[t];
                DisableCollider(CreatePrimitive($"Castle Watchtower {t + 1}", PrimitiveType.Cylinder, reactorObject.transform,
                    c + new Vector3(0f, 1.85f, 0f), new Vector3(1.55f, 1.85f, 1.55f), stoneWallMat));
                DisableCollider(CreatePrimitive($"Watchtower Crown {t + 1}", PrimitiveType.Cylinder, reactorObject.transform,
                    c + new Vector3(0f, 3.85f, 0f), new Vector3(1.85f, 0.32f, 1.85f), darkTrimMat));
                DisableCollider(CreatePrimitive($"Watchtower Beacon {t + 1}", PrimitiveType.Sphere, reactorObject.transform,
                    c + new Vector3(0f, 4.35f, 0f), new Vector3(0.7f, 0.7f, 0.7f), reactorMaterial));
            }

            // Central Castle Keep (Ana Kale Burcu)
            DisableCollider(CreatePrimitive("Central Keep Lower", PrimitiveType.Cube, reactorObject.transform,
                new Vector3(0f, 1.8f, 0f), new Vector3(3.2f, 3.2f, 3.2f), stoneWallMat));
            DisableCollider(CreatePrimitive("Central Keep Upper", PrimitiveType.Cylinder, reactorObject.transform,
                new Vector3(0f, 3.6f, 0f), new Vector3(2.4f, 1.2f, 2.4f), darkTrimMat));
            DisableCollider(CreatePrimitive("Quantum Core", PrimitiveType.Sphere, reactorObject.transform,
                new Vector3(0f, 5.1f, 0f), new Vector3(2.1f, 2.1f, 2.1f), reactorMaterial));

            reactorRingA = CreateTorus("Reactor Energy Ring A", reactorObject.transform, 4.6f, 0.15f, 48, reactorMaterial).transform;
            reactorRingA.localPosition = new Vector3(0f, 3.8f, 0f);
            reactorRingA.localRotation = Quaternion.Euler(20f, 0f, 0f);
            reactorRingB = CreateTorus("Reactor Energy Ring B", reactorObject.transform, 5.2f, 0.12f, 48, reactorMaterial).transform;
            reactorRingB.localPosition = new Vector3(0f, 3.8f, 0f);
            reactorRingB.localRotation = Quaternion.Euler(-18f, 42f, 0f);

            GameObject lightObject = new GameObject("Reactor Core Light");
            lightObject.transform.SetParent(reactorObject.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 5.2f, 0f);
            Light coreLight = lightObject.AddComponent<Light>();
            coreLight.type = LightType.Point;
            coreLight.color = new Color(0.2f, 0.85f, 1f);
            coreLight.intensity = 6.5f;
            coreLight.range = 20f;
            return reactorObject.transform;
        }

        private static void DisableCollider(GameObject obj)
        {
            if (obj == null)
                return;
            Collider col = obj.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;
        }

        private Transform BuildPlayerMech(
            Material bodyMaterial,
            Material accentMaterial,
            out HealthComponent mechHealth,
            out MechController controller,
            out PlayerInputReader inputReader,
            out WeaponSystem weaponSystem,
            out Light tacticalSpotlight,
            out ProjectileController plasmaTemplate,
            out ProjectileController missileTemplate,
            out EnergyCrystalPickup crystalTemplate)
        {
            GameObject mech = new GameObject("Player Mech");
            mech.transform.SetParent(runtimeRoot.transform, false);
            mech.transform.position = new Vector3(0f, 1.1f, -8f);
            Rigidbody body = mech.AddComponent<Rigidbody>();
            body.mass = 4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            CapsuleCollider capsule = mech.AddComponent<CapsuleCollider>();
            capsule.height = 2.4f;
            capsule.radius = 0.85f;
            capsule.center = Vector3.up * 0.1f;

            Transform chassis = new GameObject("Chassis").transform;
            chassis.SetParent(mech.transform, false);
            GameObject lowerChassis = CreatePrimitive("Lower Chassis", PrimitiveType.Cube, chassis,
                new Vector3(0f, -0.15f, 0f), new Vector3(2.4f, 0.75f, 3.2f), bodyMaterial);
            GameObject leftTrack = CreatePrimitive("Left Track", PrimitiveType.Cube, chassis,
                new Vector3(-1.15f, -0.35f, 0f), new Vector3(0.35f, 0.55f, 3.4f), accentMaterial);
            GameObject rightTrack = CreatePrimitive("Right Track", PrimitiveType.Cube, chassis,
                new Vector3(1.15f, -0.35f, 0f), new Vector3(0.35f, 0.55f, 3.4f), accentMaterial);
            lowerChassis.GetComponent<Collider>().enabled = false;
            leftTrack.GetComponent<Collider>().enabled = false;
            rightTrack.GetComponent<Collider>().enabled = false;

            Transform turret = new GameObject("Turret").transform;
            turret.SetParent(chassis, false);
            turret.localPosition = new Vector3(0f, 0.35f, 0f);
            GameObject turretArmor = CreatePrimitive("Turret Armor", PrimitiveType.Sphere, turret,
                new Vector3(0f, 0.1f, 0f), new Vector3(1.5f, 0.85f, 1.6f), accentMaterial);
            GameObject plasmaBarrel = CreatePrimitive("Plasma Barrel", PrimitiveType.Cylinder, turret,
                new Vector3(0f, 0.05f, 1.15f), new Vector3(0.24f, 0.85f, 0.24f), bodyMaterial,
                Quaternion.Euler(90f, 0f, 0f));
            turretArmor.GetComponent<Collider>().enabled = false;
            plasmaBarrel.GetComponent<Collider>().enabled = false;
            Transform muzzle = new GameObject("Plasma Muzzle").transform;
            muzzle.SetParent(turret, false);
            muzzle.localPosition = new Vector3(0f, 0.05f, 2.0f);
            muzzle.localRotation = Quaternion.identity;

            GameObject spotlightObject = new GameObject("Tactical Spot Light");
            spotlightObject.transform.SetParent(turret, false);
            spotlightObject.transform.localPosition = new Vector3(0f, 0.2f, 0.5f);
            spotlightObject.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            tacticalSpotlight = spotlightObject.AddComponent<Light>();
            tacticalSpotlight.type = LightType.Spot;
            tacticalSpotlight.color = new Color(0.35f, 0.85f, 1f);
            tacticalSpotlight.intensity = 6f;
            tacticalSpotlight.range = 28f;
            tacticalSpotlight.spotAngle = 62f;
            tacticalSpotlight.enabled = false;

            inputReader = mech.AddComponent<PlayerInputReader>();
            mechHealth = mech.AddComponent<HealthComponent>();
            controller = mech.AddComponent<MechController>();
            controller.ConfigureRuntimeReferences(chassis, turret, Camera.main);
            weaponSystem = mech.AddComponent<WeaponSystem>();
            mech.AddComponent<CrystalResourceBank>();
            mech.AddComponent<MechInteractionScanner>();
            mech.AddComponent<TacticalBuildManager>();

            Material plasmaMat = CreateMaterial(new Color(0.3f, 1f, 1f), new Color(0.4f, 1.8f, 2.2f));
            Material missileMat = CreateMaterial(new Color(1f, 0.65f, 0.15f), new Color(1.8f, 0.9f, 0.2f));
            crystalTemplate = CreateCrystalTemplate(accentMaterial);
            plasmaTemplate = CreateProjectileTemplate("Plasma Projectile Template", plasmaMat, 0.45f);
            missileTemplate = CreateProjectileTemplate("Missile Projectile Template", missileMat, 0.62f);
            return mech.transform;
        }

        private EnergyCrystalPickup CreateCrystalTemplate(Material material)
        {
            GameObject templates = GetOrCreateTemplatesRoot();
            GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystal.name = "Energy Crystal Pickup Template";
            crystal.transform.SetParent(templates.transform, false);
            crystal.transform.localScale = new Vector3(0.75f, 0.45f, 0.75f);
            SetMaterial(crystal, material);

            Material medkitGreenMat = CreateMaterial(new Color(0.18f, 0.96f, 0.35f), new Color(0.3f, 1.8f, 0.55f));
            DisableCollider(CreatePrimitive("MedkitCrossH", PrimitiveType.Cube, crystal.transform,
                new Vector3(0f, 0.62f, 0f), new Vector3(0.72f, 0.25f, 0.24f), medkitGreenMat));
            DisableCollider(CreatePrimitive("MedkitCrossV", PrimitiveType.Cube, crystal.transform,
                new Vector3(0f, 0.62f, 0f), new Vector3(0.24f, 0.25f, 0.72f), medkitGreenMat));

            crystal.AddComponent<EnergyCrystalPickup>();
            crystal.SetActive(false);
            return crystal.GetComponent<EnergyCrystalPickup>();
        }

        private ProjectileController CreateProjectileTemplate(string name, Material material, float scale)
        {
            GameObject templates = GetOrCreateTemplatesRoot();
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = name;
            projectile.transform.SetParent(templates.transform, false);
            projectile.transform.localScale = Vector3.one * scale;
            SphereCollider sc = projectile.GetComponent<SphereCollider>();
            if (sc != null)
                sc.radius = 0.85f;
            SetMaterial(projectile, material);
            ProjectileController controller = projectile.AddComponent<ProjectileController>();
            projectile.SetActive(false);
            return controller;
        }

        private EnemyPrefabDefinition[] CreateEnemyTemplates(
            EnergyCrystalPickup crystalPrefab,
            Material projectileMaterial,
            SectorLevelData levelData)
        {
            GameObject templates = GetOrCreateTemplatesRoot();
            GameObject energyProjectile = CreatePrimitive("Enemy Projectile Template", PrimitiveType.Sphere,
                templates.transform, Vector3.zero, Vector3.one * 0.36f, projectileMaterial);
            ProjectileController projectileController = energyProjectile.AddComponent<ProjectileController>();
            energyProjectile.SetActive(false);

            EnemyType[] types =
            {
                EnemyType.ScoutDrone,
                EnemyType.RangedStriker,
                EnemyType.KamikazeDrone,
                EnemyType.HeavySiegeMech,
                EnemyType.OverlordTitanBoss
            };
            float[] hp = { 60f, 90f, 40f, 360f, 2200f };
            float[] speed = { 2.9f, 2.5f, 4.6f, 1.75f, 1.45f };
            float[] range = { 2.2f, 10f, 1.5f, 2.5f, 14f };
            float[] damage = { 6f, 8f, 30f, 25f, 22f };
            float[] cooldown = { 1.6f, 1.8f, 1.2f, 2.5f, 1.3f };
            int[] score = { 50, 100, 75, 250, 1500 };
            int[] crystals = { 15, 20, 18, 35, 100 };
            float[] scale = { 1.15f, 1.25f, 1.0f, 1.8f, 3.2f };
            Color[] colors =
            {
                new Color(1f, 0.3f, 0.35f),
                new Color(1f, 0.55f, 0.1f),
                new Color(1f, 0.12f, 0.08f),
                new Color(0.75f, 0.35f, 0.9f),
                new Color(1f, 0.1f, 0.85f)
            };

            EnemyPrefabDefinition[] definitions = new EnemyPrefabDefinition[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                int lvl = Mathf.Clamp(levelData.LevelNumber, 1, 5);
                Color stageColor = GetStageEnemyColor(types[i], lvl, colors[i]);
                float stageScale = scale[i] * (1f + (lvl - 1) * 0.08f);
                float stageDamage = damage[i] * (1f + (lvl - 1) * 0.16f);
                float stageCooldown = cooldown[i] * Mathf.Max(0.62f, 1f - (lvl - 1) * 0.08f);
                int stageCrystals = crystals[i] + (lvl - 1) * 5;

                EnemyArchetypeConfig config = ScriptableObject.CreateInstance<EnemyArchetypeConfig>();
                config.name = types[i] + " Runtime Config";
                runtimeConfigs.Add(config);
                config.Configure(
                    types[i],
                    hp[i] * levelData.EnemyHealthMultiplier,
                    speed[i] * levelData.EnemySpeedMultiplier,
                    range[i] + (lvl - 1) * 0.6f,
                    stageDamage,
                    stageCooldown,
                    score[i] * levelData.LevelNumber,
                    stageCrystals,
                    stageScale,
                    stageColor);
                if (types[i] == EnemyType.RangedStriker || types[i] == EnemyType.OverlordTitanBoss)
                    config.SetRuntimeProjectilePrefab(energyProjectile);

                GameObject enemy = new GameObject(types[i] + " Enemy Template");
                enemy.SetActive(false);
                enemy.transform.SetParent(templates.transform, false);
                SphereCollider collider = enemy.AddComponent<SphereCollider>();
                collider.radius = 1.25f;
                collider.center = new Vector3(0f, 0.3f, 0f);
                enemy.AddComponent<HealthComponent>();
                EnemyAIController ai = enemy.AddComponent<EnemyAIController>();
                ai.ConfigureRuntime(config, crystalPrefab);
                BuildEnemyVisualModel(enemy.transform, types[i], stageColor, lvl);

                definitions[i] = new EnemyPrefabDefinition
                {
                    type = types[i],
                    prefab = ai,
                    config = config,
                    crystalPrefab = crystalPrefab
                };
            }
            return definitions;
        }

        private static Color GetStageEnemyColor(EnemyType type, int levelNumber, Color fallback)
        {
            float typeShift = ((int)type) * 0.06f;
            switch (levelNumber)
            {
                case 1:
                    return fallback;
                case 2:
                    // Sector 2 Desert Raiders: Scorched Gold & Bronze-Amber
                    return Color.Lerp(new Color(1f, 0.68f, 0.12f), new Color(0.95f, 0.35f, 0.08f), typeShift);
                case 3:
                    // Sector 3 Gamma Crater: Cyber Neon-Violet & Electric Magenta
                    return Color.Lerp(new Color(0.72f, 0.18f, 1f), new Color(1f, 0.15f, 0.68f), typeShift);
                case 4:
                    // Sector 4 Delta Fortress: Toxic Emerald & Acid-Lime Siege Armor
                    return Color.Lerp(new Color(0.15f, 0.95f, 0.38f), new Color(0.55f, 1f, 0.12f), typeShift);
                case 5:
                    // Sector 5 Omega Core: Blazing Crimson-Gold Apex War Machines
                    return Color.Lerp(new Color(1f, 0.12f, 0.18f), new Color(1f, 0.82f, 0.15f), typeShift);
                default:
                    return fallback;
            }
        }

        private void BuildEnemyVisualModel(Transform parent, EnemyType type, Color primaryColor, int levelNumber = 1)
        {
            Material hullMat = CreateMaterial(primaryColor, primaryColor * (1.05f + levelNumber * 0.14f));
            Color metalTint = levelNumber >= 5
                ? new Color(0.10f, 0.08f, 0.14f)
                : new Color(0.18f, 0.22f, 0.28f);
            Material darkMetalMat = CreateMaterial(metalTint, primaryColor * (0.08f * levelNumber));
            Color eyeColor = levelNumber == 3
                ? new Color(0.25f, 1f, 1f)
                : (levelNumber >= 5 ? new Color(1f, 0.95f, 0.25f) : new Color(1f, 0.85f, 0.2f));
            Material eyeGlowMat = CreateMaterial(eyeColor, eyeColor * (1.8f + levelNumber * 0.25f));

            switch (type)
            {
                case EnemyType.ScoutDrone:
                {
                    // Sleek winged interceptor drone
                    DisableCollider(CreatePrimitive("PrimaryHull", PrimitiveType.Sphere, parent,
                        new Vector3(0f, 0.35f, 0f), new Vector3(0.95f, 0.55f, 1.25f), hullMat));
                    DisableCollider(CreatePrimitive("Left Wing", PrimitiveType.Cube, parent,
                        new Vector3(-0.75f, 0.35f, -0.1f), new Vector3(0.95f, 0.12f, 0.55f), darkMetalMat,
                        Quaternion.Euler(0f, -18f, -8f)));
                    DisableCollider(CreatePrimitive("Right Wing", PrimitiveType.Cube, parent,
                        new Vector3(0.75f, 0.35f, -0.1f), new Vector3(0.95f, 0.12f, 0.55f), darkMetalMat,
                        Quaternion.Euler(0f, 18f, 8f)));
                    DisableCollider(CreatePrimitive("Drone Visor Eye", PrimitiveType.Sphere, parent,
                        new Vector3(0f, 0.4f, 0.55f), new Vector3(0.42f, 0.25f, 0.3f), eyeGlowMat));
                    DisableCollider(CreatePrimitive("Nose Gun", PrimitiveType.Cylinder, parent,
                        new Vector3(0f, 0.22f, 0.72f), new Vector3(0.14f, 0.35f, 0.14f), darkMetalMat,
                        Quaternion.Euler(90f, 0f, 0f)));
                    break;
                }

                case EnemyType.RangedStriker:
                {
                    // Armored hover-tank with dual cannon turret
                    DisableCollider(CreatePrimitive("PrimaryHull", PrimitiveType.Cube, parent,
                        new Vector3(0f, 0.22f, 0f), new Vector3(1.25f, 0.45f, 1.55f), hullMat));
                    DisableCollider(CreatePrimitive("Left Track", PrimitiveType.Cube, parent,
                        new Vector3(-0.75f, 0.12f, 0f), new Vector3(0.28f, 0.38f, 1.7f), darkMetalMat));
                    DisableCollider(CreatePrimitive("Right Track", PrimitiveType.Cube, parent,
                        new Vector3(0.75f, 0.12f, 0f), new Vector3(0.28f, 0.38f, 1.7f), darkMetalMat));
                    DisableCollider(CreatePrimitive("Turret Dome", PrimitiveType.Sphere, parent,
                        new Vector3(0f, 0.58f, -0.05f), new Vector3(0.92f, 0.55f, 0.98f), hullMat));
                    DisableCollider(CreatePrimitive("Left Cannon", PrimitiveType.Cylinder, parent,
                        new Vector3(-0.24f, 0.58f, 0.75f), new Vector3(0.15f, 0.55f, 0.15f), darkMetalMat,
                        Quaternion.Euler(90f, 0f, 0f)));
                    DisableCollider(CreatePrimitive("Right Cannon", PrimitiveType.Cylinder, parent,
                        new Vector3(0.24f, 0.58f, 0.75f), new Vector3(0.15f, 0.55f, 0.15f), darkMetalMat,
                        Quaternion.Euler(90f, 0f, 0f)));
                    DisableCollider(CreatePrimitive("Striker Visor", PrimitiveType.Cube, parent,
                        new Vector3(0f, 0.65f, 0.42f), new Vector3(0.55f, 0.18f, 0.2f), eyeGlowMat));
                    break;
                }

                case EnemyType.KamikazeDrone:
                {
                    // Spiked explosive warhead spider-drone
                    DisableCollider(CreatePrimitive("PrimaryHull", PrimitiveType.Capsule, parent,
                        new Vector3(0f, 0.38f, 0f), new Vector3(0.75f, 0.75f, 0.75f), hullMat,
                        Quaternion.Euler(90f, 0f, 0f)));
                    DisableCollider(CreatePrimitive("Blade X1", PrimitiveType.Cube, parent,
                        new Vector3(0f, 0.38f, 0f), new Vector3(1.65f, 0.12f, 0.28f), darkMetalMat,
                        Quaternion.Euler(0f, 35f, 0f)));
                    DisableCollider(CreatePrimitive("Blade X2", PrimitiveType.Cube, parent,
                        new Vector3(0f, 0.38f, 0f), new Vector3(1.65f, 0.12f, 0.28f), darkMetalMat,
                        Quaternion.Euler(0f, -35f, 0f)));
                    DisableCollider(CreatePrimitive("Explosive Core", PrimitiveType.Sphere, parent,
                        new Vector3(0f, 0.65f, 0f), new Vector3(0.55f, 0.55f, 0.55f), eyeGlowMat));
                    break;
                }

                case EnemyType.HeavySiegeMech:
                {
                    // Bipedal heavy combat robot with shoulder armor and dual arm cannons
                    DisableCollider(CreatePrimitive("Left Leg", PrimitiveType.Cube, parent,
                        new Vector3(-0.38f, 0.12f, 0f), new Vector3(0.36f, 0.65f, 0.42f), darkMetalMat));
                    DisableCollider(CreatePrimitive("Right Leg", PrimitiveType.Cube, parent,
                        new Vector3(0.38f, 0.12f, 0f), new Vector3(0.36f, 0.65f, 0.42f), darkMetalMat));
                    DisableCollider(CreatePrimitive("PrimaryHull", PrimitiveType.Cube, parent,
                        new Vector3(0f, 0.68f, 0f), new Vector3(1.15f, 0.85f, 0.82f), hullMat));
                    DisableCollider(CreatePrimitive("Left Shoulder Pad", PrimitiveType.Sphere, parent,
                        new Vector3(-0.75f, 0.92f, 0f), new Vector3(0.55f, 0.48f, 0.65f), hullMat));
                    DisableCollider(CreatePrimitive("Right Shoulder Pad", PrimitiveType.Sphere, parent,
                        new Vector3(0.75f, 0.92f, 0f), new Vector3(0.55f, 0.48f, 0.65f), hullMat));
                    DisableCollider(CreatePrimitive("Left Arm Cannon", PrimitiveType.Cylinder, parent,
                        new Vector3(-0.78f, 0.62f, 0.55f), new Vector3(0.24f, 0.55f, 0.24f), darkMetalMat,
                        Quaternion.Euler(90f, 0f, 0f)));
                    DisableCollider(CreatePrimitive("Right Arm Cannon", PrimitiveType.Cylinder, parent,
                        new Vector3(0.78f, 0.62f, 0.55f), new Vector3(0.24f, 0.55f, 0.24f), darkMetalMat,
                        Quaternion.Euler(90f, 0f, 0f)));
                    DisableCollider(CreatePrimitive("Mech Head", PrimitiveType.Cube, parent,
                        new Vector3(0f, 1.25f, 0.08f), new Vector3(0.55f, 0.38f, 0.52f), darkMetalMat));
                    DisableCollider(CreatePrimitive("Mech Visor", PrimitiveType.Cube, parent,
                        new Vector3(0f, 1.26f, 0.34f), new Vector3(0.46f, 0.16f, 0.12f), eyeGlowMat));
                    break;
                }

                case EnemyType.OverlordTitanBoss:
                {
                    // Colossal Overlord Titan Boss Mech with shoulder missile pods & crown
                    DisableCollider(CreatePrimitive("Left Titan Tread", PrimitiveType.Cube, parent,
                        new Vector3(-0.52f, 0.12f, 0f), new Vector3(0.46f, 0.65f, 1.25f), darkMetalMat));
                    DisableCollider(CreatePrimitive("Right Titan Tread", PrimitiveType.Cube, parent,
                        new Vector3(0.52f, 0.12f, 0f), new Vector3(0.46f, 0.65f, 1.25f), darkMetalMat));
                    DisableCollider(CreatePrimitive("PrimaryHull", PrimitiveType.Cube, parent,
                        new Vector3(0f, 0.78f, 0f), new Vector3(1.45f, 1.05f, 1.05f), hullMat));
                    DisableCollider(CreatePrimitive("Left Missile Pod", PrimitiveType.Cube, parent,
                        new Vector3(-0.88f, 1.25f, -0.1f), new Vector3(0.48f, 0.62f, 0.68f), hullMat));
                    DisableCollider(CreatePrimitive("Right Missile Pod", PrimitiveType.Cube, parent,
                        new Vector3(0.88f, 1.25f, -0.1f), new Vector3(0.48f, 0.62f, 0.68f), hullMat));
                    DisableCollider(CreatePrimitive("Left Siege Cannon", PrimitiveType.Cylinder, parent,
                        new Vector3(-0.92f, 0.72f, 0.72f), new Vector3(0.3f, 0.72f, 0.3f), darkMetalMat,
                        Quaternion.Euler(90f, 0f, 0f)));
                    DisableCollider(CreatePrimitive("Right Siege Cannon", PrimitiveType.Cylinder, parent,
                        new Vector3(0.92f, 0.72f, 0.72f), new Vector3(0.3f, 0.72f, 0.3f), darkMetalMat,
                        Quaternion.Euler(90f, 0f, 0f)));
                    DisableCollider(CreatePrimitive("Boss Core Chest", PrimitiveType.Sphere, parent,
                        new Vector3(0f, 0.82f, 0.52f), new Vector3(0.62f, 0.62f, 0.35f), eyeGlowMat));
                    DisableCollider(CreatePrimitive("Boss Crown Head", PrimitiveType.Cube, parent,
                        new Vector3(0f, 1.48f, 0.08f), new Vector3(0.68f, 0.48f, 0.62f), darkMetalMat));
                    DisableCollider(CreatePrimitive("Boss Eyes", PrimitiveType.Cube, parent,
                        new Vector3(0f, 1.50f, 0.40f), new Vector3(0.58f, 0.18f, 0.12f), eyeGlowMat));
                    break;
                }
            }

            BuildStageTierEvolution(parent, type, levelNumber, hullMat, darkMetalMat, eyeGlowMat);
        }

        private void BuildStageTierEvolution(
            Transform parent,
            EnemyType type,
            int levelNumber,
            Material hullMat,
            Material darkMetalMat,
            Material glowMat)
        {
            if (levelNumber <= 1)
                return;

            float heightBase = type == EnemyType.OverlordTitanBoss ? 1.05f
                : (type == EnemyType.HeavySiegeMech ? 0.82f : 0.48f);
            float widthSpread = type == EnemyType.OverlordTitanBoss ? 1.15f
                : (type == EnemyType.HeavySiegeMech ? 0.92f : 0.72f);

            // Stage 2+: Angled Side Shield Plates & Twin Heavy Barrels
            if (levelNumber >= 2)
            {
                DisableCollider(CreatePrimitive("Mk2 Left Shield Plate", PrimitiveType.Cube, parent,
                    new Vector3(-widthSpread, heightBase, 0.05f), new Vector3(0.16f, 0.52f, 0.95f), hullMat,
                    Quaternion.Euler(0f, 0f, 16f)));
                DisableCollider(CreatePrimitive("Mk2 Right Shield Plate", PrimitiveType.Cube, parent,
                    new Vector3(widthSpread, heightBase, 0.05f), new Vector3(0.16f, 0.52f, 0.95f), hullMat,
                    Quaternion.Euler(0f, 0f, -16f)));
                DisableCollider(CreatePrimitive("Mk2 Left Twin Barrel", PrimitiveType.Cylinder, parent,
                    new Vector3(-widthSpread * 0.62f, heightBase + 0.12f, 0.78f), new Vector3(0.14f, 0.48f, 0.14f), darkMetalMat,
                    Quaternion.Euler(90f, 0f, 0f)));
                DisableCollider(CreatePrimitive("Mk2 Right Twin Barrel", PrimitiveType.Cylinder, parent,
                    new Vector3(widthSpread * 0.62f, heightBase + 0.12f, 0.78f), new Vector3(0.14f, 0.48f, 0.14f), darkMetalMat,
                    Quaternion.Euler(90f, 0f, 0f)));
            }

            // Stage 3+: Dorsal Plasma Reactor Spire & Floating Energy Halo Ring
            if (levelNumber >= 3)
            {
                DisableCollider(CreatePrimitive("Mk3 Dorsal Plasma Spire", PrimitiveType.Cylinder, parent,
                    new Vector3(0f, heightBase + 0.52f, -0.25f), new Vector3(0.22f, 0.42f, 0.22f), darkMetalMat));
                DisableCollider(CreatePrimitive("Mk3 Spire Core Orb", PrimitiveType.Sphere, parent,
                    new Vector3(0f, heightBase + 0.95f, -0.25f), new Vector3(0.38f, 0.38f, 0.38f), glowMat));
                GameObject halo = CreateTorus("Mk3 Plasma Halo", parent, widthSpread * 1.05f, 0.055f, 18, glowMat);
                halo.transform.localPosition = new Vector3(0f, heightBase + 0.15f, 0f);
            }

            // Stage 4+: Shoulder Missile Pods & Spiked Siege Ram Bumper
            if (levelNumber >= 4)
            {
                DisableCollider(CreatePrimitive("Mk4 Left Missile Rack", PrimitiveType.Cube, parent,
                    new Vector3(-widthSpread * 0.85f, heightBase + 0.58f, -0.12f), new Vector3(0.36f, 0.44f, 0.54f), darkMetalMat,
                    Quaternion.Euler(-12f, 0f, 0f)));
                DisableCollider(CreatePrimitive("Mk4 Right Missile Rack", PrimitiveType.Cube, parent,
                    new Vector3(widthSpread * 0.85f, heightBase + 0.58f, -0.12f), new Vector3(0.36f, 0.44f, 0.54f), darkMetalMat,
                    Quaternion.Euler(-12f, 0f, 0f)));
                DisableCollider(CreatePrimitive("Mk4 Front Ram Wedge", PrimitiveType.Cube, parent,
                    new Vector3(0f, 0.24f, 0.88f), new Vector3(widthSpread * 1.35f, 0.26f, 0.32f), glowMat,
                    Quaternion.Euler(22f, 0f, 0f)));
            }

            // Stage 5 (Omega Apex): Floating Omega Crown Ring & Dual Ion Pylons
            if (levelNumber >= 5)
            {
                GameObject omegaCrown = CreateTorus("Mk5 Omega Crown", parent, widthSpread * 0.78f, 0.075f, 20, glowMat);
                omegaCrown.transform.localPosition = new Vector3(0f, heightBase + 1.18f, 0f);
                omegaCrown.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);

                DisableCollider(CreatePrimitive("Mk5 Left Ion Pylon", PrimitiveType.Capsule, parent,
                    new Vector3(-widthSpread * 0.95f, heightBase + 0.72f, -0.35f), new Vector3(0.24f, 0.65f, 0.24f), glowMat,
                    Quaternion.Euler(-20f, 0f, 18f)));
                DisableCollider(CreatePrimitive("Mk5 Right Ion Pylon", PrimitiveType.Capsule, parent,
                    new Vector3(widthSpread * 0.95f, heightBase + 0.72f, -0.35f), new Vector3(0.24f, 0.65f, 0.24f), glowMat,
                    Quaternion.Euler(-20f, 0f, -18f)));
            }
        }

        private void CreateBuildNodes(Material material)
        {
            const int nodeCount = 12;
            for (int i = 0; i < nodeCount; i++)
            {
                bool innerRing = i < 6;
                int ringIndex = innerRing ? i : i - 6;
                float radius = innerRing ? 10.5f : 18.5f;
                float angle = (ringIndex + (innerRing ? 0f : 0.5f)) * Mathf.PI * 2f / 6f;
                Vector3 position = new Vector3(Mathf.Sin(angle) * radius, 0.25f, Mathf.Cos(angle) * radius);
                GameObject node = new GameObject($"BuildNode_{i + 1:00}");
                node.transform.SetParent(runtimeRoot.transform, false);
                node.transform.position = position;
                BuildNode buildNode = node.AddComponent<BuildNode>();
                buildNode.ConfigureRuntime($"BuildNode_{i + 1:00}");
                BoxCollider collider = node.AddComponent<BoxCollider>();
                collider.size = new Vector3(4.2f, 2.0f, 4.2f);
                collider.isTrigger = true;
                GameObject socket = CreatePrimitive("Build Socket", PrimitiveType.Cylinder, node.transform,
                    Vector3.zero, new Vector3(2.2f, 0.08f, 2.2f), material);
                socket.GetComponent<Collider>().enabled = false;
                GameObject ring = CreateTorus("Build Node Ring", node.transform, 1.55f, 0.06f, 24, material);
                ring.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            }
        }

        private void CreateSatelliteTerminals(Material material)
        {
            Material terminalMat = CreateMaterial(new Color(1f, 0.55f, 0.1f), new Color(1.4f, 0.6f, 0.1f));
            CreateTerminal("Terminal_North", new Vector3(0f, 0f, 23f), terminalMat);
            CreateTerminal("Terminal_West", new Vector3(-23f, 0f, 0f), terminalMat);
            CreateTerminal("Terminal_East", new Vector3(23f, 0f, 0f), terminalMat);
        }

        private void CreateTerminal(string terminalId, Vector3 position, Material material)
        {
            GameObject terminal = new GameObject(terminalId);
            terminal.transform.SetParent(runtimeRoot.transform, false);
            terminal.transform.position = position;
            GameObject console = CreatePrimitive("Terminal Console", PrimitiveType.Cube, terminal.transform,
                new Vector3(0f, 1.25f, 0f), new Vector3(1.5f, 2.5f, 1.2f), material);
            GameObject signalRing = CreateTorus("Signal Ring", terminal.transform, 5.5f, 0.08f, 48, material);
            signalRing.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            SphereCollider collider = terminal.AddComponent<SphereCollider>();
            collider.radius = 3.5f;
            collider.isTrigger = true;
            SatelliteTerminalObjective objective = terminal.AddComponent<SatelliteTerminalObjective>();
            Light signalLight = terminal.AddComponent<Light>();
            signalLight.type = LightType.Point;
            signalLight.color = new Color(1f, 0.35f, 0.08f);
            signalLight.range = 8f;
            signalLight.intensity = 1.4f;
            objective.ConfigureRuntime(terminalId);
        }

        private Transform[] CreateSpawnPoints(Material material)
        {
            Vector3[] positions =
            {
                new Vector3(0f, 1f, 45f),
                new Vector3(45f, 1f, 0f),
                new Vector3(0f, 1f, -45f),
                new Vector3(-45f, 1f, 0f)
            };
            Transform[] points = new Transform[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject point = new GameObject($"Enemy Spawn {i + 1}");
                point.transform.SetParent(runtimeRoot.transform, false);
                point.transform.position = positions[i];
                point.transform.rotation = Quaternion.LookRotation(-positions[i].normalized, Vector3.up);
                points[i] = point.transform;
                GameObject beacon = CreatePrimitive("Spawn Beacon", PrimitiveType.Cylinder, point.transform,
                    Vector3.zero, new Vector3(1.6f, 0.08f, 1.6f), material);
                Collider beaconCollider = beacon.GetComponent<Collider>();
                if (beaconCollider != null)
                    beaconCollider.enabled = false;
            }
            return points;
        }

        private Camera EnsureMainCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                camera = cameraObject.GetComponent<Camera>();
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.035f, 0.075f, 1f);
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            return camera;
        }

        private GameObject GetOrCreateTemplatesRoot()
        {
            Transform existing = runtimeRoot.transform.Find("Runtime Prefab Templates");
            if (existing != null)
                return existing.gameObject;
            GameObject templates = new GameObject("Runtime Prefab Templates");
            templates.transform.SetParent(runtimeRoot.transform, false);
            return templates;
        }

        private GameObject CreatePrimitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Quaternion? localRotation = null)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            if (localRotation.HasValue)
                primitive.transform.localRotation = localRotation.Value;
            SetMaterial(primitive, material);
            return primitive;
        }

        private GameObject CreateTorus(string name, Transform parent, float radius, float tubeRadius, int segments, Material material)
        {
            GameObject ring = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            ring.transform.SetParent(parent, false);
            Mesh mesh = new Mesh { name = name + " Mesh" };
            Vector3[] vertices = new Vector3[segments * 8];
            int[] triangles = new int[segments * 24];
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                float nextAngle = (segment + 1) * Mathf.PI * 2f / segments;
                Vector3 center = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Vector3 nextCenter = new Vector3(Mathf.Cos(nextAngle) * radius, 0f, Mathf.Sin(nextAngle) * radius);
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 nextRadial = new Vector3(Mathf.Cos(nextAngle), 0f, Mathf.Sin(nextAngle));
                Vector3[] offsets = { Vector3.up * tubeRadius, -Vector3.up * tubeRadius, radial * tubeRadius, -radial * tubeRadius };
                Vector3[] nextOffsets = { Vector3.up * tubeRadius, -Vector3.up * tubeRadius, nextRadial * tubeRadius, -nextRadial * tubeRadius };
                int vertex = segment * 8;
                for (int i = 0; i < 4; i++)
                {
                    vertices[vertex + i] = center + offsets[i];
                    vertices[vertex + 4 + i] = nextCenter + nextOffsets[i];
                }
                int tri = segment * 24;
                for (int side = 0; side < 4; side++)
                {
                    int current = vertex + side;
                    int currentNext = vertex + ((side + 1) % 4);
                    int next = vertex + 4 + side;
                    int nextNext = vertex + 4 + ((side + 1) % 4);
                    triangles[tri++] = current; triangles[tri++] = next; triangles[tri++] = currentNext;
                    triangles[tri++] = currentNext; triangles[tri++] = next; triangles[tri++] = nextNext;
                }
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            runtimeMeshes.Add(mesh);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            ring.GetComponent<MeshFilter>().sharedMesh = mesh;
            ring.GetComponent<MeshRenderer>().sharedMaterial = material;
            return ring;
        }

        private Material CreateMaterial(Color color, Color? emission = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            Material material = new Material(shader);
            material.color = color;
            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }
            runtimeMaterials.Add(material);
            return material;
        }

        private static void SetMaterial(GameObject target, Material material)
        {
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        private static Light FindDirectionalLight()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                    return lights[i];
            }

            GameObject lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light directional = lightObject.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 1.1f;
            return directional;
        }
    }
}
