using System;

namespace AegisProtocol.Core
{
    /// <summary>High-level states for one game session.</summary>
    public enum GameState
    {
        MainMenu,
        Briefing,
        WaveActive,
        WaveUpgradeSelection,
        Paused,
        Victory,
        Defeat
    }

    /// <summary>Immutable description of a state transition for event consumers.</summary>
    public readonly struct GameStateChangedEvent
    {
        public GameState PreviousState { get; }
        public GameState CurrentState { get; }

        public GameStateChangedEvent(GameState previousState, GameState currentState)
        {
            PreviousState = previousState;
            CurrentState = currentState;
        }
    }

    /// <summary>Serializable snapshot of the session state for UI and save systems.</summary>
    [Serializable]
    public struct GameStateSnapshot
    {
        public GameState CurrentState;
        public int CurrentWave;
        public int CompletedWaves;
        public int CompletedTerminals;
        public int TotalScore;
        public float ElapsedSeconds;
        public float RemainingSeconds;
        public bool FinalBossDefeated;
    }
}
