using System;
using UnityEngine;
using AegisProtocol.Combat;

namespace AegisProtocol.Core
{
    /// <summary>Follows the mech with an isometric camera and exposes shake/light controls.</summary>
    [DisallowMultipleComponent]
    public sealed class IsometricCameraRig : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private Transform target;
        [SerializeField] private MechController mechController;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField, Range(45f, 55f)] private float pitchAngle = 50f;
        [SerializeField] private float yawAngle = 45f;
        [SerializeField, Min(1f)] private float followDistance = 18f;
        [SerializeField, Min(0f)] private float targetHeightOffset = 1.2f;
        [SerializeField, Min(0f)] private float aimLeadOffset = 0f;
        [SerializeField, Min(0.01f)] private float smoothTime = 0.08f;
        [SerializeField] private bool useOrthographicProjection = true;
        [SerializeField, Min(1f)] private float orthographicSize = 12f;

        [Header("Tactical Light")]
        [SerializeField] private Light tacticalSpotlight = null;
        [SerializeField] private bool tacticalLightStartsEnabled = false;

        private Camera controlledCamera;
        private Vector3 smoothVelocity;
        private Vector3 smoothedPosition;
        private float shakeRemaining;
        private float shakeDuration;
        private float shakeIntensity;
        private Vector3 currentShakeOffset;
        private bool hasInitialPosition;

        public event Action<bool> OnTacticalLightChanged;
        public Transform Target => target;
        public Light TacticalSpotlight => tacticalSpotlight;
        public bool TacticalLightEnabled => tacticalSpotlight != null && tacticalSpotlight.enabled;
        public float AimLeadOffset
        {
            get => aimLeadOffset;
            set => aimLeadOffset = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            controlledCamera = GetComponent<Camera>();
            if (mechController == null && target != null)
                mechController = target.GetComponentInChildren<MechController>();
            if (target == null && mechController != null)
                target = mechController.transform;
            if (inputReader == null && target != null)
                inputReader = target.GetComponentInParent<PlayerInputReader>();

            if (controlledCamera != null && useOrthographicProjection)
            {
                controlledCamera.orthographic = true;
                controlledCamera.orthographicSize = orthographicSize;
            }

            if (tacticalSpotlight != null)
                tacticalSpotlight.enabled = tacticalLightStartsEnabled;
        }

        private void Update()
        {
            if (inputReader == null || !CanUsePlayerInput())
                return;

            if (inputReader.CurrentSnapshot.TacticalLightTogglePressed)
                SetTacticalLightEnabled(!TacticalLightEnabled);
        }

        private void LateUpdate()
        {
            if (controlledCamera != null)
                controlledCamera.rect = new Rect(0f, 0f, 1f, 1f);

            if (target == null)
                return;

            Vector3 focusPoint = target.position + Vector3.up * targetHeightOffset;
            Vector3 aimDirection = mechController != null
                ? mechController.AimDirection
                : target.forward;
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude > 0.0001f)
                focusPoint += aimDirection.normalized * aimLeadOffset;

            Quaternion cameraRotation = Quaternion.Euler(pitchAngle, yawAngle, 0f);
            Vector3 desiredPosition = focusPoint + cameraRotation * (Vector3.back * followDistance);

            if (!hasInitialPosition)
            {
                smoothedPosition = desiredPosition;
                hasInitialPosition = true;
            }
            else
            {
                smoothedPosition = Vector3.SmoothDamp(
                    smoothedPosition,
                    desiredPosition,
                    ref smoothVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    Time.deltaTime);
            }

            transform.position = smoothedPosition + GetShakeOffset(Time.deltaTime);
            transform.rotation = cameraRotation;
        }

        private void OnValidate()
        {
            pitchAngle = Mathf.Clamp(pitchAngle, 45f, 55f);
            followDistance = Mathf.Max(1f, followDistance);
            targetHeightOffset = Mathf.Max(0f, targetHeightOffset);
            aimLeadOffset = Mathf.Max(0f, aimLeadOffset);
            smoothTime = Mathf.Max(0.01f, smoothTime);
            orthographicSize = Mathf.Max(1f, orthographicSize);
        }

        /// <summary>Connects targets built at runtime without Inspector assignments.</summary>
        public void ConfigureRuntime(Transform followTarget, MechController controller, PlayerInputReader reader, Light spotlight)
        {
            if (followTarget != null)
                target = followTarget;
            if (controller != null)
                mechController = controller;
            if (reader != null)
                inputReader = reader;
            if (spotlight != null)
            {
                tacticalSpotlight = spotlight;
                tacticalSpotlight.enabled = tacticalLightStartsEnabled;
            }
        }

        /// <summary>Requests a decaying positional shake, suitable for hits and explosions.</summary>
        public void TriggerCameraShake(float intensity = 0.18f, float duration = 0.14f)
        {
            if (intensity <= 0f || duration <= 0f ||
                float.IsNaN(intensity) || float.IsInfinity(intensity) ||
                float.IsNaN(duration) || float.IsInfinity(duration))
            {
                return;
            }

            shakeIntensity = Mathf.Max(shakeIntensity, intensity);
            shakeDuration = Mathf.Max(shakeDuration, duration);
            shakeRemaining = Mathf.Max(shakeRemaining, duration);
        }

        /// <summary>Enables or disables the assigned mech tactical spotlight.</summary>
        public void SetTacticalLightEnabled(bool enabled)
        {
            if (tacticalSpotlight == null || tacticalSpotlight.enabled == enabled)
                return;

            tacticalSpotlight.enabled = enabled;
            OnTacticalLightChanged?.Invoke(enabled);
        }

        public void ToggleTacticalLight()
        {
            SetTacticalLightEnabled(!TacticalLightEnabled);
        }

        private bool CanUsePlayerInput()
        {
            GameManager manager = GameManager.Instance;
            return manager == null ||
                   manager.CurrentState == GameState.WaveActive ||
                   manager.CurrentState == GameState.Briefing;
        }

        private Vector3 GetShakeOffset(float deltaTime)
        {
            if (shakeRemaining <= 0f)
            {
                currentShakeOffset = Vector3.zero;
                return currentShakeOffset;
            }

            // A paused game must freeze the current shake rather than generate frame-by-frame jitter.
            if (deltaTime <= 0f)
                return currentShakeOffset;

            float fade = shakeDuration <= 0f ? 0f : shakeRemaining / shakeDuration;
            currentShakeOffset = UnityEngine.Random.insideUnitSphere * (shakeIntensity * fade);
            shakeRemaining = Mathf.Max(0f, shakeRemaining - deltaTime);
            if (shakeRemaining <= 0f)
            {
                shakeDuration = 0f;
                shakeIntensity = 0f;
                currentShakeOffset = Vector3.zero;
            }

            return currentShakeOffset;
        }
    }
}
