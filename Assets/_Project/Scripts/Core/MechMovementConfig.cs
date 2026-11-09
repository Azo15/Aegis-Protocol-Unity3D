using UnityEngine;

namespace AegisProtocol.Core
{
    /// <summary>Designer-tunable movement, dash, and arena settings for the player mech.</summary>
    [CreateAssetMenu(fileName = "MechMovementConfig", menuName = "Aegis Protocol/Mech Movement Config")]
    public sealed class MechMovementConfig : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 10.5f;
        [SerializeField, Min(0f)] private float acceleration = 42f;
        [SerializeField, Min(0f)] private float deceleration = 54f;
        [SerializeField, Min(0f)] private float chassisTurnSpeed = 12f;
        [SerializeField, Min(0f)] private float turretTurnSpeed = 22f;

        [Header("Dash")]
        [SerializeField, Min(0f)] private float dashSpeed = 22f;
        [SerializeField, Min(0.01f)] private float dashDuration = 0.22f;
        [SerializeField, Min(0f)] private float dashCooldown = 3f;

        [Header("Arena")]
        [SerializeField] private Bounds arenaBounds = new Bounds(Vector3.zero, new Vector3(100f, 40f, 100f));
        [SerializeField, Min(1f)] private float aimRayDistance = 500f;

        public float MoveSpeed => moveSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float ChassisTurnSpeed => chassisTurnSpeed;
        public float TurretTurnSpeed => turretTurnSpeed;
        public float DashSpeed => dashSpeed;
        public float DashDuration => dashDuration;
        public float DashCooldown => dashCooldown;
        public Bounds ArenaBounds => arenaBounds;
        public float AimRayDistance => aimRayDistance;

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            chassisTurnSpeed = Mathf.Max(0f, chassisTurnSpeed);
            turretTurnSpeed = Mathf.Max(0f, turretTurnSpeed);
            dashSpeed = Mathf.Max(0f, dashSpeed);
            dashDuration = Mathf.Max(0.01f, dashDuration);
            dashCooldown = Mathf.Max(0f, dashCooldown);
            aimRayDistance = Mathf.Max(1f, aimRayDistance);
            Vector3 size = arenaBounds.size;
            arenaBounds.size = new Vector3(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y), Mathf.Max(1f, size.z));
        }
    }
}
