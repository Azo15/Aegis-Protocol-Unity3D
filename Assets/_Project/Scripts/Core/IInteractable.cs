using UnityEngine;

namespace AegisProtocol.Core
{
    /// <summary>Common contract for contextual world interactions.</summary>
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
