using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using AegisProtocol.Combat;
using AegisProtocol.Core;
using AegisProtocol.Systems;

namespace AegisProtocol.AI
{
    /// <summary>Targets the reactor/player and executes the configured enemy archetype behavior.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HealthComponent))]
    public sealed class EnemyAIController : MonoBehaviour
    {
        [SerializeField] private EnemyArchetypeConfig archetype = null;
        [SerializeField] private EnemyType fallbackType = EnemyType.ScoutDrone;
        [SerializeField] private Transform projectileMuzzle = null;
        [SerializeField] private EnergyCrystalPickup energyCrystalPrefab = null;
        [SerializeField] private GameObject impactEffectPrefab = null;
        [SerializeField] private LayerMask attackLayers = ~0;

        private readonly Collider[] explosionBuffer = new Collider[64];
        private readonly HashSet<HealthComponent> affectedByExplosion = new HashSet<HealthComponent>();
        private HealthComponent health;
        private HealthComponent reactor;
        private HealthComponent mech;
        private NavMeshAgent navAgent;
        private EnemyArchetypeConfig runtimeFallbackConfig;
        private float attackTimer;
        private float specialAttackTimer;
        private float slowRemaining;
        private float speedMultiplier = 1f;
        private float slowMultiplier = 1f;
        private bool isDetonating;
        private bool initialized;

        public event Action<EnemyAIController> OnEnemyDied;
        public EnemyArchetypeConfig Archetype => archetype;
        public HealthComponent Health => health;
        public bool IsDead => health == null || health.IsDead;
        public float CurrentSpeedMultiplier => speedMultiplier * slowMultiplier;

        private void Awake()
        {
            health = GetComponent<HealthComponent>();
            navAgent = GetComponent<NavMeshAgent>();
            if (archetype == null)
                CreateFallbackArchetype();
            Initialize(archetype);
        }

        private void OnEnable()
        {
            if (health != null)
                health.OnDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (health != null)
                health.OnDeath -= HandleDeath;
        }

        private void OnDestroy()
        {
            if (runtimeFallbackConfig != null)
                Destroy(runtimeFallbackConfig);
        }

        private void Update()
        {
            if (!initialized || health.IsDead)
                return;

            UpdateSlow(Time.deltaTime);
            RefreshTargets();
            HealthComponent target = SelectTarget();
            if (target == null || target.IsDead)
            {
                SetAgentStopped(true);
                return;
            }

            Vector3 targetPosition = target.transform.position;
            float distance = HorizontalDistance(transform.position, targetPosition);
            bool kamikaze = archetype.Type == EnemyType.KamikazeDrone;
            bool ranged = archetype.Type == EnemyType.RangedStriker;
            bool boss = archetype.IsBoss;
            float desiredRange = ranged || boss ? archetype.AttackRange * 0.85f : archetype.AttackRange;

            if (kamikaze && distance <= archetype.AttackRange)
            {
                Detonate(targetPosition, target);
                return;
            }

            if (distance > desiredRange)
                MoveTowards(targetPosition);
            else
                SetAgentStopped(true);

            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f && distance <= archetype.AttackRange)
            {
                if (ranged || boss)
                    FireProjectile(target);
                else
                    target.TakeDamage(archetype.AttackDamage, gameObject);
                attackTimer = archetype.AttackCooldown;
            }

        }

        /// <summary>Assigns the wave-selected configuration after instantiation.</summary>
        public void Initialize(EnemyArchetypeConfig config)
        {
            if (config == null || health == null)
                return;

            archetype = config;
            health.Configure(
                archetype.IsBoss ? HealthRole.Boss : HealthRole.Enemy,
                archetype.MaxHealth,
                refillCurrentHealth: true);
            transform.localScale = Vector3.one * archetype.Scale;
            ApplyVisualHint();
            if (navAgent != null)
            {
                navAgent.speed = archetype.MoveSpeed;
                navAgent.stoppingDistance = archetype.AttackRange * 0.8f;
                navAgent.updateRotation = true;
            }

            initialized = true;
            RefreshTargets();
        }

