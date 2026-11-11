using System.Collections.Generic;
using UnityEngine;
using AegisProtocol.AI;
using AegisProtocol.Combat;
using AegisProtocol.Core;

namespace AegisProtocol.Systems
{
    /// <summary>Runs one laser turret, EMP slow tower, or limited-capacity repair station.</summary>
    [DisallowMultipleComponent]
    public sealed class DefenseTowerController : MonoBehaviour
    {
        private readonly Collider[] targetBuffer = new Collider[128];
        private readonly HashSet<EnemyAIController> affectedEnemies = new HashSet<EnemyAIController>();
        private DefenseStructureConfig config;
        private BuildNode buildNode;
        [SerializeField] private Transform turretHead = null;
        private HealthComponent mechHealth;
        private HealthComponent reactorHealth;
        private GameManager gameManager;
        private float fireRateMultiplier = 1f;
        private float attackTimer;
        private float pulseTimer;
        private float remainingRepairCapacity;
        private int lastRepairWave;
        private bool ownsRuntimeConfig;
        private LineRenderer towerBeamRenderer;
        private float beamDisplayTimer;
        private Transform empPulseRing;
        private float empRingAnimTimer;

        public DefenseStructureType StructureType => config != null ? config.StructureType : DefenseStructureType.AutoLaserTurret;
        public float FireRateMultiplier => fireRateMultiplier;
        public float RemainingRepairCapacity => remainingRepairCapacity;
        public DefenseStructureConfig Config => config;

        private void Start()
        {
            gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.OnGameStateChanged += HandleGameStateChanged;
                ResetRepairCapacityForWave(gameManager.CurrentWave);
            }
            ResolveRepairTargets();
        }

        private void OnDisable()
        {
            if (gameManager != null)
                gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void OnDestroy()
        {
            if (buildNode != null)
                buildNode.Release(this);
            if (ownsRuntimeConfig && config != null)
                Destroy(config);
        }

        private void Update()
        {
            if (config == null)
                return;

            float deltaTime = Time.deltaTime;
            if (beamDisplayTimer > 0f)
            {
                beamDisplayTimer -= deltaTime;
                if (beamDisplayTimer <= 0f && towerBeamRenderer != null)
                    towerBeamRenderer.enabled = false;
            }

            switch (config.StructureType)
            {
                case DefenseStructureType.AutoLaserTurret:
                    UpdateLaserTurret(deltaTime);
                    break;
                case DefenseStructureType.EmpSlowTower:
                    if (turretHead != null)
                        turretHead.Rotate(Vector3.up, 75f * deltaTime, Space.Self);
                    if (empRingAnimTimer > 0f && empPulseRing != null)
                    {
                        empRingAnimTimer = Mathf.Max(0f, empRingAnimTimer - deltaTime);
                        float t = 1f - (empRingAnimTimer / 0.45f);
                        float r = Mathf.Lerp(1.2f, config.EmpRadius * 1.65f, t);
                        empPulseRing.localScale = new Vector3(r, 0.04f, r);
                        empPulseRing.gameObject.SetActive(empRingAnimTimer > 0.01f);
                    }
                    UpdateEmpTower(deltaTime);
                    break;
                case DefenseStructureType.RepairStation:
                    if (turretHead != null)
                        turretHead.Rotate(Vector3.up, 55f * deltaTime, Space.Self);
                    UpdateRepairStation(deltaTime);
                    break;
            }
        }

        /// <summary>Configures a placed instance and creates a primitive visual if the prefab is bare.</summary>
        public void Initialize(
            DefenseStructureConfig structureConfig,
            BuildNode node,
            float inheritedFireRateMultiplier = 1f,
            bool ownsConfig = false)
        {
            config = structureConfig;
            buildNode = node;
            ownsRuntimeConfig = ownsConfig;
            fireRateMultiplier = Mathf.Max(0.1f, inheritedFireRateMultiplier);
            remainingRepairCapacity = config != null ? config.RepairCapacityPerWave : 0f;
            EnsureVisualHierarchy();
            ResolveRepairTargets();
        }

        /// <summary>Applies a cumulative rate upgrade to laser turrets.</summary>
        public void ApplyFireRateMultiplier(float multiplier)
        {
            if (StructureType != DefenseStructureType.AutoLaserTurret || multiplier <= 0f ||
                float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            {
                return;
            }

            fireRateMultiplier = Mathf.Clamp(fireRateMultiplier * multiplier, 0.1f, 5f);
        }

        private void UpdateLaserTurret(float deltaTime)
        {
            attackTimer -= deltaTime;
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                config.Range,
                targetBuffer,
                ~0,
                QueryTriggerInteraction.Collide);

            HealthComponent nearestTarget = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider hit = targetBuffer[i];
                HealthComponent candidate = hit != null ? hit.GetComponentInParent<HealthComponent>() : null;
                if (candidate == null || candidate.IsDead ||
                    (candidate.Role != HealthRole.Enemy && candidate.Role != HealthRole.Boss))
                {
                    continue;
                }

                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestTarget = candidate;
                }
            }

