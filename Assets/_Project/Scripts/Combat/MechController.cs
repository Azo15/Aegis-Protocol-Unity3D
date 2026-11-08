using System;
using UnityEngine;
using AegisProtocol.Core;

namespace AegisProtocol.Combat
{
    /// <summary>Controls XZ chassis movement, independent turret aiming, and the dash.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(HealthComponent), typeof(PlayerInputReader))]
    public sealed class MechController : MonoBehaviour
    {
        private const float DefaultPlayerMaxHealth = 200f;

        [Header("References")]
        [SerializeField] private MechMovementConfig movementConfig;
        [SerializeField] private Transform chassisTransform;
        [SerializeField] private Transform turretTransform;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private LayerMask aimSurfaceMask = ~0;
        [SerializeField] private bool allowBriefingInput = true;

        private readonly RaycastHit[] aimHits = new RaycastHit[24];
        private Rigidbody body;
        private HealthComponent health;
        private PlayerInputReader inputReader;
        private MechMovementConfig runtimeFallbackConfig;
        private Vector3 currentVelocity;
        private Vector3 dashDirection;
        private float dashTimeRemaining;
        private float dashCooldownRemaining;
        private float moveSpeedMultiplier = 1f;
        private bool isDashing;

        public event Action OnDashStarted;
        /// <summary>Cooldown fraction: 1 means cooling down, 0 means ready.</summary>
        public event Action<float> OnDashCooldownUpdated;

        public Vector3 AimWorldPoint { get; private set; }
        public Vector3 AimDirection { get; private set; }
        public bool IsDashing => isDashing;
        public float DashCooldownRemaining => dashCooldownRemaining;
        public float DashCooldownNormalized => movementConfig == null || movementConfig.DashCooldown <= 0f
            ? 0f
            : Mathf.Clamp01(dashCooldownRemaining / movementConfig.DashCooldown);
        public HealthComponent Health => health;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            health = GetComponent<HealthComponent>();
            inputReader = GetComponent<PlayerInputReader>();

            if (body == null || health == null || inputReader == null)
            {
                Debug.LogError("MechController requires Rigidbody, HealthComponent, and PlayerInputReader.", this);
                enabled = false;
                return;
            }

            body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            health.Configure(HealthRole.PlayerMech, DefaultPlayerMaxHealth, refillCurrentHealth: true);

            if (chassisTransform == null)
                chassisTransform = transform;
            if (turretTransform == null)
                Debug.LogWarning("Assign a turret transform to enable independent mouse aiming.", this);
            if (aimCamera == null)
                aimCamera = Camera.main;

            if (movementConfig == null)
            {
                runtimeFallbackConfig = ScriptableObject.CreateInstance<MechMovementConfig>();
                runtimeFallbackConfig.hideFlags = HideFlags.DontSave;
                movementConfig = runtimeFallbackConfig;
            }

            AimDirection = chassisTransform.forward;
            AimWorldPoint = transform.position + AimDirection * 5f;
            dashCooldownRemaining = 0f;
        }

        private void Update()
        {
            UpdateDashCooldown(Time.deltaTime);

            if (!CanControlMech())
                return;

            PlayerInputSnapshot input = inputReader.CurrentSnapshot;
            UpdateAim(input.PointerScreenPosition);

            if (input.DashPressed)
                TryStartDash(input.MoveVector);
        }

        private void FixedUpdate()
        {
            if (!CanControlMech())
            {
                currentVelocity = Vector3.zero;
                body.linearVelocity = Vector3.zero;
                return;
            }

            float fixedDeltaTime = Time.fixedDeltaTime;
            if (isDashing)
            {
                Vector3 dashPosition = ClampToArena(body.position + dashDirection * movementConfig.DashSpeed * fixedDeltaTime);
                body.MovePosition(dashPosition);
                dashTimeRemaining -= fixedDeltaTime;
                if (dashTimeRemaining <= 0f)
                    isDashing = false;
                return;
            }

            Vector2 input = inputReader.CurrentSnapshot.MoveVector;
            Vector3 moveDirection = new Vector3(input.x, 0f, input.y);
            if (moveDirection.sqrMagnitude > 1f)
                moveDirection.Normalize();

            float effectiveSpeed = movementConfig.MoveSpeed * moveSpeedMultiplier;
            Vector3 targetVelocity = moveDirection * effectiveSpeed;
            float rate = moveDirection.sqrMagnitude > 0f
                ? movementConfig.Acceleration
                : movementConfig.Deceleration;
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, rate * fixedDeltaTime);

