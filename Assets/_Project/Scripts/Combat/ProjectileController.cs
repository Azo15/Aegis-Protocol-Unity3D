using System.Collections.Generic;
using UnityEngine;
using AegisProtocol.Audio;
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
        public int LevelTier;
        public Color ProjectileColor;
        public Color ExplosionColor;
    }

    /// <summary>Moves a plasma bolt or guided missile and applies its level-scaled impact and explosion.</summary>
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
        private Collider[] overlapBuffer = new Collider[InitialOverlapCapacity];
        private Collider[] targetSearchBuffer = new Collider[InitialOverlapCapacity];
        private readonly HashSet<HealthComponent> damagedTargets = new HashSet<HealthComponent>();
        private Material runtimeVisualMaterial;
        private Material runtimeTrailMaterial;
        private float lifetimeRemaining;
        private int levelTier = 1;
        private Color projectileColor = new Color(0.2f, 0.95f, 1f);
        private Color explosionColor = new Color(0.2f, 0.85f, 1f);
        private bool isInitialized;
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

        private void OnDestroy()
        {
            if (runtimeVisualMaterial != null)
                Destroy(runtimeVisualMaterial);
            if (runtimeTrailMaterial != null)
                Destroy(runtimeTrailMaterial);
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
            if (!isInitialized || hasImpacted || other == null || IsOwnedByInstigator(other.transform))
                return;

            string otherName = other.gameObject.name;
            if (otherName == "Foundation" || otherName.Contains("Grid") || otherName.Contains("Socket"))
                return;

            HealthComponent health = other.GetComponentInParent<HealthComponent>();
            if (other.isTrigger && health == null)
                return;

            if (health != null && !CanDamage(health))
                return;

            if (other.GetComponentInParent<ProjectileController>() != null)
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
            levelTier = Mathf.Clamp(settings.LevelTier <= 0 ? 1 : settings.LevelTier, 1, 5);
            projectileColor = settings.ProjectileColor.a > 0.01f
                ? settings.ProjectileColor
                : new Color(0.2f, 0.95f, 1f, 1f);
            explosionColor = settings.ExplosionColor.a > 0.01f
                ? settings.ExplosionColor
                : projectileColor;
            lifetimeRemaining = maxLifetime;
            hasImpacted = false;
            EnsurePhysicsComponents();
            ApplyProjectileVisuals();
            isInitialized = true;
        }

        private void EnsurePhysicsComponents()
        {
            Collider projectileCollider = GetComponent<Collider>();
            if (projectileCollider == null)
                projectileCollider = gameObject.AddComponent<SphereCollider>();
            projectileCollider.isTrigger = true;
            if (projectileCollider is SphereCollider sc)
                sc.radius = 0.55f;

            body = GetComponent<Rigidbody>();
            if (body == null)
                body = gameObject.AddComponent<Rigidbody>();

            body.isKinematic = false;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void ApplyProjectileVisuals()
        {
            Renderer rootRenderer = GetComponent<Renderer>();
            if (rootRenderer != null)
                rootRenderer.enabled = false;

            float widthScale = isMissile
                ? 0.42f + levelTier * 0.06f
                : 0.28f + levelTier * 0.05f;
            float lengthScale = isMissile
                ? 1.15f + levelTier * 0.16f
                : 0.90f + levelTier * 0.16f;
            transform.localScale = Vector3.one;

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (litShader != null)
            {
                runtimeVisualMaterial = new Material(litShader)
                {
                    color = projectileColor
                };
                if (runtimeVisualMaterial.HasProperty("_EmissionColor"))
                {
                    runtimeVisualMaterial.EnableKeyword("_EMISSION");
                    runtimeVisualMaterial.SetColor("_EmissionColor", projectileColor * (1.7f + levelTier * 0.3f));
                }

                GameObject casing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                casing.name = "Bullet Casing";
                casing.transform.SetParent(transform, false);
                casing.transform.localPosition = Vector3.zero;
                casing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                casing.transform.localScale = new Vector3(widthScale, lengthScale * 0.48f, widthScale);
                Collider casingCol = casing.GetComponent<Collider>();
                if (casingCol != null)
                    casingCol.enabled = false;
                Renderer casingRen = casing.GetComponent<Renderer>();
                if (casingRen != null)
                    casingRen.sharedMaterial = runtimeVisualMaterial;

                GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                nose.name = "Bullet Warhead Tip";
                nose.transform.SetParent(transform, false);
                nose.transform.localPosition = new Vector3(0f, 0f, lengthScale * 0.45f);
                nose.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                nose.transform.localScale = new Vector3(widthScale * 0.92f, lengthScale * 0.28f, widthScale * 0.92f);
                Collider noseCol = nose.GetComponent<Collider>();
                if (noseCol != null)
                    noseCol.enabled = false;
                Renderer noseRen = nose.GetComponent<Renderer>();
                if (noseRen != null)
                    noseRen.sharedMaterial = runtimeVisualMaterial;

                if (isMissile || levelTier >= 3)
                {
                    GameObject finA = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    finA.name = "Tail Fin Horizontal";
                    finA.transform.SetParent(transform, false);
                    finA.transform.localPosition = new Vector3(0f, 0f, -lengthScale * 0.36f);
                    finA.transform.localScale = new Vector3(widthScale * 2.2f, widthScale * 0.18f, lengthScale * 0.26f);
                    Collider finACol = finA.GetComponent<Collider>();
                    if (finACol != null)
                        finACol.enabled = false;
                    Renderer finARen = finA.GetComponent<Renderer>();
                    if (finARen != null)
                        finARen.sharedMaterial = runtimeVisualMaterial;

                    GameObject finB = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    finB.name = "Tail Fin Vertical";
                    finB.transform.SetParent(transform, false);
                    finB.transform.localPosition = new Vector3(0f, 0f, -lengthScale * 0.36f);
                    finB.transform.localScale = new Vector3(widthScale * 0.18f, widthScale * 2.2f, lengthScale * 0.26f);
                    Collider finBCol = finB.GetComponent<Collider>();
                    if (finBCol != null)
                        finBCol.enabled = false;
                    Renderer finBRen = finB.GetComponent<Renderer>();
                    if (finBRen != null)
                        finBRen.sharedMaterial = runtimeVisualMaterial;
                }
            }

            TrailRenderer trail = GetComponent<TrailRenderer>();
            if (trail == null)
                trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = isMissile ? 0.26f : 0.16f;
            trail.startWidth = widthScale * 0.85f;
            trail.endWidth = 0.04f;
            trail.minVertexDistance = 0.05f;
            Shader trailShader = Shader.Find("Sprites/Default") ?? litShader;
            if (trailShader != null)
            {
                runtimeTrailMaterial = new Material(trailShader)
                {
                    color = projectileColor
                };
                trail.sharedMaterial = runtimeTrailMaterial;
            }
            trail.startColor = projectileColor;
            trail.endColor = new Color(projectileColor.r, projectileColor.g, projectileColor.b, 0f);

            Light glow = GetComponent<Light>();
            if (glow == null)
                glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = projectileColor;
            glow.intensity = 2.4f + levelTier * 0.45f;
            glow.range = 4.2f + levelTier * 0.8f;
        }

        private void UpdateMissileHeading(float deltaTime)
        {
            if (target == null || !IsValidTarget(target))
                target = FindNearestEnemyTarget();

            Vector3 aimPoint = target != null ? target.position : targetPoint;
            Vector3 direction = aimPoint - transform.position;
            direction.y = 0f;
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

            int candidateCount = GetTargetSearchCount();
            Transform nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < candidateCount; i++)
            {
                Collider collider = targetSearchBuffer[i];
                if (collider == null)
                    continue;

                HealthComponent candidate = collider.GetComponentInParent<HealthComponent>();
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
            if (isMissile || (instigatorRole == HealthRole.PlayerMech && explosionRadius >= 0.8f))
            {
                ApplyAreaDamage(impactPoint);
                if (directTarget != null && CanDamage(directTarget) && damagedTargets.Add(directTarget))
                    directTarget.TakeDamage(damage, instigator);
            }
            else if (directTarget != null && CanDamage(directTarget))
            {
                directTarget.TakeDamage(damage, instigator);
            }

            if (instigatorRole == HealthRole.PlayerMech)
            {
                SpawnLevelExplosionEffect(impactPoint);
                if (directTarget != null || isMissile)
                    AudioManager.Instance?.PlayLevelExplosion(levelTier, isMissile);
            }
            else if (impactEffectPrefab != null)
            {
                GameObject effect = Instantiate(impactEffectPrefab, impactPoint, Quaternion.identity);
                TimedEffectDestroyer cleaner = effect.GetComponent<TimedEffectDestroyer>();
                if (cleaner == null)
                    cleaner = effect.AddComponent<TimedEffectDestroyer>();
                cleaner.Initialize(Mathf.Max(0.1f, maxLifetime));
            }

            Destroy(gameObject);
        }

        private void SpawnLevelExplosionEffect(Vector3 center)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material expMat = shader != null ? new Material(shader) : null;
            if (expMat != null)
            {
                expMat.color = explosionColor;
                if (expMat.HasProperty("_EmissionColor"))
                {
                    expMat.EnableKeyword("_EMISSION");
                    expMat.SetColor("_EmissionColor", explosionColor * (2.2f + levelTier * 0.5f));
                }
            }

            PrimitiveType burstShape = levelTier == 2 || levelTier == 4
                ? PrimitiveType.Cube
                : PrimitiveType.Sphere;
            GameObject blast = GameObject.CreatePrimitive(burstShape);
            blast.name = $"Level_{levelTier}_Explosion";
            blast.transform.position = new Vector3(center.x, 1.0f, center.z);
            blast.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            Collider blastCollider = blast.GetComponent<Collider>();
            if (blastCollider != null)
                blastCollider.enabled = false;
            Renderer blastRenderer = blast.GetComponent<Renderer>();
            if (blastRenderer != null && expMat != null)
                blastRenderer.sharedMaterial = expMat;

            if (levelTier >= 2)
            {
                GameObject shockRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                shockRing.name = "Shockwave Ring";
                shockRing.transform.SetParent(blast.transform, false);
                shockRing.transform.localPosition = Vector3.zero;
                shockRing.transform.localScale = new Vector3(1.8f, 0.08f, 1.8f);
                Collider ringCollider = shockRing.GetComponent<Collider>();
                if (ringCollider != null)
                    ringCollider.enabled = false;
                Renderer ringRenderer = shockRing.GetComponent<Renderer>();
                if (ringRenderer != null && expMat != null)
                    ringRenderer.sharedMaterial = expMat;
            }

            if (levelTier >= 4)
            {
                GameObject crossRing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crossRing.name = "Star Shock";
                crossRing.transform.SetParent(blast.transform, false);
                crossRing.transform.localPosition = Vector3.zero;
                crossRing.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                crossRing.transform.localScale = new Vector3(1.6f, 0.25f, 1.6f);
                Collider crossCollider = crossRing.GetComponent<Collider>();
                if (crossCollider != null)
                    crossCollider.enabled = false;
                Renderer crossRenderer = crossRing.GetComponent<Renderer>();
                if (crossRenderer != null && expMat != null)
                    crossRenderer.sharedMaterial = expMat;
            }

            Light blastLight = blast.AddComponent<Light>();
            blastLight.type = LightType.Point;
            blastLight.color = explosionColor;
            blastLight.intensity = (isMissile ? 5.5f : 3.2f) + levelTier * 0.8f;
            blastLight.range = (isMissile ? 7.5f : 4.5f) + levelTier * 1.4f;

            float targetDiameter = isMissile
                ? (2.4f + levelTier * 0.9f)
                : (1.1f + levelTier * 0.55f);
            float duration = isMissile
                ? (0.28f + levelTier * 0.04f)
                : (0.18f + levelTier * 0.03f);

            TimedEffectDestroyer expander = blast.AddComponent<TimedEffectDestroyer>();
            expander.InitializeExpandingExplosion(
                duration,
                Vector3.one * 0.35f,
                new Vector3(targetDiameter, targetDiameter * 0.65f, targetDiameter),
                blastLight,
                expMat);
        }

        private void ApplyAreaDamage(Vector3 center)
        {
            int count = GetOverlapCount(center);
            damagedTargets.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider candidate = overlapBuffer[i];
                if (candidate == null)
                    continue;

                HealthComponent health = candidate.GetComponentInParent<HealthComponent>();
                if (health != null && CanDamage(health) && damagedTargets.Add(health))
                    health.TakeDamage(damage, instigator);
            }
        }

        private int GetTargetSearchCount()
        {
            while (true)
            {
                int count = Physics.OverlapSphereNonAlloc(
                    transform.position,
                    targetSearchRadius,
                    targetSearchBuffer,
                    damageLayers,
                    QueryTriggerInteraction.Collide);
                if (count < targetSearchBuffer.Length || targetSearchBuffer.Length >= MaximumOverlapCapacity)
                    return count;

                targetSearchBuffer = new Collider[Mathf.Min(targetSearchBuffer.Length * 2, MaximumOverlapCapacity)];
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
            if (instigatorRole == HealthRole.Reactor)
                return candidate.Role == HealthRole.Enemy || candidate.Role == HealthRole.Boss;

            return false;
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