        /// <summary>Stacks a timed speed reduction used by EMP structures.</summary>
        public void ApplySlowModifier(float requestedSpeedMultiplier, float duration)
        {
            if (requestedSpeedMultiplier <= 0f || duration <= 0f ||
                float.IsNaN(requestedSpeedMultiplier) || float.IsInfinity(requestedSpeedMultiplier) ||
                float.IsNaN(duration) || float.IsInfinity(duration))
            {
                return;
            }

            slowMultiplier = Mathf.Min(slowMultiplier, Mathf.Clamp01(requestedSpeedMultiplier));
            slowRemaining = Mathf.Max(slowRemaining, duration);
            ApplyCurrentSpeed();
        }

        private void RefreshTargets()
        {
            if (reactor == null || reactor.IsDead)
                reactor = FindHealthRole(HealthRole.Reactor);
            if (mech == null || mech.IsDead)
                mech = FindHealthRole(HealthRole.PlayerMech);
        }

        private HealthComponent SelectTarget()
        {
            if (mech != null && !mech.IsDead &&
                HorizontalDistance(transform.position, mech.transform.position) <= archetype.ThreatDetectionRange)
            {
                return mech;
            }

            return reactor != null && !reactor.IsDead ? reactor : mech;
        }

        private void MoveTowards(Vector3 targetPosition)
        {
            SetAgentStopped(false);
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.speed = archetype.MoveSpeed * CurrentSpeedMultiplier;
                navAgent.SetDestination(targetPosition);
                return;
            }

            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            direction.Normalize();
            transform.position += direction * (archetype.MoveSpeed * CurrentSpeedMultiplier * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up),
                8f * Time.deltaTime);
        }

        private void FireProjectile(HealthComponent target)
        {
            GameObject projectilePrefab = archetype.AttackProjectilePrefab;
            Vector3 origin = projectileMuzzle != null ? projectileMuzzle.position : transform.position + Vector3.up;
            Vector3 direction = target.transform.position - origin;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = transform.forward;
            direction.Normalize();

            if (projectilePrefab == null)
            {
                target.TakeDamage(archetype.AttackDamage, gameObject);
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            GameObject projectileObject = Instantiate(projectilePrefab, origin, rotation);
            ProjectileController projectile = projectileObject.GetComponent<ProjectileController>();
            if (projectile == null)
                projectile = projectileObject.AddComponent<ProjectileController>();

            bool bossMissile = archetype.IsBoss && Time.time >= specialAttackTimer;
            projectile.Initialize(new ProjectileLaunchSettings
            {
                IsMissile = bossMissile,
                Damage = bossMissile ? archetype.AttackDamage * 2f : archetype.AttackDamage,
                Speed = archetype.RangedProjectileSpeed,
                TurnSpeed = bossMissile ? 180f : 0f,
                ExplosionRadius = archetype.ExplosionRadius,
                MaxLifetime = 5f,
                TargetSearchRadius = archetype.ThreatDetectionRange,
                TargetPoint = target.transform.position,
                Target = target.transform,
                Instigator = gameObject,
                InstigatorRole = archetype.IsBoss ? HealthRole.Boss : HealthRole.Enemy,
                ImpactEffectPrefab = impactEffectPrefab
            });

            if (bossMissile)
                specialAttackTimer = Time.time + Mathf.Max(3f, archetype.AttackCooldown * 3f);
        }

        private void Detonate(Vector3 center, HealthComponent directTarget)
        {
            if (isDetonating)
                return;

            isDetonating = true;
            int count = Physics.OverlapSphereNonAlloc(
                center,
                Mathf.Max(2f, archetype.AttackRange),
                explosionBuffer,
                attackLayers,
                QueryTriggerInteraction.Collide);
            affectedByExplosion.Clear();
            for (int i = 0; i < count; i++)
            {
                HealthComponent candidate = explosionBuffer[i] != null
                    ? explosionBuffer[i].GetComponentInParent<HealthComponent>()
                    : null;
                if (CanDamageTarget(candidate) && affectedByExplosion.Add(candidate))
                    candidate.TakeDamage(archetype.AttackDamage, gameObject);
            }

            if (CanDamageTarget(directTarget) && affectedByExplosion.Add(directTarget))
                directTarget.TakeDamage(archetype.AttackDamage, gameObject);
            health.TakeDamage(health.CurrentHealth, gameObject);
        }

        private bool CanDamageTarget(HealthComponent candidate)
        {
            return candidate != null && !candidate.IsDead && candidate != health &&
                   (candidate.Role == HealthRole.PlayerMech || candidate.Role == HealthRole.Reactor);
        }

        private void HandleDeath(HealthComponent deadHealth, GameObject instigator)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(archetype.ScoreReward);
                if (archetype.IsBoss)
                    GameManager.Instance.RegisterFinalBossDefeated();
            }

            if (energyCrystalPrefab != null && archetype.CrystalDropAmount > 0)
            {
                EnergyCrystalPickup pickup = Instantiate(
                    energyCrystalPrefab,
                    transform.position,
                    Quaternion.identity);
                pickup.Initialize(archetype.CrystalDropAmount);
            }

            OnEnemyDied?.Invoke(this);
            Destroy(gameObject);
        }

        private void UpdateSlow(float deltaTime)
        {
            if (slowRemaining <= 0f)
                return;

            slowRemaining = Mathf.Max(0f, slowRemaining - deltaTime);
            if (slowRemaining <= 0f)
            {
                slowMultiplier = 1f;
                ApplyCurrentSpeed();
            }
        }

        private void ApplyCurrentSpeed()
        {
            if (navAgent != null && navAgent.enabled)
                navAgent.speed = archetype.MoveSpeed * CurrentSpeedMultiplier;
        }

        private void SetAgentStopped(bool stopped)
        {
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
                navAgent.isStopped = stopped;
        }

        private void ApplyVisualHint()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material != null)
                    renderers[i].material.color = archetype.VisualColor;
            }
        }

        private void CreateFallbackArchetype()
        {
            runtimeFallbackConfig = ScriptableObject.CreateInstance<EnemyArchetypeConfig>();
            runtimeFallbackConfig.hideFlags = HideFlags.DontSave;
            float healthValue = 75f;
            float speedValue = 4.5f;
            float rangeValue = 1.8f;
            float damageValue = 10f;
            float cooldown = 1.5f;
            int score = 50;
            int crystals = 5;
            float scale = 1f;
            Color color = new Color(0.15f, 0.85f, 1f);

            switch (fallbackType)
            {
                case EnemyType.RangedStriker:
                    healthValue = 100f; speedValue = 3.2f; rangeValue = 10f; damageValue = 12f;
                    cooldown = 1.6f; score = 100; crystals = 8; color = new Color(1f, 0.5f, 0.1f);
                    break;
                case EnemyType.KamikazeDrone:
                    healthValue = 45f; speedValue = 6.5f; rangeValue = 1.2f; damageValue = 55f;
                    cooldown = 1f; score = 75; crystals = 6; color = Color.red;
                    break;
                case EnemyType.HeavySiegeMech:
                    healthValue = 450f; speedValue = 2f; rangeValue = 2.2f; damageValue = 45f;
                    cooldown = 2.5f; score = 250; crystals = 20; scale = 1.6f; color = Color.gray;
                    break;
                case EnemyType.OverlordTitanBoss:
                    healthValue = 3000f; speedValue = 1.7f; rangeValue = 14f; damageValue = 35f;
                    cooldown = 1.1f; score = 1500; crystals = 100; scale = 3f; color = Color.magenta;
                    break;
            }

            runtimeFallbackConfig.Configure(
                fallbackType, healthValue, speedValue, rangeValue, damageValue,
                cooldown, score, crystals, scale, color);
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

        private static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            first.y = 0f;
            second.y = 0f;
            return Vector3.Distance(first, second);
        }
    }
}
