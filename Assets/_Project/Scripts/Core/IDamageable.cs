using UnityEngine;

namespace AegisProtocol.Core
{
    /// <summary>Common health contract for mech, reactor, and enemy targets.</summary>
    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsDead { get; }

        /// <param name="amount">Positive damage to apply.</param>
        /// <param name="instigator">Optional object responsible for the damage.</param>
        void TakeDamage(float amount, GameObject instigator = null);
    }
}
