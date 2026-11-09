using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AegisProtocol.AI;
using AegisProtocol.Core;

namespace AegisProtocol.Systems
{
    [Serializable]
    public sealed class EnemyPrefabDefinition
    {
        public EnemyType type;
        public EnemyAIController prefab = null;
        public EnemyArchetypeConfig config = null;
        public EnergyCrystalPickup crystalPrefab = null;
    }

    [Serializable]
    public sealed class WaveSpawnGroup
    {
        public EnemyType type;
        [Min(1)] public int count = 1;
        [Min(0f)] public float spawnInterval = 0.7f;
        [Tooltip("0 uses any spawn point, 1 uses north, 2 uses east.")]
        [Range(0, 2)] public int corridor = 0;
    }

    /// <summary>Spawns the five siege waves and awards no-damage and final-reactor bonuses.</summary>
    [DefaultExecutionOrder(1100)]
    [DisallowMultipleComponent]
    public sealed class WaveSpawnerManager : MonoBehaviour
    {
        private const int NoDamageWaveBonus = 300;
        private const float FinalReactorHealthBonusPerPoint = 10f;
        private const float NightIntensityMultiplier = 0.18f;

        [SerializeField] private EnemyPrefabDefinition[] enemyDefinitions = Array.Empty<EnemyPrefabDefinition>();
        [SerializeField] private Transform[] spawnPoints = Array.Empty<Transform>();
        [SerializeField] private Light directionalLight = null;
        [SerializeField] private IsometricCameraRig cameraRig = null;
        [SerializeField, Min(0f)] private float defaultSpawnInterval = 0.7f;
        [SerializeField, Min(0.1f)] private float nightIntensityMultiplier = NightIntensityMultiplier;
        [SerializeField] private WaveSpawnGroup[] wave1 = Array.Empty<WaveSpawnGroup>();
        [SerializeField] private WaveSpawnGroup[] wave2 = Array.Empty<WaveSpawnGroup>();
        [SerializeField] private WaveSpawnGroup[] wave3 = Array.Empty<WaveSpawnGroup>();
        [SerializeField] private WaveSpawnGroup[] wave4 = Array.Empty<WaveSpawnGroup>();
        [SerializeField] private WaveSpawnGroup[] wave5 = Array.Empty<WaveSpawnGroup>();

        private readonly HashSet<EnemyAIController> activeEnemies = new HashSet<EnemyAIController>();
        private GameManager gameManager;
        private HealthComponent reactorHealth;
        private Coroutine spawnRoutine;
        private int managedWave;
        private int activeSpawnCount;
        private bool isSpawning;
        private bool reactorDamagedThisWave;
        private bool subscribedToReactorDamage;
        private float savedLightIntensity;
        private bool savedLightEnabled;
        private bool savedTacticalLightEnabled;
        private bool nightLightingActive;

        public int ActiveEnemyCount => activeEnemies.Count;
        public int ManagedWave => managedWave;

        /// <summary>Supplies runtime-generated spawn data from AegisArenaRuntimeBuilder.</summary>
        public void ConfigureRuntime(
            EnemyPrefabDefinition[] definitions,
            Transform[] runtimeSpawnPoints,
            Light mainDirectionalLight,
            IsometricCameraRig runtimeCameraRig)
        {
            enemyDefinitions = definitions ?? Array.Empty<EnemyPrefabDefinition>();
            spawnPoints = runtimeSpawnPoints ?? Array.Empty<Transform>();
            if (mainDirectionalLight != null)
                directionalLight = mainDirectionalLight;
            if (runtimeCameraRig != null)
                cameraRig = runtimeCameraRig;
        }

        private void Start()
        {
            gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("WaveSpawnerManager requires a GameManager in the scene.", this);
                enabled = false;
                return;
            }

            gameManager.OnGameStateChanged += HandleGameStateChanged;
            reactorHealth = FindHealthRole(HealthRole.Reactor);
            if (directionalLight == null)
                directionalLight = FindDirectionalLight();
            if (cameraRig == null)
                cameraRig = FindFirstObjectByType<IsometricCameraRig>();

            if (gameManager.CurrentState == GameState.WaveActive)
                BeginWave(gameManager.CurrentWave);
        }

