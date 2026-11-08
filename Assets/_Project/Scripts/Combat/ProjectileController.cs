using UnityEngine;
using AegisProtocol.Core;

namespace AegisProtocol.Combat
{
    /// <summary>Settings applied when a projectile instance is initialized.</summary>
    public struct ProjectileLaunchSettings
    {
        public bool IsMissile;
        public float Damage;
        public float Speed;
        public float TurnSpeed;
        public float ExplosionRadius;
        public float MaxLifetime;
        public float TargetSearchRadius;
        public Vector3 TargetPoint;
        public Transform Target;
        public GameObject Instigator;
        public HealthRole InstigatorRole;
        public GameObject ImpactEffectPrefab;
    }

    /// <summary>Moves a plasma bolt or guided missile and applies its impact damage.</summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileController : MonoBehaviour
    {
        private const int InitialOverlapCapacity = 32;
        private const int MaximumOverlapCapacity = 512;

        [SerializeField] private bool isMissile;
        [SerializeField, Min(0f)] private float damage = 25f;
        [SerializeField, Min(0f)] private float speed = 28f;
        [SerializeField, Min(0f)] private float turnSpeed = 240f;
        [SerializeField, Min(0.1f)] private float explosionRadius = 4f;
        [SerializeField, Min(0.01f)] private float maxLifetime = 3f;
        [SerializeField, Min(0f)] private float targetSearchRadius = 35f;
        [SerializeField] private Vector3 targetPoint;
        [SerializeField] private Transform target;
        [SerializeField] private GameObject instigator;
        [SerializeField] private HealthRole instigatorRole = HealthRole.PlayerMech;
        [SerializeField] private GameObject impactEffectPrefab;
        [SerializeField] private LayerMask damageLayers = ~0;

        private Rigidbody body;
        private Collider projectileCollider;
        private Collider[] overlapBuffer = new Collider[InitialOverlapCapacity];
        private float lifetimeRemaining;
        private bool initialized;
        private bool hasImpacted;

        public bool IsMissile => isMissile;
        public float Damage => damage;
        public float LifetimeRemaining => lifetimeRemaining;
        public Transform Target => target;

        private void Awake()
        {
            EnsurePhysicsComponents();
            lifetimeRemaining = Mathf.Max(0.01f, maxLifetime);
        }

        private void Update()
        {
            if (hasImpacted)
                return;

            lifetimeRemaining -= Time.deltaTime;
            if (lifetimeRemaining <= 0f)
            {
                Impact(transform.position, null);
                return;
            }

            if (isMissile)
                UpdateMissileHeading(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (body == null || hasImpacted)
                return;

            body.linearVelocity = transform.forward * speed;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasImpacted || other == null || IsOwnedByInstigator(other.transform))
                return;

            HealthComponent health = other.GetComponentInParent<HealthComponent>();
            if (health != null && !CanDamage(health))
                return;

            Impact(other.ClosestPoint(transform.position), health);
        }

        /// <summary>Initializes this instance immediately after Instantiate.</summary>
        public void Initialize(ProjectileLaunchSettings settings)
        {
            isMissile = settings.IsMissile;
            damage = Mathf.Max(0f, settings.Damage);
            speed = Mathf.Max(0f, settings.Speed);
            turnSpeed = Mathf.Max(0f, settings.TurnSpeed);
            explosionRadius = Mathf.Max(0.1f, settings.ExplosionRadius);
            maxLifetime = Mathf.Max(0.01f, settings.MaxLifetime);
            targetSearchRadius = Mathf.Max(0f, settings.TargetSearchRadius);
            targetPoint = settings.TargetPoint;
            target = settings.Target;
            instigator = settings.Instigator;
            instigatorRole = settings.InstigatorRole;
            impactEffectPrefab = settings.ImpactEffectPrefab;
            lifetimeRemaining = maxLifetime;
            hasImpacted = false;
            initialized = true;
            EnsurePhysicsComponents();
        }

        private void EnsurePhysicsComponents()
        {
            projectileCollider = GetComponent<Collider>();
            if (projectileCollider == null)
            {
                SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                projectileCollider = sphereCollider;
            }
            else
            {
                projectileCollider.isTrigger = true;
            }

            body = GetComponent<Rigidbody>();
            if (body == null)
                body = gameObject.AddComponent<Rigidbody>();

            body.isKinematic = false;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void UpdateMissileHeading(float deltaTime)
        {
            if (target == null || !IsValidTarget(target))
                target = FindNearestEnemyTarget();

            Vector3 aimPoint = target != null ? target.position : targetPoint;
            Vector3 direction = aimPoint - transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                desiredRotation,
                turnSpeed * deltaTime);
        }

        private Transform FindNearestEnemyTarget()
        {
            if (targetSearchRadius <= 0f)
                return null;

            Collider[] candidates = Physics.OverlapSphere(
                transform.position,
                targetSearchRadius,
                damageLayers,
                QueryTriggerInteraction.Collide);
            Transform nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < candidates.Length; i++)
            {
                HealthComponent candidate = candidates[i].GetComponentInParent<HealthComponent>();
                if (candidate == null || !CanDamage(candidate))
                    continue;

                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate.transform;
                }
            }

