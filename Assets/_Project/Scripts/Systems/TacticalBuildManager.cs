using System;
using UnityEngine;
using AegisProtocol.Combat;
using AegisProtocol.Core;

namespace AegisProtocol.Systems
{
    [Serializable]
    public sealed class DefenseBuildOption
    {
        public DefenseStructureType type;
        public DefenseStructureConfig config = null;
        public DefenseTowerController prefab = null;
    }

    /// <summary>Handles build selection, crystal spending, placement, and global turret upgrades.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class TacticalBuildManager : MonoBehaviour
    {
        [SerializeField] private DefenseBuildOption[] buildOptions = Array.Empty<DefenseBuildOption>();
        [SerializeField] private DefenseStructureType selectedStructureType = DefenseStructureType.AutoLaserTurret;

        private PlayerInputReader inputReader;
        private float globalTurretFireRateMultiplier = 1f;

        public event Action<DefenseStructureType> OnSelectedStructureChanged;
        public event Action<DefenseTowerController> OnStructureBuilt;
        public DefenseStructureType SelectedStructureType => selectedStructureType;
        public float GlobalTurretFireRateMultiplier => globalTurretFireRateMultiplier;
        public string BuildPrompt => CreateBuildPrompt();

        private void Awake()
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            if (!CanBuildInCurrentState())
                return;

            PlayerInputSnapshot input = inputReader.CurrentSnapshot;
            if (input.BuildSlot1Pressed)
                SelectStructure(DefenseStructureType.AutoLaserTurret);
            else if (input.BuildSlot2Pressed)
                SelectStructure(DefenseStructureType.EmpSlowTower);
            else if (input.BuildSlot3Pressed)
                SelectStructure(DefenseStructureType.RepairStation);
        }

        public void SelectStructure(DefenseStructureType type)
        {
            if (selectedStructureType == type)
                return;

            selectedStructureType = type;
            OnSelectedStructureChanged?.Invoke(selectedStructureType);
        }

        public bool TryBuildAt(BuildNode node, GameObject interactor)
        {
            if (node == null || node.IsOccupied || !CanBuildInCurrentState())
                return false;

            DefenseBuildOption option = FindOption(selectedStructureType);
            CrystalResourceBank bank = CrystalResourceBank.Instance;
            int cost = option != null && option.config != null
                ? option.config.CrystalCost
                : GetDefaultCost(selectedStructureType);
            if (bank == null || !bank.TrySpendCrystals(cost))
                return false;

            DefenseStructureConfig config = option != null ? option.config : null;
            bool ownsConfig = config == null;
            if (ownsConfig)
            {
                config = ScriptableObject.CreateInstance<DefenseStructureConfig>();
                config.hideFlags = HideFlags.DontSave;
                config.Configure(selectedStructureType);
            }

            Transform anchor = node.StructureAnchor;
            DefenseTowerController structure = option != null && option.prefab != null
                ? Instantiate(option.prefab, anchor.position, anchor.rotation)
                : CreateRuntimeFallback(config, anchor.position, anchor.rotation);

            if (structure == null)
            {
                bank.AddCrystals(cost);
                if (ownsConfig)
                    Destroy(config);
                return false;
            }

            structure.Initialize(config, node, globalTurretFireRateMultiplier, ownsConfig);
            structure.transform.SetParent(anchor, true);
            if (!node.TryMarkOccupied(structure))
            {
                Destroy(structure.gameObject);
                bank.AddCrystals(cost);
                return false;
            }

            OnStructureBuilt?.Invoke(structure);
            return true;
        }

        /// <summary>Applies a cumulative fire-rate upgrade to existing and future laser turrets.</summary>
        public void ApplyGlobalTurretFireRateMultiplier(float multiplier)
        {
            if (multiplier <= 0f || float.IsNaN(multiplier) || float.IsInfinity(multiplier))
                return;

            globalTurretFireRateMultiplier = Mathf.Clamp(globalTurretFireRateMultiplier * multiplier, 0.1f, 5f);
            DefenseTowerController[] towers = FindObjectsByType<DefenseTowerController>(FindObjectsSortMode.None);
            for (int i = 0; i < towers.Length; i++)
            {
                if (towers[i] != null)
                    towers[i].ApplyFireRateMultiplier(multiplier);
            }
        }

        private string CreateBuildPrompt()
        {
            DefenseBuildOption option = FindOption(selectedStructureType);
            int cost = option != null && option.config != null
                ? option.config.CrystalCost
                : GetDefaultCost(selectedStructureType);
            CrystalResourceBank bank = CrystalResourceBank.Instance;
            string structureName = selectedStructureType == DefenseStructureType.AutoLaserTurret
                ? "Lazer Kulesi"
                : selectedStructureType == DefenseStructureType.EmpSlowTower
                    ? "EMP Yavaşlatma Kulesi"
                    : "Onarım İstasyonu";
            return bank != null && bank.CurrentBalance >= cost
                ? $"{structureName} Kur ({cost} Kristal)"
                : $"{structureName} için {cost} Kristal Gerekli";
        }

        private DefenseBuildOption FindOption(DefenseStructureType type)
        {
            if (buildOptions == null)
                return null;
            for (int i = 0; i < buildOptions.Length; i++)
            {
                if (buildOptions[i] != null && buildOptions[i].type == type)
                    return buildOptions[i];
            }
            return null;
        }

        private bool CanBuildInCurrentState()
        {
            GameManager manager = GameManager.Instance;
            return manager == null || manager.CurrentState == GameState.WaveActive ||
                   manager.CurrentState == GameState.Briefing;
        }

        private static int GetDefaultCost(DefenseStructureType type)
        {
            switch (type)
            {
                case DefenseStructureType.AutoLaserTurret: return 50;
                case DefenseStructureType.EmpSlowTower: return 75;
                case DefenseStructureType.RepairStation: return 100;
                default: return int.MaxValue;
            }
        }

        private static DefenseTowerController CreateRuntimeFallback(
            DefenseStructureConfig config,
            Vector3 position,
            Quaternion rotation)
        {
            GameObject root = new GameObject(config.StructureType.ToString());
            root.transform.SetPositionAndRotation(position, rotation);
            return root.AddComponent<DefenseTowerController>();
        }
    }
}
