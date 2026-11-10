using UnityEngine;
using AegisProtocol.Core;

namespace AegisProtocol.Systems
{
    /// <summary>Magnetically attracts nearby crystals to the player mech, then credits the bank.</summary>
    [DisallowMultipleComponent]
    public sealed class EnergyCrystalPickup : MonoBehaviour
    {
        [SerializeField, Min(1)] private int crystalAmount = 10;
        [SerializeField, Min(0.1f)] private float pickupRadius = 4.5f;
        [SerializeField, Min(0.1f)] private float attractionSpeed = 12f;
        [SerializeField, Min(0f)] private float collectDistance = 0.45f;
        [SerializeField, Min(0.01f)] private float maxLifetime = 30f;

        private Transform playerTarget;
        private HealthComponent playerHealth;
        private CrystalResourceBank resourceBank;
        private float lifetimeRemaining;
        private bool isAttracted;

        public int CrystalAmount => crystalAmount;
        public bool IsAttracted => isAttracted;

        private void Awake()
        {
            lifetimeRemaining = maxLifetime;
        }

        private void Update()
        {
            lifetimeRemaining -= Time.deltaTime;
            if (lifetimeRemaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            ResolvePlayerTarget();
            if (playerTarget == null || resourceBank == null)
                return;

            float radius = pickupRadius * resourceBank.PickupRadiusMultiplier;
            Vector3 targetPoint = playerTarget.position;
            float distance = Vector3.Distance(transform.position, targetPoint);
            if (distance > radius)
            {
                isAttracted = false;
                return;
            }

            isAttracted = true;
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPoint,
                attractionSpeed * Time.deltaTime);
            if (distance <= collectDistance || Vector3.Distance(transform.position, targetPoint) <= collectDistance)
                Collect();
        }

        /// <summary>Sets the drop value immediately after Instantiate.</summary>
        public void Initialize(int amount)
        {
            crystalAmount = Mathf.Max(1, amount);
            lifetimeRemaining = maxLifetime;
            ResolvePlayerTarget();
        }

        private void ResolvePlayerTarget()
        {
            if (resourceBank == null)
                resourceBank = CrystalResourceBank.Instance;
            if (playerTarget != null)
                return;

            HealthComponent[] healthComponents = FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            for (int i = 0; i < healthComponents.Length; i++)
            {
                HealthComponent candidate = healthComponents[i];
                if (candidate != null && candidate.Role == HealthRole.PlayerMech && !candidate.IsDead)
                {
                    playerTarget = candidate.transform;
                    playerHealth = candidate;
                    break;
                }
            }
        }

        private void Collect()
        {
            if (resourceBank == null)
                return;

            resourceBank.AddCrystals(crystalAmount);
            if (playerHealth != null && !playerHealth.IsDead && playerHealth.CurrentHealth < playerHealth.MaxHealth)
            {
                playerHealth.Heal(25f);
            }
            Destroy(gameObject);
        }

        private void OnValidate()
        {
            crystalAmount = Mathf.Max(1, crystalAmount);
            pickupRadius = Mathf.Max(0.1f, pickupRadius);
            attractionSpeed = Mathf.Max(0.1f, attractionSpeed);
            collectDistance = Mathf.Max(0f, collectDistance);
            maxLifetime = Mathf.Max(0.01f, maxLifetime);
        }
    }
}
