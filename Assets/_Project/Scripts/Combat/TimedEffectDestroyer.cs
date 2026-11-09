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
        private bool animateExpansion;
        private Vector3 startScale = Vector3.one;
        private Vector3 endScale = Vector3.one;
        private Light attachedLight;
        private float initialLightIntensity;
        private Material ownedMaterial;

        public float Lifetime => lifetime;

        private void Awake()
        {
            if (!initialized)
                remainingLifetime = Mathf.Max(0.01f, lifetime);
        }

        private void Update()
        {
            remainingLifetime -= Time.deltaTime;
            if (animateExpansion && lifetime > 0.001f)
            {
                float progress = Mathf.Clamp01(1f - (remainingLifetime / lifetime));
                transform.localScale = Vector3.Lerp(startScale, endScale, progress);
                if (attachedLight != null)
                    attachedLight.intensity = Mathf.Lerp(initialLightIntensity, 0f, progress);
            }

            if (remainingLifetime <= 0f)
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (ownedMaterial != null)
                Destroy(ownedMaterial);
        }

        /// <summary>Overrides the lifetime just after the effect is instantiated.</summary>
        public void Initialize(float duration)
        {
            lifetime = Mathf.Max(0.01f, duration);
            remainingLifetime = lifetime;
            initialized = true;
        }

        public void InitializeExpandingExplosion(
            float duration,
            Vector3 fromScale,
            Vector3 toScale,
            Light lightComponent = null,
            Material runtimeMaterial = null)
        {
            Initialize(duration);
            animateExpansion = true;
            startScale = fromScale;
            endScale = toScale;
            attachedLight = lightComponent;
            initialLightIntensity = lightComponent != null ? lightComponent.intensity : 0f;
            ownedMaterial = runtimeMaterial;
            transform.localScale = fromScale;
        }

        private void OnValidate()
        {
            lifetime = Mathf.Max(0.01f, lifetime);
        }
    }
}
