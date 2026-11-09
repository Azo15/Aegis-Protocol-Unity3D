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
        [SerializeField, Range(10f, 65f)] private float pitchAngle = 24f;
        [SerializeField] private float yawAngle = 0f;
        [SerializeField, Min(1f)] private float followDistance = 12.5f;
        [SerializeField, Min(0f)] private float targetHeightOffset = 1.4f;
        [SerializeField, Min(0f)] private float aimLeadOffset = 0f;
        [SerializeField, Min(0.01f)] private float smoothTime = 0.06f;
        [SerializeField] private bool useOrthographicProjection = false;
        [SerializeField, Min(1f)] private float orthographicSize = 12f;
        [SerializeField, Range(40f, 80f)] private float perspectiveFieldOfView = 58f;
        [SerializeField, Min(10f)] private float orbitSpeedDegrees = 95f;

        [Header("Tactical Light")]
        [SerializeField] private Light tacticalSpotlight = null;
        [SerializeField] private bool tacticalLightStartsEnabled = false;

        private Camera controlledCamera;
        private Vector3 smoothVelocity;
        private Vector3 smoothedPosition;
        private float zoomMultiplier = 1f;
        private float cockpitYawAngle;
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
        public bool IsCockpitMode => currentViewMode == CameraViewMode.TankCockpitFPS;
        public float YawAngle => IsCockpitMode ? cockpitYawAngle : yawAngle;

        public string CurrentViewModeDisplayName
        {
            get
            {
                switch (currentViewMode)
                {
                    case CameraViewMode.ThirdPerson3D:
                        return "3B YAKIN TAKİP (TPS)";
                    case CameraViewMode.TankCockpitFPS:
                        return "TANK İÇİ KOKPİT (FPS)";
                    case CameraViewMode.LowCinematic3D:
                        return "3B ALÇAK SİNEMATİK";
                    case CameraViewMode.TacticalOverview3D:
                        return "3B GENİŞ KUŞBAKIŞI";
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
            if (Input.GetMouseButton(2))
                orbitInput += Input.GetAxis("Mouse X") * 2.4f;

            if (Mathf.Abs(orbitInput) > 0.001f)
            {
                yawAngle = Mathf.Repeat(yawAngle + orbitInput * orbitSpeedDegrees * Time.deltaTime, 360f);
                cockpitYawAngle = Mathf.Repeat(cockpitYawAngle + orbitInput * orbitSpeedDegrees * Time.deltaTime, 360f);
            }

            if (IsCockpitMode)
            {
                float mouseDeltaX = Input.GetAxis("Mouse X");
                if (Mathf.Abs(mouseDeltaX) > 0.0005f)
                    cockpitYawAngle = Mathf.Repeat(cockpitYawAngle + mouseDeltaX * 3.2f, 360f);
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f && !IsCockpitMode)
                zoomMultiplier = Mathf.Clamp(zoomMultiplier - scroll * 0.85f, 0.55f, 1.65f);
        }

        private void LateUpdate()
        {
            if (controlledCamera != null)
                controlledCamera.rect = new Rect(0f, 0f, 1f, 1f);

            if (target == null)
                return;

            ApplyProjectionSettings();

            if (IsCockpitMode)
            {
                UpdateCockpitView();
                return;
            }

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

        private void UpdateCockpitView()
        {
            Vector3 lookForward = Quaternion.Euler(0f, cockpitYawAngle, 0f) * Vector3.forward;
            Vector3 cockpitPosition = target.position + Vector3.up * 1.92f - lookForward * 0.32f;
            Quaternion cockpitRotation = Quaternion.LookRotation(lookForward + Vector3.down * 0.12f, Vector3.up);
            smoothedPosition = cockpitPosition;
            transform.position = cockpitPosition + GetShakeOffset(Time.deltaTime) * 0.30f;
            transform.rotation = cockpitRotation;
        }

        private void GetModeCameraParameters(out float modePitch, out float modeDistance, out float modeHeight)
        {
            switch (currentViewMode)
            {
                case CameraViewMode.ThirdPerson3D:
                    modePitch = 23f;
                    modeDistance = 12.0f;
                    modeHeight = 1.6f;
                    break;
                case CameraViewMode.LowCinematic3D:
                    modePitch = 13f;
                    modeDistance = 7.8f;
                    modeHeight = 1.45f;
                    break;
                case CameraViewMode.TacticalOverview3D:
                    modePitch = 48f;
                    modeDistance = 24f;
                    modeHeight = 1.2f;
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
                controlledCamera.fieldOfView = IsCockpitMode ? 66f : perspectiveFieldOfView;
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
            if (mode == CameraViewMode.TankCockpitFPS && mechController != null)
            {
                Vector3 dir = mechController.AimDirection;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    cockpitYawAngle = Quaternion.LookRotation(dir.normalized, Vector3.up).eulerAngles.y;
            }
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
