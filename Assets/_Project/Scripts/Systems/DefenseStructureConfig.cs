using UnityEngine;

namespace AegisProtocol.Systems
{
    public enum DefenseStructureType
    {
        AutoLaserTurret,
        EmpSlowTower,
        RepairStation
    }

    /// <summary>Designer-authored cost, combat and utility settings for a defense structure.</summary>
    [CreateAssetMenu(fileName = "DefenseStructureConfig", menuName = "Aegis Protocol/Defense Structure Config")]
    public sealed class DefenseStructureConfig : ScriptableObject
    {
        [SerializeField] private DefenseStructureType structureType;
        [SerializeField, Min(0)] private int crystalCost = 50;
        [SerializeField, Min(0f)] private float range = 12f;
        [SerializeField, Min(0f)] private float damagePerShot = 12f;
        [SerializeField, Min(0.05f)] private float shotsPerSecond = 1.5f;
        [SerializeField, Min(0.1f)] private float empRadius = 8f;
        [SerializeField, Range(0.05f, 1f)] private float empSpeedMultiplier = 0.55f;
        [SerializeField, Min(0f)] private float empSlowDuration = 2.5f;
        [SerializeField, Min(0.05f)] private float empPulseInterval = 2.5f;
        [SerializeField, Min(0f)] private float repairPerSecond = 20f;
        [SerializeField, Min(0f)] private float repairCapacityPerWave = 250f;
        [SerializeField] private Color visualColor = new Color(0.1f, 0.85f, 1f);
        [SerializeField] private GameObject projectilePrefab = null;

        public DefenseStructureType StructureType => structureType;
        public int CrystalCost => crystalCost;
        public float Range => range;
        public float DamagePerShot => damagePerShot;
        public float ShotsPerSecond => shotsPerSecond;
        public float EmpRadius => empRadius;
        public float EmpSpeedMultiplier => empSpeedMultiplier;
        public float EmpSlowDuration => empSlowDuration;
        public float EmpPulseInterval => empPulseInterval;
        public float RepairPerSecond => repairPerSecond;
        public float RepairCapacityPerWave => repairCapacityPerWave;
        public Color VisualColor => visualColor;
        public GameObject ProjectilePrefab => projectilePrefab;

        public void Configure(DefenseStructureType type)
        {
            structureType = type;
            switch (type)
            {
                case DefenseStructureType.AutoLaserTurret:
                    crystalCost = 50;
                    range = 12f;
                    damagePerShot = 12f;
                    shotsPerSecond = 1.5f;
                    visualColor = new Color(0.1f, 0.85f, 1f);
                    break;
                case DefenseStructureType.EmpSlowTower:
                    crystalCost = 75;
                    empRadius = 8f;
                    empSpeedMultiplier = 0.55f;
                    empSlowDuration = 2.5f;
                    empPulseInterval = 2.5f;
                    visualColor = new Color(0.65f, 0.25f, 1f);
                    break;
                case DefenseStructureType.RepairStation:
                    crystalCost = 100;
                    range = 8f;
                    repairPerSecond = 20f;
                    repairCapacityPerWave = 250f;
                    visualColor = new Color(0.2f, 1f, 0.35f);
                    break;
            }
        }

        private void OnValidate()
        {
            crystalCost = Mathf.Max(0, crystalCost);
            range = Mathf.Max(0f, range);
            damagePerShot = Mathf.Max(0f, damagePerShot);
            shotsPerSecond = Mathf.Max(0.05f, shotsPerSecond);
            empRadius = Mathf.Max(0.1f, empRadius);
            empSpeedMultiplier = Mathf.Clamp(empSpeedMultiplier, 0.05f, 1f);
            empSlowDuration = Mathf.Max(0f, empSlowDuration);
            empPulseInterval = Mathf.Max(0.05f, empPulseInterval);
            repairPerSecond = Mathf.Max(0f, repairPerSecond);
            repairCapacityPerWave = Mathf.Max(0f, repairCapacityPerWave);
        }
    }
}
