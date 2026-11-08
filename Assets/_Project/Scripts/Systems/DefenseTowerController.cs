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

            GameObject basePart = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basePart.name = "StructureBase";
            basePart.transform.SetParent(transform, false);
            basePart.transform.localPosition = Vector3.zero;
            basePart.transform.localScale = new Vector3(1.8f, 0.35f, 1.8f);
            ApplyPartColor(basePart, config.VisualColor);
            DisableCollider(basePart);

            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cube);
            column.name = "StructureColumn";
            column.transform.SetParent(transform, false);
            column.transform.localPosition = Vector3.up * 0.85f;
            column.transform.localScale = new Vector3(0.55f, 1.3f, 0.55f);
            ApplyPartColor(column, config.VisualColor * 0.8f);
            DisableCollider(column);

            PrimitiveType headType = config.StructureType == DefenseStructureType.AutoLaserTurret
                ? PrimitiveType.Sphere
                : PrimitiveType.Cube;
            GameObject head = GameObject.CreatePrimitive(headType);
            head.name = "StructureHead";
            head.transform.SetParent(transform, false);
            head.transform.localPosition = Vector3.up * 1.65f;
            head.transform.localScale = config.StructureType == DefenseStructureType.AutoLaserTurret
                ? new Vector3(0.9f, 0.45f, 1.4f)
                : Vector3.one * 0.9f;
            ApplyPartColor(head, config.VisualColor);
            DisableCollider(head);
            turretHead = head.transform;
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