            if (moveDirection.sqrMagnitude > 0.0001f)
                RotateChassis(moveDirection, fixedDeltaTime);

            Vector3 nextPosition = ClampToArena(body.position + currentVelocity * fixedDeltaTime);
            body.MovePosition(nextPosition);
        }

        private void OnDestroy()
        {
            if (runtimeFallbackConfig != null)
                Destroy(runtimeFallbackConfig);
        }

        /// <summary>Multiplies the current movement scale, allowing upgrades to stack.</summary>
        public void ApplyMoveSpeedMultiplier(float multiplier)
        {
            if (multiplier <= 0f || float.IsNaN(multiplier) || float.IsInfinity(multiplier))
                return;

            moveSpeedMultiplier = Mathf.Clamp(moveSpeedMultiplier * multiplier, 0.1f, 5f);
        }

        /// <summary>Raises maximum health without healing existing damage.</summary>
        public void ApplyMaxHealthBonus(float bonus)
        {
            if (health == null || bonus <= 0f || float.IsNaN(bonus) || float.IsInfinity(bonus))
                return;

            health.SetMaxHealth(health.MaxHealth + bonus, refillCurrentHealth: false);
        }

        private bool CanControlMech()
        {
            if (health == null || health.IsDead)
                return false;

            GameManager manager = GameManager.Instance;
            if (manager == null)
                return true;

            return manager.CurrentState == GameState.WaveActive ||
                   (allowBriefingInput && manager.CurrentState == GameState.Briefing);
        }

        private void RotateChassis(Vector3 moveDirection, float deltaTime)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            chassisTransform.rotation = Quaternion.Slerp(
                chassisTransform.rotation,
                targetRotation,
                movementConfig.ChassisTurnSpeed * deltaTime);
        }

        private void UpdateAim(Vector2 pointerPosition)
        {
            if (turretTransform == null)
                return;

            if (aimCamera == null)
                aimCamera = Camera.main;
            if (aimCamera == null)
                return;

            Ray ray = aimCamera.ScreenPointToRay(pointerPosition);
            if (!TryGetAimPoint(ray, out Vector3 hitPoint))
                return;

            AimWorldPoint = hitPoint;
            AimWorldPoint = new Vector3(AimWorldPoint.x, turretTransform.position.y, AimWorldPoint.z);
            Vector3 flatDirection = AimWorldPoint - turretTransform.position;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude <= 0.0001f)
                return;

            AimDirection = flatDirection.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(AimDirection, Vector3.up);
            turretTransform.rotation = Quaternion.Slerp(
                turretTransform.rotation,
                targetRotation,
                movementConfig.TurretTurnSpeed * Time.deltaTime);
        }

        private bool TryGetAimPoint(Ray ray, out Vector3 point)
        {
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                aimHits,
                movementConfig.AimRayDistance,
                aimSurfaceMask,
                QueryTriggerInteraction.Ignore);

            float nearestDistance = float.PositiveInfinity;
            bool foundHit = false;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = aimHits[i];
                if (hit.collider == null || hit.transform.IsChildOf(transform))
                    continue;

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    point = hit.point;
                    foundHit = true;
                }
            }

            if (foundHit)
                return true;

            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (groundPlane.Raycast(ray, out float distance) && distance >= 0f)
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = AimWorldPoint;
            return false;
        }

        private void TryStartDash(Vector2 moveInput)
        {
            if (isDashing || dashCooldownRemaining > 0f)
                return;

            Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y);
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();
            if (direction.sqrMagnitude <= 0.0001f)
                direction = AimDirection;
            if (direction.sqrMagnitude <= 0.0001f && chassisTransform != null)
                direction = chassisTransform.forward;

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                return;

            dashDirection = direction.normalized;
            dashTimeRemaining = movementConfig.DashDuration;
            dashCooldownRemaining = movementConfig.DashCooldown;
            isDashing = true;
            OnDashStarted?.Invoke();
            PublishDashCooldown();
        }

        private void UpdateDashCooldown(float deltaTime)
        {
            if (dashCooldownRemaining <= 0f)
                return;

            float previousNormalized = DashCooldownNormalized;
            dashCooldownRemaining = Mathf.Max(0f, dashCooldownRemaining - deltaTime);
            if (!Mathf.Approximately(previousNormalized, DashCooldownNormalized))
                PublishDashCooldown();
        }

        private void PublishDashCooldown()
        {
            OnDashCooldownUpdated?.Invoke(DashCooldownNormalized);
        }

        private Vector3 ClampToArena(Vector3 position)
        {
            Bounds bounds = movementConfig.ArenaBounds;
            position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            position.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);
            return position;
        }
    }
}
