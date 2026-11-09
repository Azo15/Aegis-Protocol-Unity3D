using System;
using System.Collections.Generic;
using UnityEngine;

namespace AegisProtocol.Core
{
    /// <summary>Owns the game state, run clock, score, and mission completion checks.</summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Mission Rules")]
        [SerializeField, Min(1)] private int totalWaves = 5;
        [SerializeField, Min(1)] private int requiredTerminals = 3;
        [Tooltip("A soft session target used by the HUD; reaching zero does not cause defeat.")]
        [SerializeField, Min(1f)] private float targetRunDurationSeconds = 900f;

        [Header("Mission Objectives")]
        [SerializeField] private HealthComponent mechHealth;
        [SerializeField] private HealthComponent reactorHealth;

        private const string MaxUnlockedLevelPrefKey = "Aegis_MaxUnlockedLevel";

        private readonly HashSet<string> completedTerminalIds = new HashSet<string>(StringComparer.Ordinal);
        private HealthComponent subscribedMechHealth;
        private HealthComponent subscribedReactorHealth;
        private GameState state = GameState.MainMenu;
        private GameState stateBeforePause = GameState.MainMenu;
        private int currentLevel = 1;
        private int maxUnlockedLevel = 1;
        private int currentWave;
        private int completedWaves;
        private int totalScore;
        private float elapsedSeconds;
        private float nextSnapshotTime;
        private bool finalBossDefeated;

        public event Action<GameStateChangedEvent> OnGameStateChanged;
        public event Action<int> OnScoreChanged;
        public event Action<GameStateSnapshot> OnSessionSnapshotChanged;

        public GameState CurrentState => state;
        public int CurrentLevel => currentLevel;
        public int MaxUnlockedLevel => maxUnlockedLevel;
        public int TotalLevels => SectorLevelConfig.TotalLevels;
        public SectorLevelData CurrentLevelData => SectorLevelConfig.GetLevel(currentLevel);
        public bool HasNextLevel => currentLevel < SectorLevelConfig.TotalLevels;
        public int CurrentWave => currentWave;
        public int CompletedWaves => completedWaves;
        public int TotalWaves => totalWaves;
        public int CompletedTerminals => completedTerminalIds.Count;
        public int RequiredTerminals => requiredTerminals;
        public int TotalScore => totalScore;
        public float ElapsedSeconds => elapsedSeconds;
        public float RemainingSeconds => Mathf.Max(0f, targetRunDurationSeconds - elapsedSeconds);
        public bool FinalBossDefeated => finalBossDefeated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            totalWaves = Mathf.Max(1, totalWaves);
            requiredTerminals = Mathf.Max(1, requiredTerminals);
            maxUnlockedLevel = Mathf.Clamp(PlayerPrefs.GetInt(MaxUnlockedLevelPrefKey, 1), 1, SectorLevelConfig.TotalLevels);
            currentLevel = Mathf.Clamp(currentLevel, 1, maxUnlockedLevel);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
                UnlockAllLevels();

            if (state != GameState.WaveActive)
                return;

            elapsedSeconds += Time.deltaTime;
            float regen = CurrentLevelData.PassiveRegenPerSecond;
            if (regen > 0f)
            {
                if (mechHealth != null && !mechHealth.IsDead)
                    mechHealth.Heal(regen * Time.deltaTime);
                if (reactorHealth != null && !reactorHealth.IsDead)
                    reactorHealth.Heal(regen * 0.5f * Time.deltaTime);
            }

            if (Time.unscaledTime >= nextSnapshotTime)
            {
                nextSnapshotTime = Time.unscaledTime + 0.25f;
                PublishSnapshot();
            }

