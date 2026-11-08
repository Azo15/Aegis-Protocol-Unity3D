using UnityEngine;
using AegisProtocol.Core;

namespace AegisProtocol.Systems
{
    /// <summary>An interactable construction socket that can contain a single defense structure.</summary>
    [DisallowMultipleComponent]
    public sealed class BuildNode : MonoBehaviour, IInteractable
    {
        [SerializeField] private string nodeId = "BuildNode";
        [SerializeField] private Transform structureAnchor = null;

        private DefenseTowerController builtStructure;
        private TacticalBuildManager buildManager;

        public string NodeId => nodeId;
        public Transform StructureAnchor => structureAnchor != null ? structureAnchor : transform;
        public bool IsOccupied => builtStructure != null;
        public DefenseTowerController BuiltStructure => builtStructure;

        public string InteractionPrompt
        {
            get
            {
                if (IsOccupied)
                    return string.Empty;
                TacticalBuildManager manager = GetBuildManager();
                return manager != null ? manager.BuildPrompt : "Build system unavailable";
            }
        }

        private void Awake()
        {
            buildManager = FindFirstObjectByType<TacticalBuildManager>();
        }

        public void ConfigureRuntime(string runtimeId)
        {
            if (!string.IsNullOrWhiteSpace(runtimeId))
                nodeId = runtimeId;
        }

        public bool CanInteract(GameObject interactor)
        {
            return !IsOccupied && GetBuildManager() != null;
        }

        public void Interact(GameObject interactor)
        {
            TacticalBuildManager manager = GetBuildManager();
            if (manager != null)
                manager.TryBuildAt(this, interactor);
        }

        public bool TryMarkOccupied(DefenseTowerController structure)
        {
            if (IsOccupied || structure == null)
                return false;

            builtStructure = structure;
            return true;
        }

        public void Release(DefenseTowerController structure)
        {
            if (builtStructure == structure)
                builtStructure = null;
        }

        private TacticalBuildManager GetBuildManager()
        {
            if (buildManager == null)
                buildManager = FindFirstObjectByType<TacticalBuildManager>();
            return buildManager;
        }
    }
}
