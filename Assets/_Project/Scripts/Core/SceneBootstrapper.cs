using UnityEngine;

namespace AegisProtocol.Core
{
    /// <summary>Ensures a session manager exists after scene objects have awakened.</summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class SceneBootstrapper : MonoBehaviour
    {
        [SerializeField] private GameManager gameManagerPrefab = null;
        [SerializeField] private bool createManagerWhenMissing = true;

        private void Start()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                if (gameManagerPrefab != null)
                {
                    manager = Instantiate(gameManagerPrefab);
                }
                else if (createManagerWhenMissing)
                {
                    GameObject managerObject = new GameObject("GameManager");
                    manager = managerObject.AddComponent<GameManager>();
                }
                else
                {
                    Debug.LogError("SceneBootstrapper could not find or create a GameManager.", this);
                    return;
                }
            }

            AegisArenaRuntimeBuilder arenaBuilder = GetComponent<AegisArenaRuntimeBuilder>();
            if (arenaBuilder == null)
                arenaBuilder = gameObject.AddComponent<AegisArenaRuntimeBuilder>();

            arenaBuilder.Build(manager);
            manager.ConfigureObjectives(arenaBuilder.MechHealth, arenaBuilder.ReactorHealth);
            manager.InitializeForScene();
        }
    }
}

