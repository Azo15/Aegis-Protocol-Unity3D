using UnityEngine;

namespace AegisProtocol.Systems
{
    public enum UpgradeModuleType
    {
        PlasmaCoolant,
        TitaniumArmor,
        TurretOverclock,
        MagneticPull,
        OverchargedCapacitors,
        ThrusterBoost
    }

    /// <summary>Reusable definition for a Roguelite module card.</summary>
    [CreateAssetMenu(fileName = "UpgradeModuleCard", menuName = "Aegis Protocol/Upgrade Module Card")]
    public sealed class UpgradeModuleCardConfig : ScriptableObject
    {
        [SerializeField] private UpgradeModuleType moduleType;
        [SerializeField] private string displayName = "Plasma Coolant";
        [SerializeField, TextArea(2, 4)] private string description = "Reduces generated weapon heat by 25%.";
        [SerializeField] private Sprite icon = null;
        [SerializeField, Min(0f)] private float magnitude = 0.25f;

        public UpgradeModuleType ModuleType => moduleType;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public float Magnitude => magnitude;

        public void Configure(UpgradeModuleType type, string title, string details, float value)
        {
            moduleType = type;
            displayName = title;
            description = details;
            magnitude = Mathf.Max(0f, value);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = moduleType.ToString();
            magnitude = Mathf.Max(0f, magnitude);
        }
    }
}
