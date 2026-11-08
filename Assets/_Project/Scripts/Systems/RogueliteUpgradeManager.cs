using System;
using System.Collections.Generic;
using UnityEngine;
using AegisProtocol.Combat;
using AegisProtocol.Core;

namespace AegisProtocol.Systems
{
    /// <summary>Presents three distinct upgrades between waves and applies the chosen module.</summary>
    [DefaultExecutionOrder(1101)]
    [DisallowMultipleComponent]
    public sealed class RogueliteUpgradeManager : MonoBehaviour
    {
        private static readonly UpgradeModuleType[] AllModuleTypes =
        {
            UpgradeModuleType.PlasmaCoolant,
            UpgradeModuleType.TitaniumArmor,
            UpgradeModuleType.TurretOverclock,
            UpgradeModuleType.MagneticPull,
            UpgradeModuleType.OverchargedCapacitors,
            UpgradeModuleType.ThrusterBoost
        };

        [SerializeField] private UpgradeModuleCardConfig[] cardPool = Array.Empty<UpgradeModuleCardConfig>();
        [SerializeField] private MechController mechController = null;
        [SerializeField] private WeaponSystem weaponSystem = null;
        [SerializeField] private CrystalResourceBank resourceBank = null;
        [SerializeField] private TacticalBuildManager buildManager = null;

        private readonly List<UpgradeModuleCardConfig> fallbackCards = new List<UpgradeModuleCardConfig>();
        private UpgradeModuleCardConfig[] offeredOptions = Array.Empty<UpgradeModuleCardConfig>();
        private GameManager gameManager;

        public event Action<UpgradeModuleCardConfig[]> OnUpgradeOptionsPresented;
        public event Action<UpgradeModuleCardConfig> OnUpgradeSelected;
        public IReadOnlyList<UpgradeModuleCardConfig> OfferedOptions => offeredOptions;

        private void Awake()
        {
            BuildFallbackCardPool();
            ResolveGameplaySystems();
        }

        private void Start()
        {
            gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                Debug.LogError("RogueliteUpgradeManager requires a GameManager.", this);
                enabled = false;
                return;
            }

            gameManager.OnGameStateChanged += HandleGameStateChanged;
            if (gameManager.CurrentState == GameState.WaveUpgradeSelection)
                PresentUpgradeOptions();
        }

