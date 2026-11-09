using System;
using UnityEngine;

namespace AegisProtocol.Systems
{
    /// <summary>Stores the player's spendable energy crystal balance.</summary>
    [DisallowMultipleComponent]
    public sealed class CrystalResourceBank : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingBalance = 150;
        [SerializeField, Min(0.1f)] private float pickupRadiusMultiplier = 1f;
        private int currentBalance;

        public static CrystalResourceBank Instance { get; private set; }
        public int CurrentBalance => currentBalance;
        public float PickupRadiusMultiplier => pickupRadiusMultiplier;
        public event Action<int, int> OnCrystalBalanceChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Instance.gameObject == gameObject)
                {
                    Destroy(this);
                    return;
                }
            }

            Instance = this;
            currentBalance = Mathf.Max(0, startingBalance);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ConfigureStartingBalance(int balance, float pickupMultiplier = 1f)
        {
            startingBalance = Mathf.Max(0, balance);
            currentBalance = startingBalance;
            pickupRadiusMultiplier = Mathf.Clamp(pickupMultiplier, 0.1f, 10f);
            OnCrystalBalanceChanged?.Invoke(currentBalance, 0);
        }

        public bool TrySpendCrystals(int amount)
        {
            if (amount < 0 || currentBalance < amount)
                return false;

            currentBalance -= amount;
            OnCrystalBalanceChanged?.Invoke(currentBalance, -amount);
            return true;
        }

        public void AddCrystals(int amount)
        {
            if (amount <= 0)
                return;

            int previousBalance = currentBalance;
            currentBalance = (int)Math.Min(int.MaxValue, (long)currentBalance + amount);
            OnCrystalBalanceChanged?.Invoke(currentBalance, currentBalance - previousBalance);
        }

        /// <summary>Stacks a positive magnetic pickup range upgrade.</summary>
        public void ApplyPickupRadiusMultiplier(float multiplier)
        {
            if (multiplier <= 0f || float.IsNaN(multiplier) || float.IsInfinity(multiplier))
                return;

            pickupRadiusMultiplier = Mathf.Clamp(pickupRadiusMultiplier * multiplier, 0.1f, 10f);
        }

        private void OnValidate()
        {
            startingBalance = Mathf.Max(0, startingBalance);
            pickupRadiusMultiplier = Mathf.Max(0.1f, pickupRadiusMultiplier);
        }
    }
}
