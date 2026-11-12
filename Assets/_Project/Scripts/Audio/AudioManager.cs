using System;
using System.Collections.Generic;
using UnityEngine;
using AegisProtocol.AI;
using AegisProtocol.Combat;
using AegisProtocol.Core;
using AegisProtocol.Systems;

namespace AegisProtocol.Audio
{
    /// <summary>Plays external or synthesized sci-fi audio and routes gameplay events to cues.</summary>
    [DefaultExecutionOrder(1200)]
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        [Header("Optional authored clips")]
        [SerializeField] private AudioClip backgroundMusic = null;
        [SerializeField] private AudioClip plasmaShot = null;
        [SerializeField] private AudioClip missileLaunch = null;
        [SerializeField] private AudioClip overheatWarning = null;
        [SerializeField] private AudioClip enemyDestroyed = null;
        [SerializeField] private AudioClip crystalCollected = null;
        [SerializeField] private AudioClip structureBuilt = null;
        [SerializeField] private AudioClip terminalCompleted = null;
        [SerializeField] private AudioClip uiClick = null;
        [SerializeField] private AudioClip victoryJingle = null;
        [SerializeField] private AudioClip defeatJingle = null;
        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.8f;

        private readonly List<AudioClip> generatedClips = new List<AudioClip>();
        private AudioClip[] levelShotClips = Array.Empty<AudioClip>();
        private AudioClip[] levelExplosionClips = Array.Empty<AudioClip>();
        private AudioClip castleUnderAttackClip;
        private float lastCastleAlertTime = -10f;
        private AudioSource musicSource;
        private AudioSource sfxSource;
        private GameManager gameManager;
        private WeaponSystem weaponSystem;
        private MechController mechController;
        private TacticalBuildManager buildManager;
        private SatelliteTerminalObjective[] terminals = Array.Empty<SatelliteTerminalObjective>();
        private CrystalResourceBank resourceBank;
        private bool subscribed;

