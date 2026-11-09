using System;
using UnityEngine;
using AegisProtocol.Core;

namespace AegisProtocol.Combat
{
    /// <summary>Runs the mech's plasma cannon, guided missile, and shared heat lockout.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MechController), typeof(PlayerInputReader))]
    public sealed class WeaponSystem : MonoBehaviour
    {
        private const float MissileTargetSearchRadius = 35f;
        private const float MissileShakeIntensity = 0.12f;
        private const float MissileShakeDuration = 0.12f;

        [Header("Configuration")]
        [SerializeField] private WeaponConfig weaponConfig;
        [Header("Launch points and prefabs")]
        [SerializeField] private Transform muzzleTransform = null;
        [SerializeField] private ProjectileController plasmaProjectilePrefab = null;
        [SerializeField] private ProjectileController missileProjectilePrefab = null;
        [SerializeField] private GameObject muzzleFlashPrefab = null;
        [SerializeField] private GameObject impactEffectPrefab = null;
        [SerializeField] private IsometricCameraRig cameraRig = null;

        private MechController mechController;
        private PlayerInputReader inputReader;
        private WeaponConfig runtimeFallbackConfig;
        private float currentHeat;
        private float overheatRemaining;
        private float missileCooldownRemaining;
        private float lastShotTime = float.NegativeInfinity;
        private float heatGenerationMultiplier = 1f;
        private float fireRateMultiplier = 1f;
        private float damageMultiplier = 1f;
        private float coolingMultiplier = 1f;
        private int primaryProjectileCount = 1;
        private float primarySpreadAngle = 0f;
        private int missileSalvoCount = 1;
        private bool isOverheated;

        public event Action<WeaponFireEvent> OnWeaponFired;
        public event Action<float, float, bool> OnHeatChanged;
        public event Action OnOverheatStarted;
        public event Action OnOverheatCleared;
        public event Action<float> OnMissileCooldownChanged;

        public float CurrentHeat => currentHeat;
        public float MaximumHeat => weaponConfig != null ? weaponConfig.MaximumHeat : 100f;
        public bool IsOverheated => isOverheated;
        public float OverheatRemaining => overheatRemaining;
        public float MissileCooldownRemaining => missileCooldownRemaining;
        public float MissileCooldownNormalized => weaponConfig == null || weaponConfig.MissileCooldown <= 0f
            ? 0f
            : Mathf.Clamp01(missileCooldownRemaining / weaponConfig.MissileCooldown);

        private void Awake()
        {
            mechController = GetComponent<MechController>();
            inputReader = GetComponent<PlayerInputReader>();
            if (weaponConfig == null)
            {
                runtimeFallbackConfig = ScriptableObject.CreateInstance<WeaponConfig>();
                runtimeFallbackConfig.hideFlags = HideFlags.DontSave;
                weaponConfig = runtimeFallbackConfig;
            }
        }

        private void Update()
        {
            UpdateCooldowns(Time.deltaTime);
            if (!CanUseWeapons())
                return;

            PlayerInputSnapshot input = inputReader.CurrentSnapshot;
            if (input.PrimaryFirePressed || input.PrimaryFireHeld)
                TryFirePrimary();
            if (input.SecondaryFirePressed)
                TryFireMissile();
        }

        private void OnDestroy()
        {
            if (runtimeFallbackConfig != null)
                Destroy(runtimeFallbackConfig);
        }

        /// <summary>Connects launch points and projectile templates created by the arena builder.</summary>
        public void ConfigureRuntimeReferences(
            Transform muzzle,
            ProjectileController plasmaPrefab,
            ProjectileController missilePrefab,
            IsometricCameraRig runtimeCamera)
        {
            if (muzzle != null)
                muzzleTransform = muzzle;
            if (plasmaPrefab != null)
                plasmaProjectilePrefab = plasmaPrefab;
            if (missilePrefab != null)
                missileProjectilePrefab = missilePrefab;
            if (runtimeCamera != null)
                cameraRig = runtimeCamera;
        }

        /// <summary>Applies the active campaign sector's weapon evolution and cooling bonuses.</summary>
        public void ConfigureLevelLoadout(SectorLevelData levelData)
        {
            primaryProjectileCount = Mathf.Clamp(levelData.PrimaryProjectileCount, 1, 7);
            primarySpreadAngle = Mathf.Clamp(levelData.PrimarySpreadAngle, 0f, 60f);
            missileSalvoCount = Mathf.Clamp(levelData.MissileSalvoCount, 1, 5);
            damageMultiplier = Mathf.Max(0.5f, levelData.WeaponDamageMultiplier);
            coolingMultiplier = Mathf.Max(0.5f, levelData.CoolingMultiplier);
        }

