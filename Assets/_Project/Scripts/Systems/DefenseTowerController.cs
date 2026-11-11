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
            switch (config.StructureType)
            {
                case DefenseStructureType.AutoLaserTurret:
                    UpdateLaserTurret(deltaTime);
                    break;
                case DefenseStructureType.EmpSlowTower:
                    UpdateEmpTower(deltaTime);
                    break;
                case DefenseStructureType.RepairStation:
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
            float applied = HealTargetIfInRange(mechHealth, budget);
            remainingRepairCapacity -= applied;
            float remainingBudget = Mathf.Min(config.RepairPerSecond * deltaTime, remainingRepairCapacity);
            applied = HealTargetIfInRange(reactorHealth, remainingBudget);
            remainingRepairCapacity -= applied;
            remainingRepairCapacity = Mathf.Max(0f, remainingRepairCapacity);
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

            Color primaryColor = config != null ? config.VisualColor : new Color(0.15f, 0.9f, 1f);
            Color darkMetal = new Color(0.16f, 0.22f, 0.30f);

            // Armored Pedestal Base
            CreateTowerPart("PedestalBase", PrimitiveType.Cylinder, transform,
                new Vector3(0f, 0.16f, 0f), new Vector3(2.1f, 0.16f, 2.1f), darkMetal);
            CreateTowerPart("PedestalRim", PrimitiveType.Cylinder, transform,
                new Vector3(0f, 0.36f, 0f), new Vector3(1.55f, 0.12f, 1.55f), primaryColor * 0.85f);

            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                Vector3 pos = rot * new Vector3(0f, 0.32f, 0.85f);
                CreateTowerPart($"BaseStrut_{i}", PrimitiveType.Cube, transform,
                    pos, new Vector3(0.28f, 0.32f, 0.55f), darkMetal, rot);
            }

            DefenseStructureType type = config != null ? config.StructureType : DefenseStructureType.AutoLaserTurret;
            switch (type)
            {
                case DefenseStructureType.AutoLaserTurret:
                {
                    CreateTowerPart("LaserColumn", PrimitiveType.Cylinder, transform,
                        new Vector3(0f, 0.95f, 0f), new Vector3(0.62f, 0.58f, 0.62f), darkMetal);

                    GameObject headPivot = new GameObject("LaserTurretHead");
                    headPivot.transform.SetParent(transform, false);
                    headPivot.transform.localPosition = new Vector3(0f, 1.62f, 0f);
                    turretHead = headPivot.transform;

                    CreateTowerPart("TurretHousing", PrimitiveType.Cube, turretHead,
                        Vector3.zero, new Vector3(1.05f, 0.58f, 1.25f), darkMetal);
                    CreateTowerPart("TurretCoreDome", PrimitiveType.Sphere, turretHead,
                        new Vector3(0f, 0.25f, -0.08f), new Vector3(0.75f, 0.45f, 0.85f), primaryColor);
                    CreateTowerPart("LeftLaserBarrel", PrimitiveType.Cylinder, turretHead,
                        new Vector3(-0.28f, 0.04f, 0.88f), new Vector3(0.16f, 0.65f, 0.16f), primaryColor,
                        Quaternion.Euler(90f, 0f, 0f));
                    CreateTowerPart("RightLaserBarrel", PrimitiveType.Cylinder, turretHead,
                        new Vector3(0.28f, 0.04f, 0.88f), new Vector3(0.16f, 0.65f, 0.16f), primaryColor,
                        Quaternion.Euler(90f, 0f, 0f));
                    break;
                }

                case DefenseStructureType.EmpSlowTower:
                {
                    CreateTowerPart("TeslaSpire", PrimitiveType.Cylinder, transform,
                        new Vector3(0f, 1.25f, 0f), new Vector3(0.45f, 0.95f, 0.45f), darkMetal);
                    CreateTowerPart("TeslaCoilRing1", PrimitiveType.Cylinder, transform,
                        new Vector3(0f, 0.85f, 0f), new Vector3(1.35f, 0.07f, 1.35f), primaryColor);
                    CreateTowerPart("TeslaCoilRing2", PrimitiveType.Cylinder, transform,
                        new Vector3(0f, 1.35f, 0f), new Vector3(1.10f, 0.07f, 1.10f), primaryColor);
                    CreateTowerPart("TeslaCoilRing3", PrimitiveType.Cylinder, transform,
                        new Vector3(0f, 1.82f, 0f), new Vector3(0.85f, 0.07f, 0.85f), primaryColor);

                    GameObject orb = CreateTowerPart("EmpPlasmaOrb", PrimitiveType.Sphere, transform,
                        new Vector3(0f, 2.38f, 0f), new Vector3(0.88f, 0.88f, 0.88f), primaryColor);
                    turretHead = orb.transform;
                    break;
                }

                case DefenseStructureType.RepairStation:
                {
                    CreateTowerPart("MedColumnLeft", PrimitiveType.Cube, transform,
                        new Vector3(-0.55f, 1.05f, 0f), new Vector3(0.26f, 1.35f, 0.26f), darkMetal);
                    CreateTowerPart("MedColumnRight", PrimitiveType.Cube, transform,
                        new Vector3(0.55f, 1.05f, 0f), new Vector3(0.26f, 1.35f, 0.26f), darkMetal);

                    GameObject crossPivot = new GameObject("RepairCrossHead");
                    crossPivot.transform.SetParent(transform, false);
                    crossPivot.transform.localPosition = new Vector3(0f, 2.05f, 0f);
                    turretHead = crossPivot.transform;

                    Color healGreen = new Color(0.18f, 0.98f, 0.42f);
                    CreateTowerPart("CrossVertical", PrimitiveType.Cube, turretHead,
                        Vector3.zero, new Vector3(0.32f, 1.05f, 0.32f), healGreen);
                    CreateTowerPart("CrossHorizontal", PrimitiveType.Cube, turretHead,
                        Vector3.zero, new Vector3(1.05f, 0.32f, 0.32f), healGreen);
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