            CheckObjectiveFailure();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            UnbindObjectiveEvents();
            Time.timeScale = 1f;
            Instance = null;
        }

        private void OnApplicationQuit()
        {
            Time.timeScale = 1f;
        }

        public void ConfigureObjectives(HealthComponent playerMech, HealthComponent centralReactor)
        {
            UnbindObjectiveEvents();
            if (playerMech != null)
                mechHealth = playerMech;
            if (centralReactor != null)
                reactorHealth = centralReactor;
            BindObjectiveEvents();
        }

        /// <summary>Finds objective health components for a newly opened gameplay scene.</summary>
        public void InitializeForScene()
        {
            UnbindObjectiveEvents();

            HealthComponent[] sceneHealth = FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            foreach (HealthComponent health in sceneHealth)
            {
                if (health == null)
                    continue;

                if (health.Role == HealthRole.PlayerMech)
                    mechHealth = health;
                else if (health.Role == HealthRole.Reactor)
                    reactorHealth = health;
            }

            if (mechHealth != null && mechHealth.IsDead)
                mechHealth.Initialize();
            if (reactorHealth != null && reactorHealth.IsDead)
                reactorHealth.Initialize();

            BindObjectiveEvents();
            if (mechHealth == null || reactorHealth == null)
            {
                Debug.LogWarning(
                    "GameManager needs both a PlayerMech and Reactor HealthComponent " +
                    "(or explicit Inspector references) before it can evaluate mission endings.", this);
            }

            PublishSnapshot();
        }

        public bool IsLevelUnlocked(int levelNumber)
        {
            return levelNumber >= 1 && levelNumber <= maxUnlockedLevel;
        }

        public bool SelectLevel(int levelNumber)
        {
            if (!IsLevelUnlocked(levelNumber))
                return false;

            currentLevel = Mathf.Clamp(levelNumber, 1, SectorLevelConfig.TotalLevels);
            return true;
        }

        public void UnlockAllLevels()
        {
            maxUnlockedLevel = SectorLevelConfig.TotalLevels;
            PlayerPrefs.SetInt(MaxUnlockedLevelPrefKey, maxUnlockedLevel);
            PlayerPrefs.Save();
            PublishSnapshot();
        }

        public void ResetLevelProgression()
        {
            maxUnlockedLevel = 1;
            currentLevel = 1;
            PlayerPrefs.SetInt(MaxUnlockedLevelPrefKey, 1);
            PlayerPrefs.Save();
            PublishSnapshot();
        }

        /// <summary>Starts a fresh run and opens the mission briefing state.</summary>
        public void StartRun()
        {
            Time.timeScale = 1f;
            currentWave = 0;
            completedWaves = 0;
            totalScore = 0;
            elapsedSeconds = 0f;
            finalBossDefeated = false;
            completedTerminalIds.Clear();
            if (mechHealth != null)
                mechHealth.Initialize();
            if (reactorHealth != null)
                reactorHealth.Initialize();
            OnScoreChanged?.Invoke(totalScore);
            SetState(GameState.Briefing);
            PublishSnapshot();
        }

        /// <summary>Begins the next expected wave from briefing or upgrade selection.</summary>
        public bool StartWave(int waveNumber)
        {
            if (state != GameState.Briefing && state != GameState.WaveUpgradeSelection)
                return false;

            int expectedWave = completedWaves + 1;
            if (waveNumber != expectedWave || waveNumber < 1 || waveNumber > totalWaves)
                return false;

            currentWave = waveNumber;
            SetState(GameState.WaveActive);
            PublishSnapshot();
            return true;
        }

        /// <summary>Completes the current wave once and advances to upgrade selection when applicable.</summary>
        public bool CompleteCurrentWave()
        {
            if (state != GameState.WaveActive || currentWave <= completedWaves)
                return false;

            completedWaves = currentWave;
            if (completedWaves < totalWaves)
                SetState(GameState.WaveUpgradeSelection);
            else
                TryCompleteVictory();

            PublishSnapshot();
            return true;
        }

        /// <summary>Records a terminal exactly once. IDs should be stable scene identifiers.</summary>
        public bool RegisterTerminalCompleted(string terminalId)
        {
            if (string.IsNullOrWhiteSpace(terminalId))
                return false;

            if (!completedTerminalIds.Add(terminalId.Trim()))
                return false;

            TryCompleteVictory();
            PublishSnapshot();
            return true;
        }

        /// <summary>Marks the fifth-wave boss defeated and closes that wave.</summary>
        public bool RegisterFinalBossDefeated()
        {
            if (state != GameState.WaveActive || currentWave != totalWaves || finalBossDefeated)
                return false;

            finalBossDefeated = true;
            TryCompleteVictory();
            PublishSnapshot();
            return true;
        }

        /// <summary>Adds positive points, saturating at Int32.MaxValue.</summary>
        public void AddScore(int points)
        {
            if (points <= 0)
                return;

            totalScore = (int)Math.Min(int.MaxValue, (long)totalScore + points);
            OnScoreChanged?.Invoke(totalScore);
            PublishSnapshot();
        }

        public bool Pause()
        {
            if (state != GameState.WaveActive && state != GameState.Briefing)
                return false;

            stateBeforePause = state;
            SetState(GameState.Paused);
            return true;
        }

        public bool Resume()
        {
            if (state != GameState.Paused)
                return false;

            SetState(stateBeforePause);
            return true;
        }

        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            SetState(GameState.MainMenu);
            PublishSnapshot();
        }

        /// <summary>Returns a value snapshot suitable for HUD and result screens.</summary>
        public GameStateSnapshot CreateSnapshot()
        {
            return new GameStateSnapshot
            {
                CurrentState = state,
                CurrentWave = currentWave,
                CompletedWaves = completedWaves,
                CompletedTerminals = CompletedTerminals,
                TotalScore = totalScore,
                ElapsedSeconds = elapsedSeconds,
                RemainingSeconds = RemainingSeconds,
                FinalBossDefeated = finalBossDefeated
            };
        }

        private void BindObjectiveEvents()
        {
            if (mechHealth != null)
            {
                subscribedMechHealth = mechHealth;
                subscribedMechHealth.OnDeath += HandleObjectiveDeath;
            }

            if (reactorHealth != null && reactorHealth != mechHealth)
            {
                subscribedReactorHealth = reactorHealth;
                subscribedReactorHealth.OnDeath += HandleObjectiveDeath;
            }
        }

        private void UnbindObjectiveEvents()
        {
            if (subscribedMechHealth != null)
                subscribedMechHealth.OnDeath -= HandleObjectiveDeath;
            if (subscribedReactorHealth != null)
                subscribedReactorHealth.OnDeath -= HandleObjectiveDeath;

            subscribedMechHealth = null;
            subscribedReactorHealth = null;
        }

        private void HandleObjectiveDeath(HealthComponent target, GameObject instigator)
        {
            if (target == mechHealth || target == reactorHealth)
                SetState(GameState.Defeat);
        }

        private void CheckObjectiveFailure()
        {
            if ((mechHealth != null && mechHealth.IsDead) ||
                (reactorHealth != null && reactorHealth.IsDead))
            {
                SetState(GameState.Defeat);
            }
        }

        private bool TryCompleteVictory()
        {
            bool objectivesAlive = mechHealth != null && reactorHealth != null &&
                                   !mechHealth.IsDead && !reactorHealth.IsDead;
            bool missionComplete = completedWaves >= totalWaves &&
                                   CompletedTerminals >= requiredTerminals &&
                                   finalBossDefeated;

            if (!objectivesAlive || !missionComplete)
                return false;

            if (currentLevel >= maxUnlockedLevel && currentLevel < SectorLevelConfig.TotalLevels)
            {
                maxUnlockedLevel = Mathf.Min(SectorLevelConfig.TotalLevels, currentLevel + 1);
                PlayerPrefs.SetInt(MaxUnlockedLevelPrefKey, maxUnlockedLevel);
                PlayerPrefs.Save();
            }

            SetState(GameState.Victory);
            return true;
        }

        private void SetState(GameState nextState)
        {
            if (state == nextState)
                return;

            GameState previousState = state;
            state = nextState;
            Time.timeScale = nextState == GameState.Paused ||
                             nextState == GameState.WaveUpgradeSelection ||
                             nextState == GameState.Victory ||
                             nextState == GameState.Defeat
                ? 0f
                : 1f;

            OnGameStateChanged?.Invoke(new GameStateChangedEvent(previousState, nextState));
            PublishSnapshot();
        }

        private void PublishSnapshot()
        {
            OnSessionSnapshotChanged?.Invoke(CreateSnapshot());
        }
    }
}
