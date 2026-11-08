using System;
using UnityEngine;
using UnityEngine.Rendering;
using AegisProtocol.Core;

namespace AegisProtocol.Systems
{
    /// <summary>Tracks a persistent 30-second satellite hack within its signal radius.</summary>
    [DisallowMultipleComponent]
    public sealed class SatelliteTerminalObjective : MonoBehaviour, IInteractable
    {
        private const float TerminalScoreReward = 500f;

        [SerializeField] private string terminalId = "Terminal_North";
        [SerializeField, Min(0.1f)] private float hackRadius = 5.5f;
        [SerializeField, Min(0.1f)] private float requiredHackDuration = 30f;
        [SerializeField] private Renderer[] statusRenderers = Array.Empty<Renderer>();
        [SerializeField] private Light[] statusLights = Array.Empty<Light>();
        [SerializeField] private Color inactiveColor = new Color(1f, 0.35f, 0.08f);
        [SerializeField] private Color activeColor = new Color(0.1f, 0.9f, 1f);
        [SerializeField] private Color completedColor = new Color(0.2f, 1f, 0.35f);

        private HealthComponent playerMech;
        private float progressSeconds;
        private float nextPlayerSearchTime;
        private bool hackActivated;
        private bool isCompleted;

        public event Action<float, float> OnTerminalProgressChanged;
        public event Action<SatelliteTerminalObjective> OnTerminalCompleted;
        public string TerminalId => terminalId;
        public float ProgressSeconds => progressSeconds;
        public float RequiredHackDuration => requiredHackDuration;
        public float ProgressNormalized => requiredHackDuration <= 0f ? 1f : Mathf.Clamp01(progressSeconds / requiredHackDuration);
        public bool IsCompleted => isCompleted;
        public bool IsHackActivated => hackActivated;

        public string InteractionPrompt => isCompleted
            ? $"{terminalId} - Signal restored"
            : $"{terminalId} - Hack signal ({Mathf.RoundToInt(ProgressNormalized * 100f)}%)";

        private void Awake()
        {
            ApplyVisualState(inactiveColor, false);
        }

        private void Update()
        {
            if (isCompleted || !CanProgressInCurrentState())
                return;

            ResolvePlayerMech();
            if (playerMech == null || playerMech.IsDead)
                return;

            float distance = Vector3.Distance(transform.position, playerMech.transform.position);
            if (distance <= hackRadius)
                hackActivated = true;
            if (!hackActivated || distance > hackRadius)
                return;

            progressSeconds = Mathf.Min(requiredHackDuration, progressSeconds + Time.deltaTime);
            OnTerminalProgressChanged?.Invoke(progressSeconds, requiredHackDuration);
            if (progressSeconds >= requiredHackDuration)
                CompleteHack();
        }

        public bool CanInteract(GameObject interactor)
        {
            return !isCompleted;
        }

        /// <summary>Manually arms the terminal; entering its signal radius also starts it.</summary>
        public void Interact(GameObject interactor)
        {
            if (!isCompleted)
                hackActivated = true;
        }

        private void ResolvePlayerMech()
        {
            if (playerMech != null && !playerMech.IsDead && Time.time < nextPlayerSearchTime)
                return;

            nextPlayerSearchTime = Time.time + 1f;
            HealthComponent[] components = FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].Role == HealthRole.PlayerMech && !components[i].IsDead)
                {
                    playerMech = components[i];
                    return;
                }
            }
            playerMech = null;
        }

        private bool CanProgressInCurrentState()
        {
            GameManager manager = GameManager.Instance;
            return manager == null || manager.CurrentState == GameState.WaveActive ||
                   manager.CurrentState == GameState.Briefing;
        }

        private void CompleteHack()
        {
            if (isCompleted)
                return;

            isCompleted = true;
            progressSeconds = requiredHackDuration;
            ApplyVisualState(completedColor, true);
            GameManager manager = GameManager.Instance;
            if (manager != null && manager.RegisterTerminalCompleted(terminalId))
                manager.AddScore(Mathf.RoundToInt(TerminalScoreReward));

            OnTerminalProgressChanged?.Invoke(progressSeconds, requiredHackDuration);
            OnTerminalCompleted?.Invoke(this);
        }

        private void ApplyVisualState(Color color, bool fullyActive)
        {
            if (statusRenderers != null)
            {
                for (int i = 0; i < statusRenderers.Length; i++)
                {
                    if (statusRenderers[i] != null && statusRenderers[i].material != null)
                        statusRenderers[i].material.color = color;
                }
            }
            if (statusLights != null)
            {
                for (int i = 0; i < statusLights.Length; i++)
                {
                    if (statusLights[i] == null)
                        continue;
                    statusLights[i].color = color;
                    statusLights[i].intensity = fullyActive ? 2.5f : 1f;
                    statusLights[i].enabled = true;
                }
            }
        }

        private void OnValidate()
        {
            hackRadius = Mathf.Max(0.1f, hackRadius);
            requiredHackDuration = Mathf.Max(0.1f, requiredHackDuration);
            if (string.IsNullOrWhiteSpace(terminalId))
                terminalId = "Terminal_North";
        }
    }
}
