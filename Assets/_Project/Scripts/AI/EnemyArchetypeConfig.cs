using UnityEngine;

namespace AegisProtocol.AI
{
    public enum EnemyType
    {
        ScoutDrone,
        RangedStriker,
        KamikazeDrone,
        HeavySiegeMech,
        OverlordTitanBoss
    }

    /// <summary>Designer-authored combat stats and presentation hints for an enemy archetype.</summary>
    [CreateAssetMenu(fileName = "EnemyArchetype", menuName = "Aegis Protocol/Enemy Archetype")]
    public sealed class EnemyArchetypeConfig : ScriptableObject
    {
        [SerializeField] private EnemyType enemyType;
        [SerializeField, Min(1f)] private float maxHealth = 75f;
        [SerializeField, Min(0f)] private float moveSpeed = 4.5f;
        [SerializeField, Min(0f)] private float attackRange = 1.8f;
        [SerializeField, Min(0f)] private float threatDetectionRange = 12f;
        [SerializeField, Min(0f)] private float attackDamage = 10f;
        [SerializeField, Min(0.05f)] private float attackCooldown = 1.5f;
        [SerializeField, Min(0)] private int scoreReward = 50;
        [SerializeField, Min(0)] private int crystalDropAmount = 5;
        [SerializeField, Min(0.1f)] private float scale = 1f;
        [SerializeField] private Color visualColor = new Color(0.15f, 0.85f, 1f);
        [SerializeField] private GameObject attackProjectilePrefab = null;
        [SerializeField, Min(0f)] private float rangedProjectileSpeed = 16f;
        [SerializeField, Min(0f)] private float explosionRadius = 4f;

        public EnemyType Type => enemyType;
        public float MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public float AttackRange => attackRange;
        public float ThreatDetectionRange => threatDetectionRange;
        public float AttackDamage => attackDamage;
        public float AttackCooldown => attackCooldown;
        public int ScoreReward => scoreReward;
        public int CrystalDropAmount => crystalDropAmount;
        public float Scale => scale;
        public Color VisualColor => visualColor;
        public GameObject AttackProjectilePrefab => attackProjectilePrefab;
        public float RangedProjectileSpeed => rangedProjectileSpeed;
        public float ExplosionRadius => explosionRadius;
        public bool IsBoss => enemyType == EnemyType.OverlordTitanBoss;

        public void Configure(
            EnemyType type,
            float health,
            float movementSpeed,
            float range,
            float damage,
            float cooldown,
            int score,
            int crystalDrop,
            float modelScale,
            Color color)
        {
            enemyType = type;
            maxHealth = Mathf.Max(1f, health);
            moveSpeed = Mathf.Max(0f, movementSpeed);
            attackRange = Mathf.Max(0f, range);
            attackDamage = Mathf.Max(0f, damage);
            attackCooldown = Mathf.Max(0.05f, cooldown);
            scoreReward = Mathf.Max(0, score);
            crystalDropAmount = Mathf.Max(0, crystalDrop);
            scale = Mathf.Max(0.1f, modelScale);
            visualColor = color;
        }

        public void SetRuntimeProjectilePrefab(GameObject prefab)
        {
            attackProjectilePrefab = prefab;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            attackRange = Mathf.Max(0f, attackRange);
            threatDetectionRange = Mathf.Max(0f, threatDetectionRange);
            attackCooldown = Mathf.Max(0.05f, attackCooldown);
            scoreReward = Mathf.Max(0, scoreReward);
            crystalDropAmount = Mathf.Max(0, crystalDropAmount);
            scale = Mathf.Max(0.1f, scale);
            rangedProjectileSpeed = Mathf.Max(0f, rangedProjectileSpeed);
            explosionRadius = Mathf.Max(0f, explosionRadius);

            switch (enemyType)
            {
                case EnemyType.ScoutDrone:
                    scoreReward = 50;
                    break;
                case EnemyType.RangedStriker:
                    scoreReward = 100;
                    break;
                case EnemyType.HeavySiegeMech:
                    scoreReward = 250;
                    break;
                case EnemyType.OverlordTitanBoss:
                    scoreReward = 1500;
                    break;
            }
        }
    }
}