        private void OnDisable()
        {
            if (gameManager != null)
                gameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < fallbackCards.Count; i++)
            {
                if (fallbackCards[i] != null)
                    Destroy(fallbackCards[i]);
            }
            fallbackCards.Clear();
        }

        /// <summary>Selects one of the currently presented cards and starts the next wave.</summary>
        public bool SelectUpgrade(int optionIndex)
        {
            if (gameManager == null || gameManager.CurrentState != GameState.WaveUpgradeSelection ||
                optionIndex < 0 || optionIndex >= offeredOptions.Length)
            {
                return false;
            }

            UpgradeModuleCardConfig selected = offeredOptions[optionIndex];
            ApplyUpgrade(selected);
            offeredOptions = Array.Empty<UpgradeModuleCardConfig>();
            OnUpgradeSelected?.Invoke(selected);
            return gameManager.StartWave(gameManager.CompletedWaves + 1);
        }

        public void PresentUpgradeOptions()
        {
            List<UpgradeModuleCardConfig> available = GetDistinctCardPool();
            int selectionCount = Mathf.Min(3, available.Count);
            for (int i = available.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                UpgradeModuleCardConfig temporary = available[i];
                available[i] = available[swapIndex];
                available[swapIndex] = temporary;
            }

            offeredOptions = available.GetRange(0, selectionCount).ToArray();
            OnUpgradeOptionsPresented?.Invoke((UpgradeModuleCardConfig[])offeredOptions.Clone());
        }

        private void HandleGameStateChanged(GameStateChangedEvent change)
        {
            if (change.CurrentState == GameState.WaveUpgradeSelection)
                PresentUpgradeOptions();
        }

        private void ApplyUpgrade(UpgradeModuleCardConfig card)
        {
            if (card == null)
                return;

            ResolveGameplaySystems();
            switch (card.ModuleType)
            {
                case UpgradeModuleType.PlasmaCoolant:
                    if (weaponSystem != null)
                        weaponSystem.ApplyHeatGenerationMultiplier(1f - Mathf.Clamp01(card.Magnitude));
                    break;
                case UpgradeModuleType.TitaniumArmor:
                    if (mechController != null && mechController.Health != null)
                    {
                        HealthComponent health = mechController.Health;
                        health.SetMaxHealth(health.MaxHealth + card.Magnitude, refillCurrentHealth: true);
                    }
                    break;
                case UpgradeModuleType.TurretOverclock:
                    if (buildManager != null)
                        buildManager.ApplyGlobalTurretFireRateMultiplier(card.Magnitude);
                    break;
                case UpgradeModuleType.MagneticPull:
                    if (resourceBank != null)
                        resourceBank.ApplyPickupRadiusMultiplier(card.Magnitude);
                    break;
                case UpgradeModuleType.OverchargedCapacitors:
                    if (weaponSystem != null)
                        weaponSystem.ApplyDamageMultiplier(card.Magnitude);
                    break;
                case UpgradeModuleType.ThrusterBoost:
                    if (mechController != null)
                        mechController.ApplyMoveSpeedMultiplier(card.Magnitude);
                    break;
            }
        }

        private void ResolveGameplaySystems()
        {
            if (mechController == null)
                mechController = FindFirstObjectByType<MechController>();
            if (weaponSystem == null)
                weaponSystem = FindFirstObjectByType<WeaponSystem>();
            if (resourceBank == null)
                resourceBank = CrystalResourceBank.Instance;
            if (buildManager == null)
                buildManager = FindFirstObjectByType<TacticalBuildManager>();
        }

        private List<UpgradeModuleCardConfig> GetDistinctCardPool()
        {
            List<UpgradeModuleCardConfig> distinct = new List<UpgradeModuleCardConfig>(AllModuleTypes.Length);
            for (int typeIndex = 0; typeIndex < AllModuleTypes.Length; typeIndex++)
            {
                UpgradeModuleType type = AllModuleTypes[typeIndex];
                UpgradeModuleCardConfig selected = FindCard(type);
                if (selected != null)
                    distinct.Add(selected);
            }
            return distinct;
        }

        private UpgradeModuleCardConfig FindCard(UpgradeModuleType type)
        {
            if (cardPool != null)
            {
                for (int i = 0; i < cardPool.Length; i++)
                {
                    if (cardPool[i] != null && cardPool[i].ModuleType == type)
                        return cardPool[i];
                }
            }

            for (int i = 0; i < fallbackCards.Count; i++)
            {
                if (fallbackCards[i] != null && fallbackCards[i].ModuleType == type)
                    return fallbackCards[i];
            }
            return null;
        }

        private void BuildFallbackCardPool()
        {
            for (int i = 0; i < AllModuleTypes.Length; i++)
            {
                UpgradeModuleType type = AllModuleTypes[i];
                bool hasConfiguredCard = false;
                if (cardPool != null)
                {
                    for (int cardIndex = 0; cardIndex < cardPool.Length; cardIndex++)
                    {
                        if (cardPool[cardIndex] != null && cardPool[cardIndex].ModuleType == type)
                        {
                            hasConfiguredCard = true;
                            break;
                        }
                    }
                }

                if (!hasConfiguredCard)
                    fallbackCards.Add(CreateFallbackCard(type));
            }
        }

        private static UpgradeModuleCardConfig CreateFallbackCard(UpgradeModuleType type)
        {
            UpgradeModuleCardConfig card = ScriptableObject.CreateInstance<UpgradeModuleCardConfig>();
            card.hideFlags = HideFlags.DontSave;
            switch (type)
            {
                case UpgradeModuleType.PlasmaCoolant:
                    card.Configure(type, "Plasma Coolant", "Reduces weapon heat generation by 25%.", 0.25f);
                    break;
                case UpgradeModuleType.TitaniumArmor:
                    card.Configure(type, "Titanium Armor", "Adds 40 maximum mech HP and fully repairs the mech.", 40f);
                    break;
                case UpgradeModuleType.TurretOverclock:
                    card.Configure(type, "Turret Overclock", "Increases all laser turret fire rate by 30%.", 1.3f);
                    break;
                case UpgradeModuleType.MagneticPull:
                    card.Configure(type, "Magnetic Pull", "Increases energy crystal pickup range by 50%.", 1.5f);
                    break;
                case UpgradeModuleType.OverchargedCapacitors:
                    card.Configure(type, "Overcharged Capacitors", "Increases weapon damage by 20%.", 1.2f);
                    break;
                case UpgradeModuleType.ThrusterBoost:
                    card.Configure(type, "Thruster Boost", "Increases mech movement speed by 15%.", 1.15f);
                    break;
            }
            return card;
        }
    }
}
