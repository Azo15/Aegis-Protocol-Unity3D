using System;
using UnityEngine;
using AegisProtocol.UI;

namespace AegisProtocol.Core
{
    /// <summary>Identifies an objective health source to the session manager.</summary>
    public enum HealthRole
    {
        Generic,
        PlayerMech,
        Reactor,
        Enemy,
        Boss
    }

    /// <summary>Reusable health component for the player mech, reactor, and enemies.</summary>
    [DisallowMultipleComponent]
    public sealed class HealthComponent : MonoBehaviour, IDamageable
    {
        public const float ReactorDefaultMaxHealth = 1000f;

        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private HealthRole role = HealthRole.Generic;

        private float currentHealth;
        private bool initialized;

        /// <summary>Raised after current or maximum health changes.</summary>
        public event Action<HealthComponent, float, float> OnHealthChanged;
        /// <summary>Raised when positive damage is applied.</summary>
        public event Action<HealthComponent, float, GameObject> OnDamaged;
        /// <summary>Raised once when health first reaches zero.</summary>
        public event Action<HealthComponent, GameObject> OnDeath;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0f;
        public HealthRole Role => role;
        public float HealthRatio => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

        private void Awake()
        {
            if (role == HealthRole.Reactor)
                maxHealth = ReactorDefaultMaxHealth;

            Initialize();
        }

        private void Start()
        {
            if (Application.isPlaying && GetComponent<WorldHealthBar>() == null)
            {
                gameObject.AddComponent<WorldHealthBar>();
            }
        }

        private void OnValidate()
        {
            maxHealth = role == HealthRole.Reactor
                ? ReactorDefaultMaxHealth
                : Mathf.Max(1f, maxHealth);

            if (initialized)
                currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        }

        /// <summary>Initializes health; an optional value is clamped to the configured maximum.</summary>
        public void Initialize(float? startingHealth = null)
        {
            maxHealth = role == HealthRole.Reactor
                ? ReactorDefaultMaxHealth
                : Mathf.Max(1f, maxHealth);
            currentHealth = Mathf.Clamp(startingHealth ?? maxHealth, 0f, maxHealth);
            initialized = true;
        }

        public void TakeDamage(float amount, GameObject instigator = null)
        {
            if (!initialized)
                Initialize();

            if (IsDead || amount <= 0f)
                return;

            float previousHealth = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            float appliedDamage = previousHealth - currentHealth;

            if (appliedDamage <= 0f)
                return;

            OnDamaged?.Invoke(this, appliedDamage, instigator);
            OnHealthChanged?.Invoke(this, previousHealth, currentHealth);

            if (IsDead)
                OnDeath?.Invoke(this, instigator);
        }

        /// <summary>Assigns a gameplay role and maximum health before objective registration.</summary>
        public void Configure(HealthRole newRole, float configuredMaxHealth, bool refillCurrentHealth = true)
        {
            float previousHealth = currentHealth;
            role = newRole;
            maxHealth = role == HealthRole.Reactor
                ? ReactorDefaultMaxHealth
                : Mathf.Max(1f, configuredMaxHealth);

            if (!initialized || refillCurrentHealth)
                currentHealth = maxHealth;
            else
                currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

            initialized = true;
            if (!Mathf.Approximately(previousHealth, currentHealth))
                OnHealthChanged?.Invoke(this, previousHealth, currentHealth);
        }
        /// <summary>Restores health without exceeding the configured maximum.</summary>
        public void Heal(float amount)
        {
            if (!initialized)
                Initialize();

            if (IsDead || amount <= 0f)
                return;

            float previousHealth = currentHealth;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            if (!Mathf.Approximately(previousHealth, currentHealth))
                OnHealthChanged?.Invoke(this, previousHealth, currentHealth);
        }

        /// <summary>Changes the maximum health and optionally fills the component.</summary>
        public void SetMaxHealth(float newMaxHealth, bool refillCurrentHealth = true)
        {
            if (role == HealthRole.Reactor)
                newMaxHealth = ReactorDefaultMaxHealth;

            newMaxHealth = Mathf.Max(1f, newMaxHealth);
            float previousHealth = currentHealth;
            maxHealth = newMaxHealth;
            currentHealth = refillCurrentHealth
                ? maxHealth
                : Mathf.Clamp(currentHealth, 0f, maxHealth);

            if (!Mathf.Approximately(previousHealth, currentHealth))
                OnHealthChanged?.Invoke(this, previousHealth, currentHealth);
        }
    }
}

