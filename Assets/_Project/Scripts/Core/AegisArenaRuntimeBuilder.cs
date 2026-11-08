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
            runtimeRoot = new GameObject("Aegis Runtime Arena");
            Material floorMaterial = CreateMaterial(new Color(0.025f, 0.07f, 0.12f), new Color(0.02f, 0.12f, 0.2f));
            Material gridMaterial = CreateMaterial(new Color(0.06f, 0.38f, 0.48f), new Color(0.04f, 0.5f, 0.7f));
            Material wallMaterial = CreateMaterial(new Color(0.08f, 0.15f, 0.23f));
            Material reactorMaterial = CreateMaterial(new Color(0.1f, 0.75f, 1f), new Color(0.1f, 0.8f, 2f));
            Material mechMaterial = CreateMaterial(new Color(0.15f, 0.35f, 0.48f), new Color(0.02f, 0.1f, 0.18f));
            Material accentMaterial = CreateMaterial(new Color(0.1f, 0.9f, 1f), new Color(0.1f, 1.4f, 2f));

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
            mechController.ConfigureRuntimeReferences(mech.Find("Chassis"), mech.Find("Chassis/Turret"), mainCamera);
            weaponSystem.ConfigureRuntimeReferences(
                mech.Find("Chassis/Turret/Plasma Muzzle"), plasmaTemplate, missileTemplate, cameraRig);

            CreateBuildNodes(reactorMaterial);
            CreateSatelliteTerminals(accentMaterial);
            Transform[] spawnPoints = CreateSpawnPoints(accentMaterial);
            EnemyPrefabDefinition[] enemyDefinitions = CreateEnemyTemplates(crystalTemplate, accentMaterial);

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
            reactorObject.transform.position = new Vector3(0f, 0.6f, 0f);
            health = reactorObject.AddComponent<HealthComponent>();
            health.Configure(HealthRole.Reactor, HealthComponent.ReactorDefaultMaxHealth, true);

            CreatePrimitive("Reactor Housing", PrimitiveType.Cylinder, reactorObject.transform,
                new Vector3(0f, 0.6f, 0f), new Vector3(5f, 1.2f, 5f), reactorMaterial);
            CreatePrimitive("Quantum Core", PrimitiveType.Sphere, reactorObject.transform,
                new Vector3(0f, 2.6f, 0f), new Vector3(2.2f, 2.2f, 2.2f), reactorMaterial);
            reactorRingA = CreateTorus("Reactor Energy Ring A", reactorObject.transform, 4.8f, 0.16f, 48, reactorMaterial).transform;
            reactorRingA.localPosition = new Vector3(0f, 1.9f, 0f);
            reactorRingA.localRotation = Quaternion.Euler(20f, 0f, 0f);
            reactorRingB = CreateTorus("Reactor Energy Ring B", reactorObject.transform, 5.3f, 0.12f, 48, reactorMaterial).transform;
            reactorRingB.localPosition = new Vector3(0f, 1.9f, 0f);
            reactorRingB.localRotation = Quaternion.Euler(-18f, 42f, 0f);

            GameObject lightObject = new GameObject("Reactor Core Light");
            lightObject.transform.SetParent(reactorObject.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 3f, 0f);
            Light coreLight = lightObject.AddComponent<Light>();
            coreLight.type = LightType.Point;
            coreLight.color = new Color(0.1f, 0.75f, 1f);
            coreLight.intensity = 5f;
            coreLight.range = 16f;
            return reactorObject.transform;
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
            mech.transform.position = new Vector3(0f, 1.2f, -27f);
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
            CreatePrimitive("Lower Chassis", PrimitiveType.Cube, chassis,
                new Vector3(0f, -0.15f, 0f), new Vector3(2.4f, 0.75f, 3.2f), bodyMaterial);
            CreatePrimitive("Left Track", PrimitiveType.Cube, chassis,
                new Vector3(-1.15f, -0.35f, 0f), new Vector3(0.35f, 0.55f, 3.4f), accentMaterial);
            CreatePrimitive("Right Track", PrimitiveType.Cube, chassis,
                new Vector3(1.15f, -0.35f, 0f), new Vector3(0.35f, 0.55f, 3.4f), accentMaterial);

            Transform turret = new GameObject("Turret").transform;
            turret.SetParent(chassis, false);
            turret.localPosition = new Vector3(0f, 0.5f, 0f);
            CreatePrimitive("Turret Armor", PrimitiveType.Sphere, turret,
                new Vector3(0f, 0.1f, 0f), new Vector3(1.5f, 0.85f, 1.6f), accentMaterial);
            CreatePrimitive("Plasma Barrel", PrimitiveType.Cylinder, turret,
                new Vector3(0f, 0.15f, 1.15f), new Vector3(0.22f, 0.85f, 0.22f), bodyMaterial,
                Quaternion.Euler(90f, 0f, 0f));
            Transform muzzle = new GameObject("Plasma Muzzle").transform;
            muzzle.SetParent(turret, false);
            muzzle.localPosition = new Vector3(0f, 0.15f, 2f);
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

            crystalTemplate = CreateCrystalTemplate(accentMaterial);
            plasmaTemplate = CreateProjectileTemplate("Plasma Projectile Template", accentMaterial, 0.28f);
            missileTemplate = CreateProjectileTemplate("Missile Projectile Template", bodyMaterial, 0.48f);
            return mech.transform;
        }

        private EnergyCrystalPickup CreateCrystalTemplate(Material material)
        {
            GameObject templates = GetOrCreateTemplatesRoot();
            GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crystal.name = "Energy Crystal Pickup Template";
            crystal.transform.SetParent(templates.transform, false);
            crystal.transform.localScale = Vector3.one * 0.55f;
            SetMaterial(crystal, material);
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
            SetMaterial(projectile, material);
            ProjectileController controller = projectile.AddComponent<ProjectileController>();
            Renderer renderer = projectile.GetComponent<Renderer>();
            if (renderer != null)
                renderer.enabled = false;
            projectile.SetActive(false);
            return controller;
        }

        private EnemyPrefabDefinition[] CreateEnemyTemplates(EnergyCrystalPickup crystalPrefab, Material projectileMaterial)
        {
            GameObject templates = GetOrCreateTemplatesRoot();
            GameObject energyProjectile = CreatePrimitive("Enemy Projectile Template", PrimitiveType.Sphere,
                templates.transform, Vector3.zero, Vector3.one * 0.36f, projectileMaterial);
            ProjectileController projectileController = energyProjectile.AddComponent<ProjectileController>();
            energyProjectile.GetComponent<Renderer>().enabled = false;
            energyProjectile.SetActive(false);

            EnemyType[] types =
            {
                EnemyType.ScoutDrone,
                EnemyType.RangedStriker,
                EnemyType.KamikazeDrone,
                EnemyType.HeavySiegeMech,
                EnemyType.OverlordTitanBoss
            };
            float[] hp = { 75f, 100f, 45f, 450f, 3000f };
            float[] speed = { 4.5f, 3.2f, 6.5f, 2f, 1.7f };
            float[] range = { 1.8f, 10f, 1.2f, 2.2f, 14f };
            float[] damage = { 10f, 12f, 55f, 45f, 35f };
            float[] cooldown = { 1.5f, 1.6f, 1f, 2.5f, 1.1f };
            int[] score = { 50, 100, 75, 250, 1500 };
            int[] crystals = { 5, 8, 6, 20, 100 };
            float[] scale = { 0.9f, 1f, 0.75f, 1.7f, 3.2f };
            Color[] colors =
            {
                new Color(0.1f, 0.8f, 1f),
                new Color(1f, 0.45f, 0.1f),
                new Color(1f, 0.12f, 0.08f),
                new Color(0.3f, 0.4f, 0.5f),
                new Color(0.8f, 0.1f, 0.85f)
            };

            EnemyPrefabDefinition[] definitions = new EnemyPrefabDefinition[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                EnemyArchetypeConfig config = ScriptableObject.CreateInstance<EnemyArchetypeConfig>();
                config.name = types[i] + " Runtime Config";
                runtimeConfigs.Add(config);
                config.Configure(types[i], hp[i], speed[i], range[i], damage[i], cooldown[i], score[i], crystals[i], scale[i], colors[i]);
                if (types[i] == EnemyType.RangedStriker || types[i] == EnemyType.OverlordTitanBoss)
                    config.SetRuntimeProjectilePrefab(energyProjectile);

                GameObject enemy = new GameObject(types[i] + " Enemy Template");
                enemy.SetActive(false);
                enemy.transform.SetParent(templates.transform, false);
                SphereCollider collider = enemy.AddComponent<SphereCollider>();
                collider.radius = 0.8f;
                enemy.AddComponent<HealthComponent>();
                EnemyAIController ai = enemy.AddComponent<EnemyAIController>();
                ai.ConfigureRuntime(config, crystalPrefab);
                GameObject visual = GameObject.CreatePrimitive(
                    types[i] == EnemyType.HeavySiegeMech || types[i] == EnemyType.OverlordTitanBoss
                        ? PrimitiveType.Cube
                        : PrimitiveType.Sphere);
                visual.name = "Archetype Visual";
                visual.transform.SetParent(enemy.transform, false);
                visual.transform.localPosition = Vector3.up * 0.45f;
                visual.transform.localScale = Vector3.one * (types[i] == EnemyType.OverlordTitanBoss ? 1.1f : 1f);
                SetMaterial(visual, CreateMaterial(colors[i], colors[i] * 1.2f));
                Collider visualCollider = visual.GetComponent<Collider>();
                if (visualCollider != null)
                    visualCollider.enabled = false;

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

        private void CreateBuildNodes(Material material)
        {
            const int nodeCount = 12;
            for (int i = 0; i < nodeCount; i++)
            {
                float angle = i * Mathf.PI * 2f / nodeCount;
                Vector3 position = new Vector3(Mathf.Sin(angle) * 24f, 0.25f, Mathf.Cos(angle) * 24f);
                GameObject node = new GameObject($"BuildNode_{i + 1:00}");
                node.transform.SetParent(runtimeRoot.transform, false);
                node.transform.position = position;
                BuildNode buildNode = node.AddComponent<BuildNode>();
                buildNode.ConfigureRuntime($"BuildNode_{i + 1:00}");
                BoxCollider collider = node.AddComponent<BoxCollider>();
                collider.size = new Vector3(3.4f, 1.5f, 3.4f);
                collider.isTrigger = true;
                CreatePrimitive("Build Socket", PrimitiveType.Cylinder, node.transform,
                    Vector3.zero, new Vector3(2f, 0.08f, 2f), material);
            }
        }

        private void CreateSatelliteTerminals(Material material)
        {
            CreateTerminal("Terminal_North", new Vector3(0f, 0f, 42f), material);
            CreateTerminal("Terminal_West", new Vector3(-42f, 0f, 0f), material);
            CreateTerminal("Terminal_South", new Vector3(0f, 0f, -42f), material);
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