            return nearest;
        }

        private void Impact(Vector3 impactPoint, HealthComponent directTarget)
        {
            if (hasImpacted)
                return;

            hasImpacted = true;
            if (isMissile)
                ApplyAreaDamage(impactPoint);
            else if (directTarget != null && CanDamage(directTarget))
                directTarget.TakeDamage(damage, instigator);

            if (impactEffectPrefab != null)
            {
                GameObject effect = Instantiate(impactEffectPrefab, impactPoint, Quaternion.identity);
                TimedEffectDestroyer cleaner = effect.GetComponent<TimedEffectDestroyer>();
                if (cleaner == null)
                    cleaner = effect.AddComponent<TimedEffectDestroyer>();
                cleaner.Initialize(Mathf.Max(0.1f, maxLifetime));
            }

            Destroy(gameObject);
        }

        private void ApplyAreaDamage(Vector3 center)
        {
            int count = GetOverlapCount(center);
            for (int i = 0; i < count; i++)
            {
                Collider candidate = overlapBuffer[i];
                if (candidate == null)
                    continue;

                HealthComponent health = candidate.GetComponentInParent<HealthComponent>();
                if (health != null && CanDamage(health))
                    health.TakeDamage(damage, instigator);
            }
        }

        private int GetOverlapCount(Vector3 center)
        {
            while (true)
            {
                int count = Physics.OverlapSphereNonAlloc(
                    center,
                    explosionRadius,
                    overlapBuffer,
                    damageLayers,
                    QueryTriggerInteraction.Collide);
                if (count < overlapBuffer.Length || overlapBuffer.Length >= MaximumOverlapCapacity)
                    return count;

                overlapBuffer = new Collider[Mathf.Min(overlapBuffer.Length * 2, MaximumOverlapCapacity)];
            }
        }

        private bool CanDamage(HealthComponent candidate)
        {
            if (candidate == null || candidate.IsDead || candidate.gameObject == instigator ||
                (instigator != null && candidate.transform.IsChildOf(instigator.transform)))
            {
                return false;
            }

            if (instigatorRole == HealthRole.PlayerMech)
                return candidate.Role == HealthRole.Enemy || candidate.Role == HealthRole.Boss;
            if (instigatorRole == HealthRole.Enemy || instigatorRole == HealthRole.Boss)
                return candidate.Role == HealthRole.PlayerMech || candidate.Role == HealthRole.Reactor;

            return candidate.Role != instigatorRole;
        }

        private bool IsValidTarget(Transform candidate)
        {
            HealthComponent health = candidate != null ? candidate.GetComponentInParent<HealthComponent>() : null;
            return health != null && CanDamage(health);
        }

        private bool IsOwnedByInstigator(Transform candidate)
        {
            return instigator != null && (candidate == instigator.transform || candidate.IsChildOf(instigator.transform));
        }
    }
}