        public static AudioManager Instance { get; private set; }
        public float MasterVolume => masterVolume;
        public float BgmVolume => bgmVolume;
        public float SfxVolume => sfxVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            CreateAudioSources();
            CreateMissingClips();
            ApplyVolumes();
        }

        private void Start()
        {
            BindGameplayEvents();
            if (musicSource != null && backgroundMusic != null)
            {
                musicSource.clip = backgroundMusic;
                musicSource.Play();
            }
        }

        private void OnDisable()
        {
            UnbindGameplayEvents();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            for (int i = 0; i < generatedClips.Count; i++)
            {
                if (generatedClips[i] != null)
                    Destroy(generatedClips[i]);
            }
            generatedClips.Clear();
        }

        public void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }

        public void SetBgmVolume(float value)
        {
            bgmVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }

        public void SetSfxVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }

        public void PlayUiClick() => Play(uiClick);

        public void PlayCastleUnderAttackAlert()
        {
            if (Time.unscaledTime - lastCastleAlertTime < 1.6f)
                return;

            lastCastleAlertTime = Time.unscaledTime;
            Play(castleUnderAttackClip != null ? castleUnderAttackClip : overheatWarning);
        }

        public void PlayLevelExplosion(int levelTier, bool isMissile)
        {
            int idx = Mathf.Clamp(levelTier, 1, 5) - 1;
            if (levelExplosionClips != null && idx < levelExplosionClips.Length && levelExplosionClips[idx] != null)
            {
                Play(levelExplosionClips[idx]);
                return;
            }
            Play(isMissile ? missileLaunch : enemyDestroyed);
        }

        public void Play(AudioClip clip)
        {
            if (clip != null && sfxSource != null && sfxVolume * masterVolume > 0f)
                sfxSource.PlayOneShot(clip);
        }

        private void BindGameplayEvents()
        {
            if (subscribed)
                return;

            gameManager = GameManager.Instance;
            weaponSystem = FindFirstObjectByType<WeaponSystem>();
            mechController = FindFirstObjectByType<MechController>();
            buildManager = FindFirstObjectByType<TacticalBuildManager>();
            terminals = FindObjectsByType<SatelliteTerminalObjective>(FindObjectsSortMode.None);
            resourceBank = CrystalResourceBank.Instance;

            if (gameManager != null)
                gameManager.OnGameStateChanged += HandleGameStateChanged;
            if (weaponSystem != null)
            {
                weaponSystem.OnWeaponFired += HandleWeaponFired;
                weaponSystem.OnOverheatStarted += HandleOverheatStarted;
            }
            if (mechController != null)
                mechController.OnDashStarted += HandleDashStarted;
            if (buildManager != null)
                buildManager.OnStructureBuilt += HandleStructureBuilt;
            for (int i = 0; i < terminals.Length; i++)
            {
                if (terminals[i] != null)
                    terminals[i].OnTerminalCompleted += HandleTerminalCompleted;
            }
            if (resourceBank != null)
                resourceBank.OnCrystalBalanceChanged += HandleCrystalBalanceChanged;

            EnemyAIController.OnAnyEnemyDied += HandleEnemyDestroyed;
            subscribed = true;
        }

        private void UnbindGameplayEvents()
        {
            if (!subscribed)
                return;

            if (gameManager != null)
                gameManager.OnGameStateChanged -= HandleGameStateChanged;
            if (weaponSystem != null)
            {
                weaponSystem.OnWeaponFired -= HandleWeaponFired;
                weaponSystem.OnOverheatStarted -= HandleOverheatStarted;
            }
            if (mechController != null)
                mechController.OnDashStarted -= HandleDashStarted;
            if (buildManager != null)
                buildManager.OnStructureBuilt -= HandleStructureBuilt;
            for (int i = 0; i < terminals.Length; i++)
            {
                if (terminals[i] != null)
                    terminals[i].OnTerminalCompleted -= HandleTerminalCompleted;
            }
            if (resourceBank != null)
                resourceBank.OnCrystalBalanceChanged -= HandleCrystalBalanceChanged;
            EnemyAIController.OnAnyEnemyDied -= HandleEnemyDestroyed;
            subscribed = false;
        }

        private void HandleGameStateChanged(GameStateChangedEvent change)
        {
            if (change.CurrentState == GameState.Victory)
                Play(victoryJingle);
            else if (change.CurrentState == GameState.Defeat)
                Play(defeatJingle);
        }

        private void HandleWeaponFired(WeaponFireEvent fireEvent)
        {
            int currentLevel = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : 1;
            int idx = Mathf.Clamp(currentLevel, 1, 5) - 1;
            if (!fireEvent.IsMissile && levelShotClips != null && idx < levelShotClips.Length && levelShotClips[idx] != null)
            {
                Play(levelShotClips[idx]);
                return;
            }
            Play(fireEvent.IsMissile ? missileLaunch : plasmaShot);
        }

        private void HandleOverheatStarted() => Play(overheatWarning);
        private void HandleDashStarted() => Play(plasmaShot);
        private void HandleStructureBuilt(DefenseTowerController structure) => Play(structureBuilt);
        private void HandleTerminalCompleted(SatelliteTerminalObjective terminal) => Play(terminalCompleted);
        private void HandleEnemyDestroyed(EnemyAIController enemy) => Play(enemyDestroyed);

        private void HandleCrystalBalanceChanged(int balance, int delta)
        {
            if (delta > 0)
                Play(crystalCollected);
        }

        private void CreateAudioSources()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;

            GameObject sfxObject = new GameObject("SFX Audio Source");
            sfxObject.transform.SetParent(transform, false);
            sfxSource = sfxObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }

        private void CreateMissingClips()
        {
            if (backgroundMusic == null)
                backgroundMusic = CreateSynthClip("Aegis Synthwave Loop", 4f, 55f, 110f, 0.12f, true);
            if (plasmaShot == null)
                plasmaShot = CreateSynthClip("Plasma Shot", 0.22f, 760f, 180f, 0.32f, false);
            if (missileLaunch == null)
                missileLaunch = CreateSynthClip("Missile Launch", 0.42f, 420f, 95f, 0.34f, false);
            if (overheatWarning == null)
                overheatWarning = CreateSynthClip("Overheat Warning", 0.48f, 980f, 650f, 0.28f, false);
            castleUnderAttackClip = CreateSynthClip("Castle Alert Siren", 0.36f, 740f, 1120f, 0.30f, false);
            if (enemyDestroyed == null)
                enemyDestroyed = CreateSynthClip("Enemy Destruction", 0.32f, 280f, 65f, 0.35f, false);
            if (crystalCollected == null)
                crystalCollected = CreateSynthClip("Crystal Pickup", 0.2f, 880f, 1320f, 0.24f, false);
            if (structureBuilt == null)
                structureBuilt = CreateSynthClip("Structure Built", 0.3f, 330f, 660f, 0.25f, false);
            if (terminalCompleted == null)
                terminalCompleted = CreateSynthClip("Terminal Complete", 0.72f, 440f, 880f, 0.3f, false);
            if (uiClick == null)
                uiClick = CreateSynthClip("UI Click", 0.08f, 620f, 520f, 0.18f, false);
            if (victoryJingle == null)
                victoryJingle = CreateSynthClip("Victory Jingle", 1.25f, 440f, 880f, 0.3f, false);
            if (defeatJingle == null)
                defeatJingle = CreateSynthClip("Defeat Jingle", 1.1f, 220f, 82f, 0.3f, false);

            levelShotClips = new[]
            {
                CreateSynthClip("Shot L1", 0.18f, 820f, 240f, 0.30f, false),
                CreateSynthClip("Shot L2 Twin", 0.22f, 680f, 190f, 0.34f, false),
                CreateSynthClip("Shot L3 Triple", 0.25f, 920f, 160f, 0.36f, false),
                CreateSynthClip("Shot L4 Quad", 0.28f, 540f, 110f, 0.38f, false),
                CreateSynthClip("Shot L5 Omega", 0.34f, 480f, 85f, 0.42f, false)
            };

            levelExplosionClips = new[]
            {
                CreateSynthClip("Explosion L1", 0.24f, 260f, 70f, 0.28f, false),
                CreateSynthClip("Explosion L2", 0.30f, 220f, 58f, 0.33f, false),
                CreateSynthClip("Explosion L3", 0.36f, 310f, 50f, 0.36f, false),
                CreateSynthClip("Explosion L4", 0.44f, 180f, 40f, 0.40f, false),
                CreateSynthClip("Explosion L5 Omega", 0.55f, 150f, 32f, 0.45f, false)
            };
        }

        private AudioClip CreateSynthClip(
            string clipName,
            float duration,
            float startFrequency,
            float endFrequency,
            float amplitude,
            bool stereo)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(duration * sampleRate));
            int channels = stereo ? 2 : 1;
            float[] samples = new float[sampleCount * channels];
            for (int frame = 0; frame < sampleCount; frame++)
            {
                float time = frame / (float)sampleRate;
                float normalizedTime = time / duration;
                float phase = 2f * Mathf.PI *
                              (startFrequency * time + (endFrequency - startFrequency) * time * time / (2f * duration));
                float envelope = stereo
                    ? 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.25f * time)
                    : Mathf.Min(1f, time * 35f) * Mathf.Min(1f, (duration - time) * 20f);
                float tone = Mathf.Sin(phase) * 0.72f + Mathf.Sin(phase * 2.01f) * 0.18f;
                if (stereo)
                    tone += Mathf.Sin(phase * 0.5f) * 0.1f;
                float value = Mathf.Clamp(tone * amplitude * envelope, -1f, 1f);
                for (int channel = 0; channel < channels; channel++)
                    samples[frame * channels + channel] = value;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, channels, sampleRate, false);
            clip.SetData(samples, 0);
            generatedClips.Add(clip);
            return clip;
        }

        private void ApplyVolumes()
        {
            if (musicSource != null)
                musicSource.volume = masterVolume * bgmVolume;
            if (sfxSource != null)
                sfxSource.volume = masterVolume * sfxVolume;
        }
    }
}