        private void OnDisable()
        {
            if (gameManager != null)
                gameManager.OnGameStateChanged -= HandleGameStateChanged;
            UnsubscribeFromReactorDamage();
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }
            RestoreNightLighting();
        }

        private void HandleGameStateChanged(GameStateChangedEvent change)
        {
            if (change.CurrentState == GameState.WaveActive && gameManager != null &&
                managedWave != gameManager.CurrentWave)
            {
                BeginWave(gameManager.CurrentWave);
            }
            else if (change.CurrentState != GameState.WaveActive &&
                     change.CurrentState != GameState.Paused && isSpawning)
            {
                StopCurrentSpawning();
            }
        }

        private void BeginWave(int waveNumber)
        {
            if (waveNumber < 1 || waveNumber > 5 || waveNumber == managedWave)
                return;

            StopCurrentSpawning();
            managedWave = waveNumber;
            activeEnemies.Clear();
            activeSpawnCount = 0;
            isSpawning = true;
            reactorDamagedThisWave = false;
            reactorHealth = reactorHealth != null ? reactorHealth : FindHealthRole(HealthRole.Reactor);
            if (reactorHealth != null)
            {
                reactorHealth.OnDamaged += HandleReactorDamaged;
                subscribedToReactorDamage = true;
            }

            if (waveNumber == 3)
                ApplyNightLighting();
            else
                RestoreNightLighting();

            WaveSpawnGroup[] groups = GetWaveGroups(waveNumber);
            if (groups == null || groups.Length == 0)
                groups = CreateDefaultComposition(waveNumber);
            spawnRoutine = StartCoroutine(SpawnGroups(groups));
        }

        private IEnumerator SpawnGroups(WaveSpawnGroup[] groups)
        {
            int requestedSpawns = 0;
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                WaveSpawnGroup group = groups[groupIndex];
                if (group == null || group.count <= 0)
                    continue;

                EnemyPrefabDefinition definition = FindDefinition(group.type);
                if (definition == null || definition.prefab == null)
                {
                    Debug.LogError($"No enemy prefab is assigned for {group.type}; wave spawning is blocked.", this);
                    continue;
                }

                for (int i = 0; i < group.count; i++)
                {
                    SpawnEnemy(definition, group.corridor);
                    requestedSpawns++;
                    float interval = group.spawnInterval > 0f ? group.spawnInterval : defaultSpawnInterval;
                    if (interval > 0f)
                        yield return new WaitForSeconds(interval);
                }
            }

            isSpawning = false;
            spawnRoutine = null;
            if (requestedSpawns == 0 || activeSpawnCount == 0)
            {
                Debug.LogError("No enemies were spawned. Assign prefabs and archetype configs in the Inspector.", this);
                yield break;
            }

            TryCompleteWave();
        }

        private void SpawnEnemy(EnemyPrefabDefinition definition, int corridor)
        {
            Transform spawnPoint = SelectSpawnPoint(corridor);
            if (spawnPoint == null)
            {
                Debug.LogError("WaveSpawnerManager needs at least one active spawn point.", this);
                return;
            }

            EnemyAIController enemy = Instantiate(definition.prefab, spawnPoint.position, spawnPoint.rotation);
            enemy.gameObject.SetActive(true);
            enemy.ConfigureRuntime(definition.config, definition.crystalPrefab);
            enemy.OnEnemyDied += HandleEnemyDied;
            activeEnemies.Add(enemy);
            activeSpawnCount++;
        }

        private void HandleEnemyDied(EnemyAIController enemy)
        {
            if (enemy != null)
                enemy.OnEnemyDied -= HandleEnemyDied;
            activeEnemies.Remove(enemy);
            if (!isSpawning)
                TryCompleteWave();
        }

        private void TryCompleteWave()
        {
            if (isSpawning || activeEnemies.Count > 0 || gameManager == null ||
                gameManager.CurrentState != GameState.WaveActive || gameManager.CurrentWave != managedWave)
            {
                return;
            }

            UnsubscribeFromReactorDamage();
            if (reactorHealth != null && !reactorDamagedThisWave)
                gameManager.AddScore(NoDamageWaveBonus);

            RestoreNightLighting();
            if (managedWave == 5 && reactorHealth != null && !reactorHealth.IsDead)
            {
                int remainingHealthBonus = Mathf.RoundToInt(reactorHealth.CurrentHealth * FinalReactorHealthBonusPerPoint);
                gameManager.AddScore(remainingHealthBonus);
            }

            gameManager.CompleteCurrentWave();
        }

        private void HandleReactorDamaged(HealthComponent target, float amount, GameObject instigator)
        {
            reactorDamagedThisWave = true;
        }

        private void ApplyNightLighting()
        {
            if (nightLightingActive)
                return;

            if (directionalLight != null)
            {
                savedLightIntensity = directionalLight.intensity;
                savedLightEnabled = directionalLight.enabled;
                directionalLight.intensity = savedLightIntensity * Mathf.Clamp01(nightIntensityMultiplier);
            }

            if (cameraRig != null)
            {
                savedTacticalLightEnabled = cameraRig.TacticalLightEnabled;
                cameraRig.SetTacticalLightEnabled(true);
            }

            nightLightingActive = true;
        }

        private void RestoreNightLighting()
        {
            if (!nightLightingActive)
                return;

            if (directionalLight != null)
            {
                directionalLight.intensity = savedLightIntensity;
                directionalLight.enabled = savedLightEnabled;
            }
            if (cameraRig != null)
                cameraRig.SetTacticalLightEnabled(savedTacticalLightEnabled);

            nightLightingActive = false;
        }

        private void StopCurrentSpawning()
        {
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }
            isSpawning = false;
            UnsubscribeFromReactorDamage();
        }

        private void UnsubscribeFromReactorDamage()
        {
            if (subscribedToReactorDamage && reactorHealth != null)
                reactorHealth.OnDamaged -= HandleReactorDamaged;
            subscribedToReactorDamage = false;
        }

        private EnemyPrefabDefinition FindDefinition(EnemyType type)
        {
            if (enemyDefinitions == null)
                return null;

            for (int i = 0; i < enemyDefinitions.Length; i++)
            {
                if (enemyDefinitions[i] != null && enemyDefinitions[i].type == type)
                    return enemyDefinitions[i];
            }
            return null;
        }

        private Transform SelectSpawnPoint(int corridor)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return null;

            if (corridor == 1 && spawnPoints.Length > 0)
                return spawnPoints[0];
            if (corridor == 2 && spawnPoints.Length > 1)
                return spawnPoints[1];

            return spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
        }

        private WaveSpawnGroup[] GetWaveGroups(int waveNumber)
        {
            switch (waveNumber)
            {
                case 1: return wave1;
                case 2: return wave2;
                case 3: return wave3;
                case 4: return wave4;
                case 5: return wave5;
                default: return null;
            }
        }

        private static WaveSpawnGroup[] CreateDefaultComposition(int waveNumber)
        {
            int bonus = GameManager.Instance != null
                ? GameManager.Instance.CurrentLevelData.ExtraEnemiesPerGroup
                : 0;
            switch (waveNumber)
            {
                case 1:
                    return new[] { Group(EnemyType.ScoutDrone, 8 + bonus, 0) };
                case 2:
                    return new[] { Group(EnemyType.ScoutDrone, 6 + bonus, 1), Group(EnemyType.RangedStriker, 4 + bonus, 2) };
                case 3:
                    return new[] { Group(EnemyType.KamikazeDrone, 8 + bonus, 1), Group(EnemyType.RangedStriker, 6 + bonus, 2) };
                case 4:
                    return new[]
                    {
                        Group(EnemyType.HeavySiegeMech, 3 + Mathf.Max(0, bonus / 2), 0),
                        Group(EnemyType.ScoutDrone, 5 + bonus, 0),
                        Group(EnemyType.RangedStriker, 4 + bonus, 2),
                        Group(EnemyType.KamikazeDrone, 3 + bonus, 1)
                    };
                case 5:
                    return new[]
                    {
                        Group(EnemyType.OverlordTitanBoss, 1, 0),
                        Group(EnemyType.ScoutDrone, 8 + bonus, 1),
                        Group(EnemyType.RangedStriker, 6 + bonus, 2)
                    };
                default:
                    return Array.Empty<WaveSpawnGroup>();
            }
        }

        private static WaveSpawnGroup Group(EnemyType type, int count, int corridor)
        {
            return new WaveSpawnGroup { type = type, count = count, corridor = corridor };
        }

        private static HealthComponent FindHealthRole(HealthRole role)
        {
            HealthComponent[] components = FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].Role == role)
                    return components[i];
            }

            return null;
        }

        private static Light FindDirectionalLight()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && lights[i].type == LightType.Directional)
                    return lights[i];
            }
            return null;
        }
    }
}
