using UnityEngine;

namespace AegisProtocol.Combat
{
    /// <summary>Designer-tunable values for the mech's primary and secondary weapons.</summary>
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "Aegis Protocol/Weapon Config")]
    public sealed class WeaponConfig : ScriptableObject
    {
        [Header("Primary: Plasma Cannon")]
        [SerializeField, Min(0f)] private float primaryDamage = 25f;
        [SerializeField, Min(0.01f)] private float primaryFireRate = 6f;
        [SerializeField, Min(0f)] private float primaryProjectileSpeed = 28f;
        [SerializeField, Min(0f)] private float primaryHeatPerShot = 8f;
        [SerializeField, Min(0.01f)] private float primaryLifetime = 3f;

        [Header("Secondary: Guided Micro-Missile")]
        [SerializeField, Min(0f)] private float missileDamage = 80f;
        [SerializeField, Min(0.1f)] private float missileExplosionRadius = 4f;
        [SerializeField, Min(0f)] private float missileCooldown = 2f;
        [SerializeField, Min(0f)] private float missileSpeed = 18f;
        [SerializeField, Min(0f)] private float missileTurnSpeed = 240f;
        [SerializeField, Min(0f)] private float missileHeatPerShot = 28f;
        [SerializeField, Min(0.01f)] private float missileLifetime = 4.5f;

        [Header("Heat")]
        [SerializeField, Min(1f)] private float maximumHeat = 100f;
        [SerializeField, Min(0f)] private float coolingRate = 22f;
        [SerializeField, Min(0f)] private float overheatLockoutDuration = 2.5f;

        public float PrimaryDamage => primaryDamage;
        public float PrimaryFireRate => primaryFireRate;
        public float PrimaryProjectileSpeed => primaryProjectileSpeed;
        public float PrimaryHeatPerShot => primaryHeatPerShot;
        public float PrimaryLifetime => primaryLifetime;
        public float MissileDamage => missileDamage;
        public float MissileExplosionRadius => missileExplosionRadius;
        public float MissileCooldown => missileCooldown;
        public float MissileSpeed => missileSpeed;
        public float MissileTurnSpeed => missileTurnSpeed;
        public float MissileHeatPerShot => missileHeatPerShot;
        public float MissileLifetime => missileLifetime;
        public float MaximumHeat => maximumHeat;
        public float CoolingRate => coolingRate;
        public float OverheatLockoutDuration => overheatLockoutDuration;

        private void OnValidate()
        {
            primaryDamage = Mathf.Max(0f, primaryDamage);
            primaryFireRate = Mathf.Max(0.01f, primaryFireRate);
            primaryProjectileSpeed = Mathf.Max(0f, primaryProjectileSpeed);
            primaryHeatPerShot = Mathf.Max(0f, primaryHeatPerShot);
            primaryLifetime = Mathf.Max(0.01f, primaryLifetime);
            missileDamage = Mathf.Max(0f, missileDamage);
            missileExplosionRadius = Mathf.Max(0.1f, missileExplosionRadius);
            missileCooldown = Mathf.Max(0f, missileCooldown);
            missileSpeed = Mathf.Max(0f, missileSpeed);
            missileTurnSpeed = Mathf.Max(0f, missileTurnSpeed);
            missileHeatPerShot = Mathf.Max(0f, missileHeatPerShot);
            missileLifetime = Mathf.Max(0.01f, missileLifetime);
            maximumHeat = Mathf.Max(1f, maximumHeat);
            coolingRate = Mathf.Max(0f, coolingRate);
            overheatLockoutDuration = Mathf.Max(0f, overheatLockoutDuration);
        }
    }
}