        /// <summary>Applies a cumulative positive heat-generation upgrade.</summary>
        public void ApplyHeatGenerationMultiplier(float multiplier)
        {
            if (IsFinitePositive(multiplier))
                heatGenerationMultiplier = Mathf.Clamp(heatGenerationMultiplier * multiplier, 0.1f, 5f);
        }

        /// <summary>Applies a cumulative positive primary fire-rate upgrade.</summary>
        public void ApplyFireRateMultiplier(float multiplier)
        {
            if (IsFinitePositive(multiplier))
                fireRateMultiplier = Mathf.Clamp(fireRateMultiplier * multiplier, 0.1f, 5f);
        }

        /// <summary>Applies a cumulative positive damage upgrade to both weapons.</summary>
        public void ApplyDamageMultiplier(float multiplier)
        {
            if (IsFinitePositive(multiplier))
                damageMultiplier = Mathf.Clamp(damageMultiplier * multiplier, 0.1f, 5f);
        }

        private bool TryFirePrimary()
        {
            float shotInterval = 1f / (weaponConfig.PrimaryFireRate * fireRateMultiplier);
            if (isOverheated || Time.time - lastShotTime < shotInterval || plasmaProjectilePrefab == null)
                return false;

            float heatAfterShot = currentHeat + weaponConfig.PrimaryHeatPerShot * heatGenerationMultiplier;

            Vector3 baseDirection = GetAimDirection();
            Vector3 muzzleOrigin = GetMuzzlePosition();
            Vector3 right = Vector3.Cross(Vector3.up, baseDirection).normalized;
            int count = Mathf.Max(1, primaryProjectileCount);

            for (int i = 0; i < count; i++)
            {
                float centeredIndex = count == 1 ? 0f : (i / (float)(count - 1)) - 0.5f;
                float yawAngle = centeredIndex * primarySpreadAngle;
                Vector3 shotDirection = Quaternion.AngleAxis(yawAngle, Vector3.up) * baseDirection;
                Vector3 lateralOffset = right * (centeredIndex * Mathf.Min(0.9f, (count - 1) * 0.32f));
                Vector3 spawnPos = muzzleOrigin + lateralOffset;

                ProjectileController projectile = Instantiate(
                    plasmaProjectilePrefab,
                    spawnPos,
                    Quaternion.LookRotation(shotDirection, Vector3.up));
                projectile.gameObject.SetActive(true);
                projectile.Initialize(new ProjectileLaunchSettings
                {
                    IsMissile = false,
                    Damage = weaponConfig.PrimaryDamage * damageMultiplier,
                    Speed = weaponConfig.PrimaryProjectileSpeed,
                    MaxLifetime = weaponConfig.PrimaryLifetime,
                    TargetPoint = mechController.AimWorldPoint,
                    Instigator = gameObject,
                    InstigatorRole = HealthRole.PlayerMech,
                    ImpactEffectPrefab = impactEffectPrefab
                });
            }

            currentHeat = Mathf.Min(weaponConfig.MaximumHeat, heatAfterShot);
            lastShotTime = Time.time;
            PublishHeatChanged();
            SpawnMuzzleFlash(baseDirection);
            OnWeaponFired?.Invoke(new WeaponFireEvent(false, muzzleOrigin, baseDirection));
            if (heatAfterShot >= weaponConfig.MaximumHeat)
                BeginOverheat();
            return true;
        }

        private bool TryFireMissile()
        {
            if (isOverheated || missileCooldownRemaining > 0f || missileProjectilePrefab == null)
                return false;

            float heatAfterShot = currentHeat + weaponConfig.MissileHeatPerShot * heatGenerationMultiplier;

            Vector3 muzzleOrigin = GetMuzzlePosition();
            Vector3 direction = mechController.AimWorldPoint - muzzleOrigin;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = GetAimDirection();
            direction.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            int salvo = Mathf.Max(1, missileSalvoCount);
            for (int i = 0; i < salvo; i++)
            {
                float centered = salvo == 1 ? 0f : (i / (float)(salvo - 1)) - 0.5f;
                Vector3 missileDir = Quaternion.AngleAxis(centered * 14f, Vector3.up) * direction;
                Vector3 spawnPos = muzzleOrigin + right * (centered * 0.65f);

                ProjectileController projectile = Instantiate(
                    missileProjectilePrefab,
                    spawnPos,
                    Quaternion.LookRotation(missileDir, Vector3.up));
                projectile.gameObject.SetActive(true);
                projectile.Initialize(new ProjectileLaunchSettings
                {
                    IsMissile = true,
                    Damage = weaponConfig.MissileDamage * damageMultiplier,
                    Speed = weaponConfig.MissileSpeed,
                    TurnSpeed = weaponConfig.MissileTurnSpeed,
                    ExplosionRadius = weaponConfig.MissileExplosionRadius,
                    MaxLifetime = weaponConfig.MissileLifetime,
                    TargetSearchRadius = MissileTargetSearchRadius,
                    TargetPoint = mechController.AimWorldPoint,
                    Instigator = gameObject,
                    InstigatorRole = HealthRole.PlayerMech,
                    ImpactEffectPrefab = impactEffectPrefab
                });
            }

            currentHeat = Mathf.Min(weaponConfig.MaximumHeat, heatAfterShot);
            missileCooldownRemaining = weaponConfig.MissileCooldown;
            PublishMissileCooldown();
            PublishHeatChanged();
            SpawnMuzzleFlash(direction);
            OnWeaponFired?.Invoke(new WeaponFireEvent(true, muzzleOrigin, direction));
            if (cameraRig != null)
                cameraRig.TriggerCameraShake(MissileShakeIntensity, MissileShakeDuration);
            if (heatAfterShot >= weaponConfig.MaximumHeat)
                BeginOverheat();
            return true;
        }

