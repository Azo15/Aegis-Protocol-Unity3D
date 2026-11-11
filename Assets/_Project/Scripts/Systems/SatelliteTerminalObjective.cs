using System;
using UnityEngine;
using UnityEngine.UI;
using AegisProtocol.Core;

namespace AegisProtocol.Systems
{
    /// <summary>Tracks a persistent 30-second satellite hack within its signal radius.</summary>
    [DisallowMultipleComponent]
    public sealed class SatelliteTerminalObjective : MonoBehaviour, IInteractable
    {
        private const float TerminalScoreReward = 500f;

        [SerializeField] private string terminalId = "Terminal_North";
        [SerializeField, Min(0.1f)] private float hackRadius = 6.5f;
        [SerializeField, Min(0.1f)] private float requiredHackDuration = 12f;
        [SerializeField] private Renderer[] statusRenderers = Array.Empty<Renderer>();
        [SerializeField] private Light[] statusLights = Array.Empty<Light>();
        [SerializeField] private Color inactiveColor = new Color(1f, 0.35f, 0.08f);
        [SerializeField] private Color activeColor = new Color(0.1f, 0.9f, 1f);
        [SerializeField] private Color completedColor = new Color(0.2f, 1f, 0.35f);

        private HealthComponent playerMech;
        private float progressSeconds;
        private float nextPlayerSearchTime;
        private bool hackActivated;
        private bool isCompleted;
        private RectTransform worldBarCanvas;
        private RectTransform worldBarFill;
        private Image worldBarFillImage;
        private Text worldBarLabel;
        private Camera cachedCamera;
        private static Sprite sharedWhiteSprite;
        private static Font sharedFont;

        public event Action<float, float> OnTerminalProgressChanged;
        public event Action<SatelliteTerminalObjective> OnTerminalCompleted;
        public string TerminalId => terminalId;
        public float ProgressSeconds => progressSeconds;
        public float RequiredHackDuration => requiredHackDuration;
        public float ProgressNormalized => requiredHackDuration <= 0f ? 1f : Mathf.Clamp01(progressSeconds / requiredHackDuration);
        public bool IsCompleted => isCompleted;
        public bool IsHackActivated => hackActivated;

        public string InteractionPrompt => isCompleted
            ? $"{terminalId} - Uydu Sinyali Tamamlandı (%100)"
            : $"{terminalId} - Uydu Hackleniyor (%{Mathf.RoundToInt(ProgressNormalized * 100f)})";

        private void Awake()
        {
            ApplyVisualState(inactiveColor, false);
        }

        private void Start()
        {
            if (Application.isPlaying)
            {
                BuildWorldHackBar();
                UpdateWorldHackBar();
            }
        }

        private void LateUpdate()
        {
            if (worldBarCanvas == null)
                return;

            worldBarCanvas.position = transform.position + Vector3.up * 3.15f;
            if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
                cachedCamera = Camera.main;
            if (cachedCamera != null)
                worldBarCanvas.rotation = cachedCamera.transform.rotation;

            UpdateWorldHackBar();
        }

        private void Update()
        {
            if (isCompleted || !CanProgressInCurrentState())
                return;

            ResolvePlayerMech();
            if (playerMech == null || playerMech.IsDead)
                return;

            float distance = Vector3.Distance(transform.position, playerMech.transform.position);
            if (distance <= hackRadius && !hackActivated)
                ActivateHack();
            if (!hackActivated || distance > hackRadius)
                return;

            progressSeconds = Mathf.Min(requiredHackDuration, progressSeconds + Time.deltaTime);
            OnTerminalProgressChanged?.Invoke(progressSeconds, requiredHackDuration);
            if (progressSeconds >= requiredHackDuration)
                CompleteHack();
        }

        public void ConfigureRuntime(string id)
        {
            if (!string.IsNullOrWhiteSpace(id))
                terminalId = id;
            if (statusRenderers == null || statusRenderers.Length == 0)
                statusRenderers = GetComponentsInChildren<Renderer>(true);
            if (statusLights == null || statusLights.Length == 0)
                statusLights = GetComponentsInChildren<Light>(true);
            ApplyVisualState(isCompleted ? completedColor : (hackActivated ? activeColor : inactiveColor), hackActivated || isCompleted);
        }

        public bool CanInteract(GameObject interactor)
        {
            return !isCompleted;
        }

        /// <summary>Manually arms the terminal; entering its signal radius also starts it.</summary>
        public void Interact(GameObject interactor)
        {
            if (!isCompleted)
                ActivateHack();
        }

        private void ActivateHack()
        {
            hackActivated = true;
            ApplyVisualState(activeColor, true);
        }

