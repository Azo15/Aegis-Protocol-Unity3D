using System;
using UnityEngine;
using AegisProtocol.Combat;

namespace AegisProtocol.Core
{
    /// <summary>Selectable 3D camera perspectives including inside-tank cockpit view.</summary>
    public enum CameraViewMode
    {
        ThirdPerson3D = 0,
        TankCockpitFPS = 1,
        LowCinematic3D = 2,
        TacticalOverview3D = 3
    }

    /// <summary>Follows the mech with switchable 3D perspective, cockpit FPS, and orbit/zoom controls.</summary>
    [DisallowMultipleComponent]
    public sealed class IsometricCameraRig : MonoBehaviour
    {
        [Header("Follow")]
        [SerializeField] private Transform target;
        [SerializeField] private MechController mechController;
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private CameraViewMode currentViewMode = CameraViewMode.ThirdPerson3D;
        [SerializeField, Range(10f, 65f)] private float pitchAngle = 34f;
        [SerializeField] private float yawAngle = 0f;
        [SerializeField, Min(1f)] private float followDistance = 19.5f;
        [SerializeField, Min(0f)] private float targetHeightOffset = 1.6f;
        [SerializeField, Min(0f)] private float aimLeadOffset = 0f;
        [SerializeField, Min(0.01f)] private float smoothTime = 0.06f;
        [SerializeField] private bool useOrthographicProjection = false;
        [SerializeField, Min(1f)] private float orthographicSize = 12f;
        [SerializeField, Range(40f, 80f)] private float perspectiveFieldOfView = 60f;
        [SerializeField, Min(10f)] private float orbitSpeedDegrees = 85f;

        [Header("Tactical Light")]
        [SerializeField] private Light tacticalSpotlight = null;
        [SerializeField] private bool tacticalLightStartsEnabled = false;

        private Camera controlledCamera;
        private Vector3 smoothVelocity;
        private Vector3 smoothedPosition;
        private float zoomMultiplier = 1f;
        private float shakeRemaining;
        private float shakeDuration;
        private float shakeIntensity;
        private Vector3 currentShakeOffset;
        private bool hasInitialPosition;

        public event Action<bool> OnTacticalLightChanged;
        public event Action<CameraViewMode> OnCameraViewModeChanged;

        public Transform Target => target;
        public Light TacticalSpotlight => tacticalSpotlight;
        public bool TacticalLightEnabled => tacticalSpotlight != null && tacticalSpotlight.enabled;
        public CameraViewMode CurrentViewMode => currentViewMode;
        public bool IsCockpitMode => false;
        public float YawAngle => yawAngle;

        public string CurrentViewModeDisplayName
        {
            get
            {
                switch (currentViewMode)
                {
                    case CameraViewMode.ThirdPerson3D:
                        return "DENGELİ 3B SAVAŞ AÇISI";
                    case CameraViewMode.TankCockpitFPS:
                        return "TANK ARKASI YAKIN 3B";
                    case CameraViewMode.LowCinematic3D:
                        return "ORTA MESAFE 3B TAKİP";
                    case CameraViewMode.TacticalOverview3D:
                        return "GENİŞ KUŞBAKIŞI 3B";
                    default:
                        return "3B KAMERA";
                }
            }
        }

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

            ApplyProjectionSettings();

            if (tacticalSpotlight != null)
                tacticalSpotlight.enabled = tacticalLightStartsEnabled;
        }

        private void Update()
        {
            if (!CanUsePlayerInput())
                return;

            if (inputReader != null && inputReader.CurrentSnapshot.TacticalLightTogglePressed)
                SetTacticalLightEnabled(!TacticalLightEnabled);

            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.V))
                CycleCameraMode();

            float orbitInput = 0f;
            if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow))
                orbitInput -= 1f;
            if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.RightArrow))
                orbitInput += 1f;

            if (Mathf.Abs(orbitInput) > 0.001f)
                yawAngle = Mathf.Repeat(yawAngle + orbitInput * orbitSpeedDegrees * Time.deltaTime, 360f);

            float zoomDelta = 0f;
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
                zoomDelta -= scroll * 0.95f;
            if (Input.GetKey(KeyCode.UpArrow))
                zoomDelta -= 0.55f * Time.deltaTime;
            if (Input.GetKey(KeyCode.DownArrow))
                zoomDelta += 0.55f * Time.deltaTime;

            if (Mathf.Abs(zoomDelta) > 0.0001f)
                zoomMultiplier = Mathf.Clamp(zoomMultiplier + zoomDelta, 0.60f, 1.85f);
        }

        private void LateUpdate()
        {
            if (controlledCamera != null)
                controlledCamera.rect = new Rect(0f, 0f, 1f, 1f);

            if (target == null)
                return;

            ApplyProjectionSettings();

            GetModeCameraParameters(out float modePitch, out float modeDistance, out float modeHeight);
            Vector3 focusPoint = target.position + Vector3.up * modeHeight;
            Quaternion cameraRotation = Quaternion.Euler(modePitch, yawAngle, 0f);
            Vector3 desiredPosition = focusPoint + cameraRotation * (Vector3.back * (modeDistance * zoomMultiplier));

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

        private void GetModeCameraParameters(out float modePitch, out float modeDistance, out float modeHeight)
        {
            switch (currentViewMode)
            {
                case CameraViewMode.ThirdPerson3D:
                    modePitch = 34f;
                    modeDistance = 19.5f;
                    modeHeight = 1.6f;
                    break;
                case CameraViewMode.TankCockpitFPS:
                    modePitch = 19f;
                    modeDistance = 9.2f;
                    modeHeight = 2.0f;
                    break;
                case CameraViewMode.LowCinematic3D:
                    modePitch = 26f;
                    modeDistance = 14.0f;
                    modeHeight = 1.75f;
                    break;
                case CameraViewMode.TacticalOverview3D:
                    modePitch = 50f;
                    modeDistance = 27.5f;
                    modeHeight = 1.3f;
                    break;
                default:
                    modePitch = pitchAngle;
                    modeDistance = followDistance;
                    modeHeight = targetHeightOffset;
                    break;
            }
        }

        private void ApplyProjectionSettings()
        {
            if (controlledCamera == null)
                return;

            controlledCamera.orthographic = useOrthographicProjection;
            if (useOrthographicProjection)
                controlledCamera.orthographicSize = orthographicSize;
            else
                controlledCamera.fieldOfView = perspectiveFieldOfView;
        }

        public void CycleCameraMode()
        {
            int next = ((int)currentViewMode + 1) % 4;
            SetCameraViewMode((CameraViewMode)next);
        }

        public void SetCameraViewMode(CameraViewMode mode)
        {
            currentViewMode = mode;
            useOrthographicProjection = false;
            ApplyProjectionSettings();
            OnCameraViewModeChanged?.Invoke(currentViewMode);
        }

        private void OnValidate()
        {
            pitchAngle = Mathf.Clamp(pitchAngle, 10f, 65f);
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
            useOrthographicProjection = false;
            ApplyProjectionSettings();
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
