using UnityEngine;

namespace AegisProtocol.Combat
{
    /// <summary>Destroys transient visual effects after their lifetime to release runtime resources.</summary>
    [DisallowMultipleComponent]
    public sealed class TimedEffectDestroyer : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float lifetime = 1f;
        private float remainingLifetime;
        private bool initialized;

        public float Lifetime => lifetime;

        private void Awake()
        {
            if (!initialized)
                remainingLifetime = Mathf.Max(0.01f, lifetime);
        }

        private void Update()
        {
            remainingLifetime -= Time.deltaTime;
            if (remainingLifetime <= 0f)
                Destroy(gameObject);
        }

        /// <summary>Overrides the lifetime just after the effect is instantiated.</summary>
        public void Initialize(float duration)
        {
            lifetime = Mathf.Max(0.01f, duration);
            remainingLifetime = lifetime;
            initialized = true;
        }

        private void OnValidate()
        {
            lifetime = Mathf.Max(0.01f, lifetime);
        }
    }
}