        private void ResolvePlayerMech()
        {
            if (playerMech != null && !playerMech.IsDead && Time.time < nextPlayerSearchTime)
                return;

            nextPlayerSearchTime = Time.time + 1f;
            HealthComponent[] components = FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].Role == HealthRole.PlayerMech && !components[i].IsDead)
                {
                    playerMech = components[i];
                    return;
                }
            }
            playerMech = null;
        }

        private bool CanProgressInCurrentState()
        {
            GameManager manager = GameManager.Instance;
            return manager == null || manager.CurrentState == GameState.WaveActive ||
                   manager.CurrentState == GameState.Briefing;
        }

        private void CompleteHack()
        {
            if (isCompleted)
                return;

            isCompleted = true;
            progressSeconds = requiredHackDuration;
            ApplyVisualState(completedColor, true);
            SpawnCompletedSkyBeam();
            GameManager manager = GameManager.Instance;
            if (manager != null && manager.RegisterTerminalCompleted(terminalId))
            {
                manager.AddScore(Mathf.RoundToInt(TerminalScoreReward));
                if (CrystalResourceBank.Instance != null)
                    CrystalResourceBank.Instance.AddCrystals(75);
                if (playerMech != null && !playerMech.IsDead)
                    playerMech.Heal(150f);
            }

            OnTerminalProgressChanged?.Invoke(progressSeconds, requiredHackDuration);
            OnTerminalCompleted?.Invoke(this);
        }

        private void ApplyVisualState(Color color, bool fullyActive)
        {
            if (statusRenderers != null)
            {
                for (int i = 0; i < statusRenderers.Length; i++)
                {
                    if (statusRenderers[i] != null && statusRenderers[i].material != null)
                        statusRenderers[i].material.color = color;
                }
            }
            if (statusLights != null)
            {
                for (int i = 0; i < statusLights.Length; i++)
                {
                    if (statusLights[i] == null)
                        continue;
                    statusLights[i].color = color;
                    statusLights[i].intensity = fullyActive ? 2.5f : 1f;
                    statusLights[i].enabled = true;
                }
            }
        }

        private void SpawnCompletedSkyBeam()
        {
            if (!Application.isPlaying)
                return;

            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "SatelliteSkyBeam";
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = new Vector3(0f, 16f, 0f);
            beam.transform.localScale = new Vector3(0.45f, 14f, 0.45f);
            Collider col = beam.GetComponent<Collider>();
            if (col != null)
                col.enabled = false;
            Renderer rend = beam.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = completedColor;
                if (rend.material.HasProperty("_EmissionColor"))
                {
                    rend.material.EnableKeyword("_EMISSION");
                    rend.material.SetColor("_EmissionColor", completedColor * 2.2f);
                }
            }
        }

        private void OnValidate()
        {
            hackRadius = Mathf.Max(0.1f, hackRadius);
            requiredHackDuration = Mathf.Max(0.1f, requiredHackDuration);
            if (string.IsNullOrWhiteSpace(terminalId))
                terminalId = "Terminal_North";
        }

        private void BuildWorldHackBar()
        {
            if (sharedWhiteSprite == null)
            {
                Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "SatelliteBar White Pixel",
                    hideFlags = HideFlags.DontSave
                };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                sharedWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
                sharedWhiteSprite.hideFlags = HideFlags.DontSave;
            }
            if (sharedFont == null)
            {
                try
                {
                    sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                catch (ArgumentException)
                {
                    sharedFont = null;
                }
            }

            GameObject canvasObj = new GameObject("SatelliteHackBarCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30;

            worldBarCanvas = canvasObj.GetComponent<RectTransform>();
            worldBarCanvas.sizeDelta = new Vector2(260f, 38f);
            worldBarCanvas.localScale = Vector3.one * 0.015f;

            GameObject borderObj = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(worldBarCanvas, false);
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            Image borderImg = borderObj.GetComponent<Image>();
            borderImg.sprite = sharedWhiteSprite;
            borderImg.color = new Color(0.03f, 0.06f, 0.12f, 0.94f);

            GameObject trackObj = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackObj.transform.SetParent(borderRect, false);
            RectTransform trackRect = trackObj.GetComponent<RectTransform>();
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = new Vector2(3.5f, 3.5f);
            trackRect.offsetMax = new Vector2(-3.5f, -3.5f);
            Image trackImg = trackObj.GetComponent<Image>();
            trackImg.sprite = sharedWhiteSprite;
            trackImg.color = new Color(0.18f, 0.10f, 0.05f, 0.92f);

            GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(trackRect, false);
            worldBarFill = fillObj.GetComponent<RectTransform>();
            worldBarFill.anchorMin = Vector2.zero;
            worldBarFill.anchorMax = new Vector2(0f, 1f);
            worldBarFill.offsetMin = Vector2.zero;
            worldBarFill.offsetMax = Vector2.zero;
            worldBarFillImage = fillObj.GetComponent<Image>();
            worldBarFillImage.sprite = sharedWhiteSprite;
            worldBarFillImage.color = activeColor;

            GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Outline));
            textObj.transform.SetParent(borderRect, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            worldBarLabel = textObj.GetComponent<Text>();
            worldBarLabel.font = sharedFont;
            worldBarLabel.fontSize = 16;
            worldBarLabel.fontStyle = FontStyle.Bold;
            worldBarLabel.alignment = TextAnchor.MiddleCenter;
            worldBarLabel.color = Color.white;
            worldBarLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            worldBarLabel.verticalOverflow = VerticalWrapMode.Overflow;

            Outline outline = textObj.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        private void UpdateWorldHackBar()
        {
            if (worldBarFill == null || worldBarLabel == null)
                return;

            float ratio = isCompleted ? 1f : ProgressNormalized;
            worldBarFill.anchorMax = new Vector2(ratio, 1f);

            if (isCompleted)
            {
                worldBarFillImage.color = completedColor;
                worldBarLabel.text = "✓ UYDU AKTİF! (+500 Puan & +75 Kristal)";
            }
            else if (hackActivated)
            {
                worldBarFillImage.color = activeColor;
                int pct = Mathf.RoundToInt(ratio * 100f);
                worldBarLabel.text = $"📡 UYDU HACKLENİYOR: %{pct}";
            }
            else
            {
                worldBarFill.anchorMax = new Vector2(0.12f, 1f);
                worldBarFillImage.color = inactiveColor;
                worldBarLabel.text = "📡 UYDU TERMINALİ (Halkada Dur)";
            }
        }
    }
}
