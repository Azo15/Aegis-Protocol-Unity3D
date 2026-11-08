using System;
using System.Collections.Generic;
using UnityEngine;
using AegisProtocol.Core;

namespace AegisProtocol.Systems
{
    /// <summary>Finds the nearest usable IInteractable and routes the E input to it.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class MechInteractionScanner : MonoBehaviour
    {
        private const int MaximumColliderBufferSize = 256;

        [SerializeField, Min(0.1f)] private float interactionRadius = 3.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField] private bool drawDebugRadius = false;

        private readonly List<MonoBehaviour> componentBuffer = new List<MonoBehaviour>(16);
        private readonly List<InteractableCandidate> candidates = new List<InteractableCandidate>(16);
        private Collider[] overlapBuffer = new Collider[32];
        private PlayerInputReader inputReader;
        private MonoBehaviour currentTargetBehaviour;
        private IInteractable currentTarget;
        private string currentPrompt = string.Empty;

        public event Action<IInteractable, string> OnTargetChanged;
        public event Action<string> OnPromptChanged;
        public IInteractable CurrentTarget => currentTarget;
        public MonoBehaviour CurrentTargetBehaviour => currentTargetBehaviour;
        public string CurrentPrompt => currentPrompt;
        public float InteractionRadius => interactionRadius;

        private void Awake()
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            if (inputReader == null)
                return;

            if (!CanInteractInCurrentState())
            {
                SetCurrentTarget(null, null, string.Empty);
                return;
            }

            ScanForNearestTarget();
            if (currentTarget != null && currentTargetBehaviour != null &&
                inputReader.CurrentSnapshot.InteractPressed &&
                currentTarget.CanInteract(gameObject))
            {
                currentTarget.Interact(gameObject);
            }
        }

        private void OnDisable()
        {
            SetCurrentTarget(null, null, string.Empty);
        }

        private bool CanInteractInCurrentState()
        {
            GameManager manager = GameManager.Instance;
            return manager == null ||
                   manager.CurrentState == GameState.WaveActive ||
                   manager.CurrentState == GameState.Briefing;
        }

        private void ScanForNearestTarget()
        {
            int overlapCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                interactionRadius,
                overlapBuffer,
                interactionLayers,
                QueryTriggerInteraction.Collide);

            if (overlapCount == overlapBuffer.Length && overlapBuffer.Length < MaximumColliderBufferSize)
            {
                Array.Resize(ref overlapBuffer, Mathf.Min(overlapBuffer.Length * 2, MaximumColliderBufferSize));
                overlapCount = Physics.OverlapSphereNonAlloc(
                    transform.position,
                    interactionRadius,
                    overlapBuffer,
                    interactionLayers,
                    QueryTriggerInteraction.Collide);
            }

            candidates.Clear();
            for (int i = 0; i < overlapCount; i++)
            {
                Collider hitCollider = overlapBuffer[i];
                if (hitCollider == null)
                    continue;

                float distanceSquared = (hitCollider.ClosestPoint(transform.position) - transform.position).sqrMagnitude;
                componentBuffer.Clear();
                hitCollider.GetComponentsInParent(true, componentBuffer);

                for (int componentIndex = 0; componentIndex < componentBuffer.Count; componentIndex++)
                {
                    MonoBehaviour behaviour = componentBuffer[componentIndex];
                    if (behaviour == null || !behaviour.isActiveAndEnabled ||
                        behaviour.transform == transform || behaviour.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    if (!(behaviour is IInteractable interactable) || !interactable.CanInteract(gameObject))
                        continue;

                    AddOrUpdateCandidate(interactable, behaviour, distanceSquared);
                }
            }

            int nearestIndex = -1;
            float nearestDistanceSquared = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].DistanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = candidates[i].DistanceSquared;
                    nearestIndex = i;
                }
            }

            if (nearestIndex < 0)
            {
                SetCurrentTarget(null, null, string.Empty);
                return;
            }

            InteractableCandidate nearest = candidates[nearestIndex];
            SetCurrentTarget(nearest.Interactable, nearest.Behaviour, nearest.Interactable.InteractionPrompt);
        }

        private void AddOrUpdateCandidate(
            IInteractable interactable,
            MonoBehaviour behaviour,
            float distanceSquared)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!ReferenceEquals(candidates[i].Interactable, interactable))
                    continue;

                InteractableCandidate existing = candidates[i];
                if (distanceSquared < existing.DistanceSquared)
                {
                    existing.DistanceSquared = distanceSquared;
                    candidates[i] = existing;
                }
                return;
            }

            candidates.Add(new InteractableCandidate(interactable, behaviour, distanceSquared));
        }

        private void SetCurrentTarget(IInteractable target, MonoBehaviour behaviour, string prompt)
        {
            if (behaviour == null)
            {
                target = null;
                prompt = string.Empty;
            }

            bool targetChanged = !ReferenceEquals(currentTarget, target) || currentTargetBehaviour != behaviour;
            bool promptChanged = !string.Equals(currentPrompt, prompt, StringComparison.Ordinal);
            if (!targetChanged && !promptChanged)
                return;

            currentTarget = target;
            currentTargetBehaviour = behaviour;
            currentPrompt = prompt ?? string.Empty;

            if (targetChanged)
                OnTargetChanged?.Invoke(currentTarget, currentPrompt);
            if (targetChanged || promptChanged)
                OnPromptChanged?.Invoke(currentPrompt);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugRadius)
                return;

            Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }

        private struct InteractableCandidate
        {
            public readonly IInteractable Interactable;
            public readonly MonoBehaviour Behaviour;
            public float DistanceSquared;

            public InteractableCandidate(IInteractable interactable, MonoBehaviour behaviour, float distanceSquared)
            {
                Interactable = interactable;
                Behaviour = behaviour;
                DistanceSquared = distanceSquared;
            }
        }
    }
}