        private void UpdateCooldowns(float deltaTime)
        {
            if (missileCooldownRemaining > 0f)
            {
                float previous = MissileCooldownNormalized;
                missileCooldownRemaining = Mathf.Max(0f, missileCooldownRemaining - deltaTime);
                if (!Mathf.Approximately(previous, MissileCooldownNormalized))
                    PublishMissileCooldown();
            }

            if (isOverheated)
            {
                overheatRemaining = Mathf.Max(0f, overheatRemaining - deltaTime);
                if (overheatRemaining <= 0f)
                    ClearOverheat();
            }
            else if (currentHeat > 0f)
            {
                currentHeat = Mathf.Max(0f, currentHeat - weaponConfig.CoolingRate * coolingMultiplier * deltaTime);
                PublishHeatChanged();
            }
        }

        private void BeginOverheat()
        {
            if (isOverheated)
                return;

            currentHeat = Mathf.Min(currentHeat, weaponConfig.MaximumHeat);
            isOverheated = true;
            overheatRemaining = weaponConfig.OverheatLockoutDuration;
            PublishHeatChanged();
            OnOverheatStarted?.Invoke();
            if (overheatRemaining <= 0f)
                ClearOverheat();
        }

        private void ClearOverheat()
        {
            if (!isOverheated)
                return;

            isOverheated = false;
            overheatRemaining = 0f;
            currentHeat = Mathf.Min(currentHeat, weaponConfig.MaximumHeat * 0.35f);
            PublishHeatChanged();
            OnOverheatCleared?.Invoke();
        }

        private void PublishHeatChanged()
        {
            OnHeatChanged?.Invoke(currentHeat, weaponConfig.MaximumHeat, isOverheated);
        }

        private void PublishMissileCooldown()
        {
            OnMissileCooldownChanged?.Invoke(MissileCooldownNormalized);
        }

        private void SpawnMuzzleFlash(Vector3 direction)
        {
            if (muzzleFlashPrefab == null)
                return;

            GameObject flash = Instantiate(
                muzzleFlashPrefab,
                GetMuzzlePosition(),
                Quaternion.LookRotation(direction, Vector3.up));
            TimedEffectDestroyer cleaner = flash.GetComponent<TimedEffectDestroyer>();
            if (cleaner == null)
                cleaner = flash.AddComponent<TimedEffectDestroyer>();
            cleaner.Initialize(0.2f);
        }

        private Vector3 GetAimDirection()
        {
            Vector3 direction = mechController.AimDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
                direction = transform.forward;
            return direction.normalized;
        }

        private Vector3 GetMuzzlePosition()
        {
            Vector3 dir = GetAimDirection();
            Vector3 pos = muzzleTransform != null ? muzzleTransform.position : transform.position + dir * 2.1f;
            pos.y = 1.05f;
            return pos;
        }

        private bool CanUseWeapons()
        {
            if (mechController == null || inputReader == null || mechController.Health == null ||
                mechController.Health.IsDead)
            {
                return false;
            }

            GameManager manager = GameManager.Instance;
            return manager == null || manager.CurrentState == GameState.WaveActive;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>Describes one weapon shot for UI, sound, and gameplay listeners.</summary>
    public readonly struct WeaponFireEvent
    {
        public bool IsMissile { get; }
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }

        public WeaponFireEvent(bool isMissile, Vector3 origin, Vector3 direction)
        {
            IsMissile = isMissile;
            Origin = origin;
            Direction = direction;
        }
    }
}
