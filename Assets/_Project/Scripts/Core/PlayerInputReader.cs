using UnityEngine;

namespace AegisProtocol.Core
{
    /// <summary>Immutable input values for one player update.</summary>
    public readonly struct PlayerInputSnapshot
    {
        public Vector2 MoveVector { get; }
        public Vector2 PointerScreenPosition { get; }
        public bool DashPressed { get; }
        public bool InteractPressed { get; }
        public bool PausePressed { get; }
        public bool PrimaryFirePressed { get; }
        public bool PrimaryFireHeld { get; }
        public bool PrimaryFireReleased { get; }
        public bool SecondaryFirePressed { get; }
        public bool SecondaryFireHeld { get; }
        public bool BuildSlot1Pressed { get; }
        public bool BuildSlot2Pressed { get; }
        public bool BuildSlot3Pressed { get; }
        public bool TacticalLightTogglePressed { get; }

        public PlayerInputSnapshot(
            Vector2 moveVector,
            Vector2 pointerScreenPosition,
            bool dashPressed = false,
            bool interactPressed = false,
            bool pausePressed = false,
            bool primaryFirePressed = false,
            bool primaryFireHeld = false,
            bool primaryFireReleased = false,
            bool secondaryFirePressed = false,
            bool secondaryFireHeld = false,
            bool buildSlot1Pressed = false,
            bool buildSlot2Pressed = false,
            bool buildSlot3Pressed = false,
            bool tacticalLightTogglePressed = false)
        {
            MoveVector = Vector2.ClampMagnitude(moveVector, 1f);
            PointerScreenPosition = pointerScreenPosition;
            DashPressed = dashPressed;
            InteractPressed = interactPressed;
            PausePressed = pausePressed;
            PrimaryFirePressed = primaryFirePressed;
            PrimaryFireHeld = primaryFireHeld;
            PrimaryFireReleased = primaryFireReleased;
            SecondaryFirePressed = secondaryFirePressed;
            SecondaryFireHeld = secondaryFireHeld;
            BuildSlot1Pressed = buildSlot1Pressed;
            BuildSlot2Pressed = buildSlot2Pressed;
            BuildSlot3Pressed = buildSlot3Pressed;
            TacticalLightTogglePressed = tacticalLightTogglePressed;
        }

        public static PlayerInputSnapshot Empty => new PlayerInputSnapshot(Vector2.zero, Vector2.zero);
    }

    /// <summary>Pluggable source for tests, replay, and automation input.</summary>
    public interface IPlayerInputSource
    {
        PlayerInputSnapshot ReadSnapshot();
    }

    /// <summary>Reads classic Unity Input Manager values or accepts injected input.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private IPlayerInputSource inputSource;
        private PlayerInputSnapshot simulatedSnapshot;
        private bool hasSimulatedSnapshot;

        public PlayerInputSnapshot CurrentSnapshot { get; private set; } = PlayerInputSnapshot.Empty;

        /// <summary>Uses the supplied source instead of Unity's classic Input Manager.</summary>
        public void SetInputSource(IPlayerInputSource source)
        {
            inputSource = source;
            hasSimulatedSnapshot = false;
        }

        /// <summary>Supplies a deterministic snapshot; call each frame to simulate held input.</summary>
        public void SetSimulatedSnapshot(PlayerInputSnapshot snapshot)
        {
            simulatedSnapshot = snapshot;
            hasSimulatedSnapshot = true;
        }

        /// <summary>Returns to the injected source, if any, or the classic Input Manager.</summary>
        public void ClearSimulatedInput()
        {
            hasSimulatedSnapshot = false;
        }

        private void Update()
        {
            if (inputSource != null)
            {
                CurrentSnapshot = inputSource.ReadSnapshot();
                return;
            }

            if (hasSimulatedSnapshot)
            {
                CurrentSnapshot = simulatedSnapshot;
                return;
            }

            CurrentSnapshot = ReadClassicInput();
        }

        private static PlayerInputSnapshot ReadClassicInput()
        {
            float moveX = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                moveX -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                moveX += 1f;

            float moveY = 0f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                moveY -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                moveY += 1f;

            Vector2 move = new Vector2(moveX, moveY);

            return new PlayerInputSnapshot(
                move,
                Input.mousePosition,
                dashPressed: Input.GetKeyDown(KeyCode.Space),
                interactPressed: Input.GetKeyDown(KeyCode.E),
                pausePressed: Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P),
                primaryFirePressed: Input.GetMouseButtonDown(0),
                primaryFireHeld: Input.GetMouseButton(0),
                primaryFireReleased: Input.GetMouseButtonUp(0),
                secondaryFirePressed: Input.GetMouseButtonDown(1),
                secondaryFireHeld: Input.GetMouseButton(1),
                buildSlot1Pressed: Input.GetKeyDown(KeyCode.Alpha1),
                buildSlot2Pressed: Input.GetKeyDown(KeyCode.Alpha2),
                buildSlot3Pressed: Input.GetKeyDown(KeyCode.Alpha3),
                tacticalLightTogglePressed: Input.GetKeyDown(KeyCode.F));
        }
    }
}