            if (nearestTarget == null)
                return;

            Vector3 aimDirection = nearestTarget.transform.position - transform.position;
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
                Transform rotateTarget = turretHead != null ? turretHead : transform;
                rotateTarget.rotation = Quaternion.Slerp(rotateTarget.rotation, targetRotation, 8f * deltaTime);
            }

            float shotInterval = 1f / (config.ShotsPerSecond * fireRateMultiplier);
            if (attackTimer > 0f || !IsGameplayActive())
                return;

            GameObject projectilePrefab = config.ProjectilePrefab;
            if (projectilePrefab != null)
            {
                Vector3 origin = turretHead != null ? turretHead.position : transform.position + Vector3.up;
                Quaternion rotation = Quaternion.LookRotation(nearestTarget.transform.position - origin, Vector3.up);
                GameObject instance = Instantiate(projectilePrefab, origin, rotation);
                ProjectileController projectile = instance.GetComponent<ProjectileController>();
                if (projectile == null)
                    projectile = instance.AddComponent<ProjectileController>();
                projectile.Initialize(new ProjectileLaunchSettings
                {
                    IsMissile = false,
                    Damage = config.DamagePerShot,
                    Speed = 24f,
                    MaxLifetime = 2f,
                    TargetPoint = nearestTarget.transform.position,
                    Target = nearestTarget.transform,
                    Instigator = gameObject,
                    InstigatorRole = HealthRole.PlayerMech
                });
            }
            else
            {
                nearestTarget.TakeDamage(config.DamagePerShot, gameObject);
            }

            Vector3 beamStart = turretHead != null
                ? turretHead.position + turretHead.forward * 0.85f
                : transform.position + Vector3.up * 1.65f;
            Vector3 beamEnd = nearestTarget.transform.position + Vector3.up * 0.45f;
            ShowTowerBeam(beamStart, beamEnd, new Color(0.15f, 0.95f, 1f, 1f), 0.14f);

            attackTimer = shotInterval;
        }

        private void UpdateEmpTower(float deltaTime)
        {
            if (!IsGameplayActive())
                return;

            pulseTimer -= deltaTime;
            if (pulseTimer > 0f)
                return;

            pulseTimer = config.EmpPulseInterval;
            empRingAnimTimer = 0.45f;
            if (empPulseRing != null)
                empPulseRing.gameObject.SetActive(true);

            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                config.EmpRadius,
                targetBuffer,
                ~0,
                QueryTriggerInteraction.Collide);
            affectedEnemies.Clear();
            for (int i = 0; i < count; i++)
            {
                EnemyAIController enemy = targetBuffer[i] != null
                    ? targetBuffer[i].GetComponentInParent<EnemyAIController>()
                    : null;
                if (enemy != null && affectedEnemies.Add(enemy))
                    enemy.ApplySlowModifier(config.EmpSpeedMultiplier, config.EmpSlowDuration);
            }
        }

        private void UpdateRepairStation(float deltaTime)
        {
            if (!IsGameplayActive() || remainingRepairCapacity <= 0f)
                return;

            ResolveRepairTargets();
            float budget = Mathf.Min(config.RepairPerSecond * deltaTime, remainingRepairCapacity);
            float appliedMech = HealTargetIfInRange(mechHealth, budget);
            remainingRepairCapacity -= appliedMech;
            float remainingBudget = Mathf.Min(config.RepairPerSecond * deltaTime, remainingRepairCapacity);
            float appliedReactor = HealTargetIfInRange(reactorHealth, remainingBudget);
            remainingRepairCapacity -= appliedReactor;
            remainingRepairCapacity = Mathf.Max(0f, remainingRepairCapacity);

            if (appliedMech > 0f && mechHealth != null)
            {
                ShowTowerBeam(transform.position + Vector3.up * 1.85f,
                    mechHealth.transform.position + Vector3.up * 1.0f,
                    new Color(0.2f, 1f, 0.45f, 0.95f), 0.12f);
            }
            else if (appliedReactor > 0f && reactorHealth != null)
            {
                ShowTowerBeam(transform.position + Vector3.up * 1.85f,
                    reactorHealth.transform.position + Vector3.up * 2.2f,
                    new Color(0.2f, 1f, 0.45f, 0.95f), 0.12f);
            }
        }

        private void ShowTowerBeam(Vector3 start, Vector3 end, Color beamColor, float duration)
        {
            if (!Application.isPlaying)
                return;

            if (towerBeamRenderer == null)
            {
                GameObject beamObj = new GameObject("TowerBeamEffect");
                beamObj.transform.SetParent(transform, false);
                towerBeamRenderer = beamObj.AddComponent<LineRenderer>();
                towerBeamRenderer.positionCount = 2;
                towerBeamRenderer.useWorldSpace = true;
                towerBeamRenderer.startWidth = 0.16f;
                towerBeamRenderer.endWidth = 0.08f;
                Shader shader = Shader.Find("Sprites/Default");
                if (shader != null)
                    towerBeamRenderer.material = new Material(shader);
            }

            towerBeamRenderer.startColor = beamColor;
            towerBeamRenderer.endColor = beamColor;
            towerBeamRenderer.SetPosition(0, start);
            towerBeamRenderer.SetPosition(1, end);
            towerBeamRenderer.enabled = true;
            beamDisplayTimer = duration;
        }

        private float HealTargetIfInRange(HealthComponent target, float amount)
        {
            if (target == null || target.IsDead || amount <= 0f ||
                Vector3.Distance(transform.position, target.transform.position) > config.Range)
            {
                return 0f;
            }

            float before = target.CurrentHealth;
            target.Heal(amount);
            return target.CurrentHealth - before;
        }

        private void ResolveRepairTargets()
        {
            if (mechHealth == null || mechHealth.IsDead)
                mechHealth = FindHealthRole(HealthRole.PlayerMech);
            if (reactorHealth == null || reactorHealth.IsDead)
                reactorHealth = FindHealthRole(HealthRole.Reactor);
        }

        private void HandleGameStateChanged(GameStateChangedEvent change)
        {
            if (change.CurrentState == GameState.WaveActive && gameManager != null &&
                lastRepairWave != gameManager.CurrentWave)
            {
                ResetRepairCapacityForWave(gameManager.CurrentWave);
            }
        }

        private void ResetRepairCapacityForWave(int wave)
        {
            if (lastRepairWave == wave || config == null)
                return;
            lastRepairWave = wave;
            remainingRepairCapacity = config.RepairCapacityPerWave;
        }

        private bool IsGameplayActive()
        {
            return gameManager == null || gameManager.CurrentState == GameState.WaveActive;
        }

        private void EnsureVisualHierarchy()
        {
            Renderer[] existing = GetComponentsInChildren<Renderer>();
            if (existing.Length > 0)
            {
                if (turretHead == null)
                    turretHead = transform.childCount > 0 ? transform.GetChild(transform.childCount - 1) : transform;
                return;
            }

            Color primaryColor = config != null ? config.VisualColor : new Color(0.15f, 0.85f, 1f);
            Color darkMetal = new Color(0.16f, 0.22f, 0.30f);
            Color brightGlow = primaryColor * 1.35f;

            CreateTowerPart("StructureBase", PrimitiveType.Cylinder, transform,
                new Vector3(0f, 0.18f, 0f), new Vector3(1.95f, 0.22f, 1.95f), darkMetal);
            CreateTowerPart("BaseRim", PrimitiveType.Cylinder, transform,
                new Vector3(0f, 0.38f, 0f), new Vector3(1.45f, 0.14f, 1.45f), primaryColor * 0.85f);
            CreateTowerPart("StructureColumn", PrimitiveType.Cylinder, transform,
                new Vector3(0f, 0.92f, 0f), new Vector3(0.68f, 0.65f, 0.68f), darkMetal);

            GameObject headPivot = new GameObject("StructureHead");
            headPivot.transform.SetParent(transform, false);
            headPivot.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            turretHead = headPivot.transform;

            switch (StructureType)
            {
                case DefenseStructureType.AutoLaserTurret:
                {
                    CreateTowerPart("TurretHousing", PrimitiveType.Cube, turretHead,
                        Vector3.zero, new Vector3(0.95f, 0.58f, 1.15f), primaryColor);
                    CreateTowerPart("TurretDome", PrimitiveType.Sphere, turretHead,
                        new Vector3(0f, 0.28f, -0.05f), new Vector3(0.76f, 0.42f, 0.82f), darkMetal);
                    CreateTowerPart("LeftLaserBarrel", PrimitiveType.Cylinder, turretHead,
                        new Vector3(-0.26f, 0.02f, 0.78f), new Vector3(0.16f, 0.58f, 0.16f), darkMetal,
                        Quaternion.Euler(90f, 0f, 0f));
                    CreateTowerPart("RightLaserBarrel", PrimitiveType.Cylinder, turretHead,
                        new Vector3(0.26f, 0.02f, 0.78f), new Vector3(0.16f, 0.58f, 0.16f), darkMetal,
                        Quaternion.Euler(90f, 0f, 0f));
                    CreateTowerPart("LaserLens", PrimitiveType.Sphere, turretHead,
                        new Vector3(0f, 0.06f, 0.58f), new Vector3(0.32f, 0.24f, 0.22f), brightGlow);
                    break;
                }

                case DefenseStructureType.EmpSlowTower:
                {
                    for (int r = 0; r < 3; r++)
                    {
                        float y = -0.45f + r * 0.28f;
                        float scaleXZ = 1.25f - r * 0.16f;
                        CreateTowerPart($"TeslaCoilRing_{r + 1}", PrimitiveType.Cylinder, turretHead,
                            new Vector3(0f, y, 0f), new Vector3(scaleXZ, 0.06f, scaleXZ), primaryColor);
                    }
                    CreateTowerPart("EmpOrbCore", PrimitiveType.Sphere, turretHead,
                        new Vector3(0f, 0.36f, 0f), new Vector3(0.78f, 0.78f, 0.78f), brightGlow);
                    CreateTowerPart("EmpProngX", PrimitiveType.Cube, turretHead,
                        new Vector3(0f, 0.36f, 0f), new Vector3(1.35f, 0.12f, 0.14f), darkMetal);
                    CreateTowerPart("EmpProngZ", PrimitiveType.Cube, turretHead,
                        new Vector3(0f, 0.36f, 0f), new Vector3(0.14f, 0.12f, 1.35f), darkMetal);
                    GameObject pulseDisc = CreateTowerPart("EmpShockwaveDisc", PrimitiveType.Cylinder, transform,
                        new Vector3(0f, 0.25f, 0f), new Vector3(1.5f, 0.04f, 1.5f), brightGlow);
                    empPulseRing = pulseDisc.transform;
                    empPulseRing.gameObject.SetActive(false);
                    break;
                }

                case DefenseStructureType.RepairStation:
                {
                    CreateTowerPart("NanoHub", PrimitiveType.Cylinder, turretHead,
                        new Vector3(0f, -0.15f, 0f), new Vector3(1.05f, 0.24f, 1.05f), darkMetal);
                    CreateTowerPart("MedCrossH", PrimitiveType.Cube, turretHead,
                        new Vector3(0f, 0.38f, 0f), new Vector3(1.15f, 0.30f, 0.30f), brightGlow);
                    CreateTowerPart("MedCrossV", PrimitiveType.Cube, turretHead,
                        new Vector3(0f, 0.38f, 0f), new Vector3(0.30f, 1.15f, 0.30f), brightGlow);
                    break;
                }
            }
        }

        private GameObject CreateTowerPart(
            string partName,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Color color,
            Quaternion? localRot = null)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            if (localRot.HasValue)
                part.transform.localRotation = localRot.Value;
            ApplyPartColor(part, color);
            DisableCollider(part);
            return part;
        }

        private void ApplyPartColor(GameObject part, Color color)
        {
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;
        }

        private static void DisableCollider(GameObject part)
        {
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                collider.enabled = false;
        }

        private static HealthComponent FindHealthRole(HealthRole role)
        {
            HealthComponent[] components = FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].Role == role && !components[i].IsDead)
                    return components[i];
            }
            return null;
        }
    }
}
