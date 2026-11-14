using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AegisProtocol.Audio;
using AegisProtocol.AI;
using AegisProtocol.Combat;
using AegisProtocol.Core;
using AegisProtocol.Systems;

namespace AegisProtocol.UI
{
    /// <summary>Creates and controls the complete runtime uGUI menu, HUD, and card selection flow.</summary>
    [DefaultExecutionOrder(1300)]
    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour
    {
        private static readonly Color BackgroundColor = new Color(0.015f, 0.035f, 0.075f, 0.78f);
        private static readonly Color PanelColor = new Color(0.025f, 0.09f, 0.15f, 0.96f);
        private static readonly Color AccentColor = new Color(0.05f, 0.85f, 1f, 1f);

        private GameManager gameManager;
        private PlayerInputReader inputReader;
        private HealthComponent mechHealth;
        private HealthComponent reactorHealth;
        private WeaponSystem weaponSystem;
        private MechController mechController;
        private CrystalResourceBank resourceBank;
        private TacticalBuildManager buildManager;
        private MechInteractionScanner interactionScanner;
        private RogueliteUpgradeManager upgradeManager;
        private WaveSpawnerManager waveSpawner;
        private AudioManager audioManager;
        private IsometricCameraRig cameraRig;

        private GameObject mainPanel;
        private GameObject levelSelectPanel;
        private GameObject briefingPanel;
        private GameObject howToPanel;
        private GameObject settingsPanel;
        private GameObject hudPanel;
        private GameObject upgradePanel;
        private GameObject pausePanel;
        private GameObject resultPanel;
        private Transform canvasTransform;
        private Text briefingTitleText;
        private Text briefingDetailsText;
        private Text resultBadgeText;
        private Text resultTitle;
        private Text resultDetails;
        private GameObject resultUnlockBox;
        private Text resultUnlockText;
        private Image resultTopBannerImage;
        private GameObject nextLevelButtonObject;
        private Text nextLevelButtonText;
        private Button[] levelSelectButtons = Array.Empty<Button>();
        private Text[] levelSelectLabels = Array.Empty<Text>();
        private Text buildSelectionText;
        private Text interactPromptText;
        private Text waveText;
        private Text scoreText;
        private Text timerText;
        private Text terminalText;
        private Text mechHealthText;
        private Text reactorHealthText;
        private Text heatText;
        private Text missileText;
        private Text dashText;
        private Text crystalText;
        private Text cameraModeText;
        private Text cockpitCrosshairText;
        private Text waveAlertText;
        private Text castleUnderAttackText;
        private DamageVignetteOverlay damageVignette;
        private HealthComponent subscribedReactorHealth;
        private HealthComponent subscribedMechHealth;
        private float castleUnderAttackTimer;
        private int lastAnnouncedWave;
        private float waveAlertTimer;
        private Text[] upgradeCardTexts = Array.Empty<Text>();
        private Slider mechHealthBar;
        private Slider reactorHealthBar;
        private Slider heatBar;
        private Slider missileBar;
        private Slider dashBar;
        private Font runtimeFont;
        private Sprite runtimeWhiteSprite;
        private Texture2D runtimeWhiteTexture;
        private UpgradeModuleCardConfig[] presentedCards = Array.Empty<UpgradeModuleCardConfig>();
        private GameState stateBeforeAuxiliaryPanel = GameState.MainMenu;
        private float targetDurationSeconds = 900f;

        private void Awake()
        {
            BuildRuntimeCanvas();
            CreateAllScreens();
        }

        private void Start()
        {
            ResolveSystems();
            if (gameManager != null)
            {
                gameManager.OnGameStateChanged += HandleGameStateChanged;
                SetVisibleState(gameManager.CurrentState);
                targetDurationSeconds = gameManager.ElapsedSeconds + gameManager.RemainingSeconds;
            }
            if (upgradeManager != null)
                upgradeManager.OnUpgradeOptionsPresented += HandleUpgradeOptionsPresented;
        }

        private void OnDestroy()
        {
            UnbindHealthEvents();
            if (gameManager != null)
                gameManager.OnGameStateChanged -= HandleGameStateChanged;
            if (upgradeManager != null)
                upgradeManager.OnUpgradeOptionsPresented -= HandleUpgradeOptionsPresented;
            Time.timeScale = 1f;
            if (runtimeWhiteSprite != null)
                Destroy(runtimeWhiteSprite);
            if (runtimeWhiteTexture != null)
                Destroy(runtimeWhiteTexture);
        }

        private void Update()
        {
            ResolveSystemsIfNeeded();
            if (Input.GetKeyDown(KeyCode.F11))
                ToggleFullscreen();
            if (Input.GetKeyDown(KeyCode.F10))
            {
                gameManager?.UnlockAllLevels();
                RefreshLevelSelectButtons();
            }

            if (inputReader != null)
            {
                PlayerInputSnapshot input = inputReader.CurrentSnapshot;
                if (input.PausePressed && gameManager != null)
                {
                    if (gameManager.CurrentState == GameState.Paused)
                        gameManager.Resume();
                    else
                        gameManager.Pause();
                }

                if (gameManager != null && gameManager.CurrentState == GameState.WaveUpgradeSelection)
                {
                    if (input.BuildSlot1Pressed)
                        SelectUpgrade(0);
                    else if (input.BuildSlot2Pressed)
                        SelectUpgrade(1);
                    else if (input.BuildSlot3Pressed)
                        SelectUpgrade(2);
                }
            }

            UpdateHud();
        }

        private void BuildRuntimeCanvas()
        {
            GameObject canvasObject = new GameObject("Aegis Runtime Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(null, false);
            canvasObject.transform.localScale = Vector3.one;
            canvasTransform = canvasObject.transform;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            runtimeWhiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Aegis Runtime UI Pixel",
                hideFlags = HideFlags.DontSave
            };
            runtimeWhiteTexture.SetPixel(0, 0, Color.white);
            runtimeWhiteTexture.Apply();
            runtimeWhiteSprite = Sprite.Create(runtimeWhiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            runtimeWhiteSprite.name = "Aegis Runtime UI Sprite";
            runtimeWhiteSprite.hideFlags = HideFlags.DontSave;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventObject = new GameObject("Aegis Event System", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(eventObject);
            }

            runtimeFont = TryLoadRuntimeFont("LegacyRuntime.ttf");
            if (runtimeFont == null)
                runtimeFont = TryLoadRuntimeFont("Arial.ttf");
            if (runtimeFont == null)
                Debug.LogWarning("Aegis UI could not load a built-in font; panels will remain available.", this);
        }

        private Font TryLoadRuntimeFont(string resourceName)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(resourceName);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private void CreateAllScreens()
        {
            mainPanel = CreatePanel("Main Menu", BackgroundColor);
            BuildMainMenu(mainPanel.transform);
            levelSelectPanel = CreatePanel("Level Select", BackgroundColor);
            BuildLevelSelect(levelSelectPanel.transform);
            briefingPanel = CreatePanel("Mission Briefing", BackgroundColor);
            BuildBriefing(briefingPanel.transform);
            howToPanel = CreatePanel("How To Play", BackgroundColor);
            BuildHowToPlay(howToPanel.transform);
            settingsPanel = CreatePanel("Audio Settings", BackgroundColor);
            BuildSettings(settingsPanel.transform);
            hudPanel = CreatePanel("Gameplay HUD", new Color(0f, 0f, 0f, 0f));
            BuildHud(hudPanel.transform);
            upgradePanel = CreatePanel("Upgrade Selection", BackgroundColor);
            BuildUpgradeScreen(upgradePanel.transform);
            pausePanel = CreatePanel("Pause Menu", BackgroundColor);
            BuildPauseMenu(pausePanel.transform);
            resultPanel = CreatePanel("Mission Result", BackgroundColor);
            BuildResultScreen(resultPanel.transform);
            SetVisibleState(GameState.MainMenu);
        }

        private void BuildMainMenu(Transform parent)
        {
            Transform content = CreateVerticalContent(parent, 740f, 840f);
            CreateText(content, "AEGIS PROTOCOL", 52, AccentColor, 82f, FontStyle.Bold);
            CreateText(content, "3D TACTICAL MECH & 5-SECTOR CAMPAIGN", 22, Color.white, 48f, FontStyle.Bold);
            AddButton(content, "OYUNA BAŞLA (SEVİYE SEÇİMİ)", ShowLevelSelectPanel, 64f);
            AddButton(content, "NASIL OYNANIR & PUAN TABLOSU", () => ShowAuxiliaryPanel(howToPanel), 56f);
            AddButton(content, "SES AYARLARI", () => ShowAuxiliaryPanel(settingsPanel), 56f);
            AddButton(content, "ÇIKIŞ", Application.Quit, 56f);
        }

        private void BuildLevelSelect(Transform parent)
        {
            Transform content = CreateVerticalContent(parent, 860f, 820f);
            CreateText(content, "SEKTÖR SEÇİMİ", 38, AccentColor, 54f, FontStyle.Bold);
            CreateText(content, "Oynamak istediğin operasyonu seç  •  (Test: F10)", 17, new Color(0.75f, 0.88f, 0.96f, 0.9f), 30f, FontStyle.Normal);

            levelSelectButtons = new Button[SectorLevelConfig.TotalLevels];
            levelSelectLabels = new Text[SectorLevelConfig.TotalLevels];
            for (int i = 0; i < SectorLevelConfig.TotalLevels; i++)
            {
                int levelNumber = i + 1;
                Text label = AddButton(content, string.Empty, () => LaunchSelectedLevel(levelNumber), 84f, 19);
                levelSelectLabels[i] = label;
                levelSelectButtons[i] = label.transform.parent.GetComponent<Button>();
            }

            AddButton(content, "GERİ", () => SetVisibleState(GameState.MainMenu), 52f, 20);
        }

        private void RefreshLevelSelectButtons()
        {
            int maxUnlocked = gameManager != null ? gameManager.MaxUnlockedLevel : 1;
            string[] shortLoadouts =
            {
                "1x Mavi Lazer  •  1x Güdümlü Füze",
                "2x İkili Zümrüt Tüfek  •  2x Füze  •  +%20 Hız",
                "3x Yıldırım Tüfeği  •  3x Füze  •  Gece Projektörü",
                "4x Kızıl Magma Topu  •  4x Füze  •  Oto-Onarım",
                "5x Omega Hyper-Cannon  •  5x Nükleer  •  +%50 Kule Hızı"
            };

            for (int i = 0; i < SectorLevelConfig.TotalLevels; i++)
            {
                int levelNumber = i + 1;
                SectorLevelData data = SectorLevelConfig.GetLevel(levelNumber);
                bool unlocked = levelNumber <= maxUnlocked;
                bool completed = levelNumber < maxUnlocked;
                string badge = completed ? "✓ TAMAMLANDI" : (unlocked ? "▶ OYNANABİLİR" : "🔒 KİLİTLİ");

                if (levelSelectLabels[i] != null)
                {
                    string loadout = i < shortLoadouts.Length ? shortLoadouts[i] : string.Empty;
                    levelSelectLabels[i].text = unlocked
                        ? $"{data.SectorName}   •   [{badge}]\n{loadout}"
                        : $"{data.SectorName}   •   [{badge}]";
                    levelSelectLabels[i].color = completed
                        ? new Color(0.45f, 1f, 0.68f, 1f)
                        : (unlocked ? Color.white : new Color(0.50f, 0.56f, 0.62f, 0.8f));
                }

                if (levelSelectButtons[i] != null)
                    levelSelectButtons[i].interactable = unlocked;
            }
        }

        private void ShowLevelSelectPanel()
        {
            RefreshLevelSelectButtons();
            stateBeforeAuxiliaryPanel = GameState.MainMenu;
            SetAllPanelsInactive();
            SetPanel(levelSelectPanel, true);
        }

        private void LaunchSelectedLevel(int levelNumber)
        {
            if (gameManager == null)
                return;
            if (!gameManager.SelectLevel(levelNumber))
                return;

            gameManager.StartRun();
            ReloadActiveScene();
            UpdateBriefingTexts();
        }

        private void ReloadActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.name))
                SceneManager.LoadScene(scene.name);
            else if (scene.IsValid() && scene.buildIndex >= 0)
                SceneManager.LoadScene(scene.buildIndex);
        }

        private void BuildBriefing(Transform parent)
        {
            Transform content = CreateVerticalContent(parent, 820f, 680f);
            briefingTitleText = CreateText(content, "GÖREV BRİFİNGİ", 38, AccentColor, 58f, FontStyle.Bold);
            briefingDetailsText = CreateText(content, string.Empty, 21, Color.white, 240f, FontStyle.Normal);
            AddButton(content, "▶ SAVAŞI BAŞLAT", () => gameManager?.StartWave(1), 64f, 23);
            AddButton(content, "SEKTÖR DEĞİŞTİR", ShowLevelSelectPanel, 52f, 20);
            AddButton(content, "ANA MENÜ", ReturnToMainMenu, 50f, 20);
            UpdateBriefingTexts();
        }

        private void UpdateBriefingTexts()
        {
            if (briefingTitleText == null || briefingDetailsText == null)
                return;

            SectorLevelData data = gameManager != null ? gameManager.CurrentLevelData : SectorLevelConfig.GetLevel(1);
            string enemyTierDesc = GetStageEnemyDescription(data.LevelNumber);
            briefingTitleText.text = $"{data.SectorName}";
            briefingDetailsText.text =
                $"★ {data.WeaponEvolutionTitle}\n" +
                $"☠ {enemyTierDesc}\n\n" +
                "🎯 GÖREV: 5 Dalgayı temizle ve Kaleyi koru.\n" +
                "📡 BONUS: Turuncu uydu halkalarında dur (+75 Kristal & +Can).";
        }

        private static string GetStageEnemyDescription(int levelNumber)
        {
            switch (levelNumber)
            {
                case 1:
                    return "MK-I Öncü Dronlar";
                case 2:
                    return "MK-II Çift Namlulu Çöl Kuşatmacıları";
                case 3:
                    return "MK-III Mor Plazma Siber Lejyon";
                case 4:
                    return "MK-IV Füzeli Juggernautlar (Çift Boss)";
                case 5:
                    return "MK-V Omega İyon Ordusu (Final)";
                default:
                    return "Zırhlı Mekanik Birlikler";
            }
        }

        private void BuildHowToPlay(Transform parent)
        {
            Transform content = CreateVerticalContent(parent, 820f, 660f);
            CreateText(content, "KONTROLLER & PUANLAR", 34, AccentColor, 56f, FontStyle.Bold);
            CreateText(content,
                "[WASD / Oklar] Hareket  •  [Space] Dash  •  [H] İlaç (+Can)\n" +
                "[Fare] Nişan & Ateş (Sol: Tüfek / Sağ: Füze)\n" +
                "[C] Kamera Açısı  •  [Q / R] Döndür  •  [Tekerlek] Yakınlaştır\n" +
                "[1] Lazer (50)  •  [2] EMP (75)  •  [3] Onarım (100)  •  [E] Kule Kur\n" +
                "[F] Fener  •  [Esc / P] Duraklat\n\n" +
                "Puan: Drone +50 | Tank +100 | Robot +250 | Boss +1500 | Uydu +500",
                20, Color.white, 290f, FontStyle.Normal);
            AddButton(content, "GERİ", () => SetVisibleState(stateBeforeAuxiliaryPanel), 56f);
        }

        private void BuildSettings(Transform parent)
        {
            Transform content = CreateVerticalContent(parent, 760f, 700f);
            CreateText(content, "SES AYARLARI", 38, AccentColor, 64f, FontStyle.Bold);
            AddVolumeSlider(content, "MASTER", 0.8f, value => audioManager?.SetMasterVolume(value));
            AddVolumeSlider(content, "MÜZİK", 0.45f, value => audioManager?.SetBgmVolume(value));
            AddVolumeSlider(content, "SES EFEKTLERİ", 0.8f, value => audioManager?.SetSfxVolume(value));
            AddButton(content, "TAM EKRAN AÇ / KAPAT  (F11)", ToggleFullscreen, 58f);
            AddButton(content, "GERİ", () => SetVisibleState(stateBeforeAuxiliaryPanel), 58f);
        }

        private static void ToggleFullscreen()
        {
#if UNITY_EDITOR
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType != null)
            {
                UnityEditor.EditorWindow gameView = UnityEditor.EditorWindow.GetWindow(gameViewType);
                if (gameView != null)
                {
                    gameView.maximized = !gameView.maximized;
                    return;
                }
            }
#endif
            bool makeFullscreen = !Screen.fullScreen;
            if (makeFullscreen)
            {
                int width = Display.main != null ? Display.main.systemWidth : 1920;
                int height = Display.main != null ? Display.main.systemHeight : 1080;
                if (width <= 0 || height <= 0)
                {
                    width = 1920;
                    height = 1080;
                }
                Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
                Screen.fullScreen = true;
            }
            else
            {
                Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
                Screen.fullScreen = false;
            }
        }

        private void BuildPauseMenu(Transform parent)
        {
            Transform content = CreateVerticalContent(parent, 700f, 760f);
            CreateText(content, "SİSTEM DURAKLATILDI", 40, AccentColor, 72f, FontStyle.Bold);
            AddButton(content, "DEVAM ET", () => gameManager?.Resume(), 62f);
            AddButton(content, "BÖLÜMÜ YENİDEN BAŞLAT", RestartCurrentScene, 58f);
            AddButton(content, "SES SEVİYESİ", () => ShowAuxiliaryPanel(settingsPanel), 56f);
            AddButton(content, "ANA MENÜYE DÖN", ReturnToMainMenu, 56f);
        }

        private void BuildResultScreen(Transform parent)
        {
            Transform content = CreateVerticalContent(parent, 980f, 920f);

            GameObject bannerObj = new GameObject("Celebration Top Banner", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            bannerObj.transform.SetParent(content, false);
            LayoutElement bannerLayout = bannerObj.GetComponent<LayoutElement>();
            bannerLayout.minHeight = 68f;
            bannerLayout.preferredHeight = 68f;
            resultTopBannerImage = bannerObj.GetComponent<Image>();
            resultTopBannerImage.sprite = runtimeWhiteSprite;
            resultTopBannerImage.color = new Color(0.85f, 0.62f, 0.08f, 0.95f);

            GameObject badgeTextObj = new GameObject("Badge Text", typeof(RectTransform), typeof(Text));
            badgeTextObj.transform.SetParent(bannerObj.transform, false);
            Stretch(badgeTextObj.GetComponent<RectTransform>());
            resultBadgeText = badgeTextObj.GetComponent<Text>();
            resultBadgeText.font = runtimeFont;
            resultBadgeText.fontSize = 26;
            resultBadgeText.fontStyle = FontStyle.Bold;
            resultBadgeText.color = new Color(0.05f, 0.08f, 0.12f, 1f);
            resultBadgeText.alignment = TextAnchor.MiddleCenter;
            resultBadgeText.text = "★ ★ ★   TEBRİKLER KOMUTAN — ZAFER MADALYASI   ★ ★ ★";

            resultTitle = CreateText(content, "GÖREV SONUCU", 36, AccentColor, 58f, FontStyle.Bold);
            resultDetails = CreateText(content, string.Empty, 21, Color.white, 175f, FontStyle.Normal);

            resultUnlockBox = new GameObject("Unlock Reward Box", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            resultUnlockBox.transform.SetParent(content, false);
            LayoutElement unlockLayout = resultUnlockBox.GetComponent<LayoutElement>();
            unlockLayout.minHeight = 128f;
            unlockLayout.preferredHeight = 128f;
            Image unlockBg = resultUnlockBox.GetComponent<Image>();
            unlockBg.sprite = runtimeWhiteSprite;
            unlockBg.color = new Color(0.06f, 0.24f, 0.32f, 0.96f);

            GameObject unlockTextObj = new GameObject("Unlock Text", typeof(RectTransform), typeof(Text));
            unlockTextObj.transform.SetParent(resultUnlockBox.transform, false);
            Stretch(unlockTextObj.GetComponent<RectTransform>());
            resultUnlockText = unlockTextObj.GetComponent<Text>();
            resultUnlockText.font = runtimeFont;
            resultUnlockText.fontSize = 20;
            resultUnlockText.fontStyle = FontStyle.Bold;
            resultUnlockText.color = new Color(1f, 0.92f, 0.35f, 1f);
            resultUnlockText.alignment = TextAnchor.MiddleCenter;
            resultUnlockText.lineSpacing = 1.22f;

            nextLevelButtonText = AddButton(content, "► SONRAKİ SEVİYEYE GEÇ", AdvanceToNextLevel, 64f, 23);
            nextLevelButtonObject = nextLevelButtonText.transform.parent.gameObject;
            Image nextBtnImg = nextLevelButtonObject.GetComponent<Image>();
            if (nextBtnImg != null)
                nextBtnImg.color = new Color(0.08f, 0.48f, 0.26f, 1f);
            Button nextBtn = nextLevelButtonObject.GetComponent<Button>();
            if (nextBtn != null)
            {
                ColorBlock cb = nextBtn.colors;
                cb.normalColor = new Color(0.08f, 0.48f, 0.26f, 1f);
                cb.highlightedColor = new Color(0.14f, 0.68f, 0.36f, 1f);
                cb.pressedColor = new Color(0.2f, 0.85f, 0.45f, 1f);
                cb.selectedColor = cb.highlightedColor;
                nextBtn.colors = cb;
            }

            AddButton(content, "BU SEVİYEYİ TEKRAR OYNA", RestartCurrentScene, 54f, 20);
            AddButton(content, "SEVİYE & SEKTÖR SEÇİMİ", ShowLevelSelectPanel, 54f, 20);
            AddButton(content, "ANA MENÜ", ReturnToMainMenu, 50f, 20);
        }

        private void AdvanceToNextLevel()
        {
            if (gameManager == null)
                return;

            int nextLevel = Mathf.Min(SectorLevelConfig.TotalLevels, gameManager.CurrentLevel + 1);
            LaunchSelectedLevel(nextLevel);
        }

        private void BuildUpgradeScreen(Transform parent)
        {
            Transform content = CreateVerticalContent(parent, 1060f, 900f);
            CreateText(content, "DALGA YÜKSELTME SEÇİMİ", 38, AccentColor, 64f, FontStyle.Bold);
            CreateText(content, "Bir modül seç: tıklayın veya 1 / 2 / 3 tuşuna basın.", 20, Color.white, 46f, FontStyle.Normal);
            upgradeCardTexts = new Text[3];
            for (int i = 0; i < upgradeCardTexts.Length; i++)
            {
                int optionIndex = i;
                upgradeCardTexts[i] = AddButton(content, "Kart yükleniyor...", () => SelectUpgrade(optionIndex), 170f, 19);
            }
        }

        private void BuildHud(Transform parent)
        {
            mechHealthText = CreateHudText(parent, "MECH HP", new Vector2(28f, -28f), new Vector2(0f, 1f), 22, TextAnchor.UpperLeft);
            mechHealthBar = CreateHudBar(parent, new Vector2(30f, -60f), new Vector2(0f, 1f), new Vector2(360f, 20f), new Color(0.1f, 0.85f, 1f));
            reactorHealthText = CreateHudText(parent, "REAKTÖR HP", new Vector2(28f, -94f), new Vector2(0f, 1f), 22, TextAnchor.UpperLeft);
            reactorHealthBar = CreateHudBar(parent, new Vector2(30f, -126f), new Vector2(0f, 1f), new Vector2(360f, 20f), new Color(0.2f, 1f, 0.4f));

            waveText = CreateHudText(parent, string.Empty, new Vector2(-30f, -28f), new Vector2(1f, 1f), 23, TextAnchor.UpperRight);
            timerText = CreateHudText(parent, string.Empty, new Vector2(-30f, -64f), new Vector2(1f, 1f), 20, TextAnchor.UpperRight);
            scoreText = CreateHudText(parent, string.Empty, new Vector2(-30f, -98f), new Vector2(1f, 1f), 20, TextAnchor.UpperRight);
            terminalText = CreateHudText(parent, string.Empty, new Vector2(-30f, -132f), new Vector2(1f, 1f), 20, TextAnchor.UpperRight);

            heatText = CreateHudText(parent, "HEAT", new Vector2(0f, 142f), new Vector2(0.5f, 0f), 20, TextAnchor.MiddleCenter);
            heatBar = CreateHudBar(parent, new Vector2(0f, 116f), new Vector2(0.5f, 0f), new Vector2(440f, 22f), new Color(1f, 0.35f, 0.05f));
            missileText = CreateHudText(parent, "MISSILE", new Vector2(-230f, 82f), new Vector2(0.5f, 0f), 17, TextAnchor.MiddleCenter);
            missileBar = CreateHudBar(parent, new Vector2(-230f, 60f), new Vector2(0.5f, 0f), new Vector2(180f, 14f), new Color(1f, 0.6f, 0.1f));
            dashText = CreateHudText(parent, "DASH", new Vector2(230f, 82f), new Vector2(0.5f, 0f), 17, TextAnchor.MiddleCenter);
            dashBar = CreateHudBar(parent, new Vector2(230f, 60f), new Vector2(0.5f, 0f), new Vector2(180f, 14f), AccentColor);
            crystalText = CreateHudText(parent, string.Empty, new Vector2(0f, 40f), new Vector2(0.5f, 0f), 21, TextAnchor.MiddleCenter);
            buildSelectionText = CreateHudText(parent, string.Empty, new Vector2(0f, 10f), new Vector2(0.5f, 0f), 18, TextAnchor.MiddleCenter);
            interactPromptText = CreateHudText(parent, string.Empty, new Vector2(0f, 188f), new Vector2(0.5f, 0f), 24, TextAnchor.MiddleCenter);
            interactPromptText.color = new Color(0.2f, 1f, 0.55f, 1f);

            Text guideBanner = CreateHudText(
                parent,
                "YUKARI/AŞAĞI OK (W/S): İleri/Geri  |  SOL/SAĞ OK (A/D veya Q/R): 360° Sola/Sağa Dön\n[C]: Kamera Açısı  |  [Z/X veya Tekerlek]: Uzaklaştır  |  [H]: İlaç (+150 Can)",
                new Vector2(0f, -18f),
                new Vector2(0.5f, 1f),
                15,
                TextAnchor.UpperCenter);
            guideBanner.GetComponent<RectTransform>().sizeDelta = new Vector2(760f, 52f);
            guideBanner.color = new Color(0.75f, 0.96f, 1f, 0.95f);

            cameraModeText = CreateHudText(
                parent,
                "[C] KAMERA: DENGELİ 3B SAVAŞ AÇISI  |  [Sol/Sağ Ok veya Q/R] 360° Döndür",
                new Vector2(28f, -158f),
                new Vector2(0f, 1f),
                16,
                TextAnchor.UpperLeft);
            cameraModeText.GetComponent<RectTransform>().sizeDelta = new Vector2(640f, 34f);
            cameraModeText.color = new Color(1f, 0.88f, 0.32f, 1f);

            cockpitCrosshairText = CreateHudText(
                parent,
                "[   +   ]",
                new Vector2(0f, 18f),
                new Vector2(0.5f, 0.5f),
                28,
                TextAnchor.MiddleCenter);
            cockpitCrosshairText.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 44f);
            cockpitCrosshairText.color = new Color(0.15f, 1f, 0.55f, 0.92f);
            cockpitCrosshairText.gameObject.SetActive(false);

            TacticalMinimapHUD minimap = parent.gameObject.AddComponent<TacticalMinimapHUD>();
            minimap.BuildRadar(parent, runtimeWhiteSprite, runtimeFont);

            waveAlertText = CreateHudText(
                parent,
                string.Empty,
                new Vector2(0f, -88f),
                new Vector2(0.5f, 1f),
                25,
                TextAnchor.UpperCenter);
            waveAlertText.GetComponent<RectTransform>().sizeDelta = new Vector2(920f, 54f);
            waveAlertText.gameObject.SetActive(false);

            damageVignette = parent.gameObject.AddComponent<DamageVignetteOverlay>();
            damageVignette.BuildOverlay(parent);

            castleUnderAttackText = CreateHudText(
                parent,
                "⚠ KALE SALDIRI ALTINDA! KORUMAYA DÖN! ⚠",
                new Vector2(0f, -138f),
                new Vector2(0.5f, 1f),
                24,
                TextAnchor.UpperCenter);
            castleUnderAttackText.GetComponent<RectTransform>().sizeDelta = new Vector2(920f, 48f);
            castleUnderAttackText.color = new Color(1f, 0.22f, 0.18f, 1f);
            castleUnderAttackText.gameObject.SetActive(false);
        }

        private void ResolveSystems()
        {
            gameManager = GameManager.Instance;
            inputReader = FindFirstObjectByType<PlayerInputReader>();
            mechHealth = FindHealthRole(HealthRole.PlayerMech);
            reactorHealth = FindHealthRole(HealthRole.Reactor);
            weaponSystem = FindFirstObjectByType<WeaponSystem>();
            mechController = FindFirstObjectByType<MechController>();
            resourceBank = CrystalResourceBank.Instance;
            buildManager = FindFirstObjectByType<TacticalBuildManager>();
            interactionScanner = FindFirstObjectByType<MechInteractionScanner>();
            upgradeManager = FindFirstObjectByType<RogueliteUpgradeManager>();
            waveSpawner = FindFirstObjectByType<WaveSpawnerManager>();
            audioManager = AudioManager.Instance;
            cameraRig = FindFirstObjectByType<IsometricCameraRig>();
            BindHealthEvents();
        }

        private void BindHealthEvents()
        {
            if (subscribedReactorHealth != reactorHealth)
            {
                if (subscribedReactorHealth != null)
                    subscribedReactorHealth.OnDamaged -= HandleReactorDamaged;
                subscribedReactorHealth = reactorHealth;
                if (subscribedReactorHealth != null)
                    subscribedReactorHealth.OnDamaged += HandleReactorDamaged;
            }

            if (subscribedMechHealth != mechHealth)
            {
                if (subscribedMechHealth != null)
                    subscribedMechHealth.OnDamaged -= HandleMechDamaged;
                subscribedMechHealth = mechHealth;
                if (subscribedMechHealth != null)
                    subscribedMechHealth.OnDamaged += HandleMechDamaged;
            }
        }

        private void UnbindHealthEvents()
        {
            if (subscribedReactorHealth != null)
            {
                subscribedReactorHealth.OnDamaged -= HandleReactorDamaged;
                subscribedReactorHealth = null;
            }
            if (subscribedMechHealth != null)
            {
                subscribedMechHealth.OnDamaged -= HandleMechDamaged;
                subscribedMechHealth = null;
            }
        }

        private void HandleReactorDamaged(HealthComponent target, float amount, GameObject instigator)
        {
            if (damageVignette != null)
                damageVignette.TriggerHitFlash(0.92f, true);
            castleUnderAttackTimer = 2.6f;
            audioManager?.PlayCastleUnderAttackAlert();
        }

        private void HandleMechDamaged(HealthComponent target, float amount, GameObject instigator)
        {
            if (damageVignette != null)
                damageVignette.TriggerHitFlash(0.65f, false);
        }

        private void ResolveSystemsIfNeeded()
        {
            if (gameManager == null || inputReader == null || mechHealth == null || reactorHealth == null ||
                weaponSystem == null || resourceBank == null || buildManager == null || interactionScanner == null ||
                upgradeManager == null || waveSpawner == null || audioManager == null || cameraRig == null)
            {
                ResolveSystems();
            }
        }

        private void UpdateHud()
        {
            if (hudPanel == null || !hudPanel.activeSelf)
                return;

            int currentLvl = gameManager != null ? Mathf.Clamp(gameManager.CurrentLevel, 1, 5) : 1;
            SetHealthBar(mechHealth, mechHealthBar, mechHealthText, $"TANK MK-{currentLvl} CANI");
            SetHealthBar(reactorHealth, reactorHealthBar, reactorHealthText, "KALE CANI (REAKTÖR)");
            UpdateDamageAlertFeedback();
            if (cameraRig != null)
            {
                if (cameraModeText != null)
                {
                    cameraModeText.text =
                        $"[C] KAMERA: {cameraRig.CurrentViewModeDisplayName}  |  [Sol/Sağ Ok veya Q/R] 360° Döndür";
                }
                if (cockpitCrosshairText != null)
                    cockpitCrosshairText.gameObject.SetActive(false);
            }
            if (gameManager != null)
            {
                int currentWave = Mathf.Max(1, gameManager.CurrentWave);
                if (currentWave != lastAnnouncedWave && gameManager.CurrentState == GameState.WaveActive)
                {
                    lastAnnouncedWave = currentWave;
                    waveAlertTimer = 3.8f;
                    if (waveAlertText != null)
                    {
                        waveAlertText.gameObject.SetActive(true);
                        if (currentWave == 5)
                        {
                            waveAlertText.color = new Color(1f, 0.25f, 0.2f, 1f);
                            waveAlertText.text = "⚠ DİKKAT: SON DALGA (5/5) — OVERLORD TITAN BOSS SALDIRIYOR! ⚠";
                        }
                        else if (currentWave == 3)
                        {
                            waveAlertText.color = new Color(1f, 0.78f, 0.2f, 1f);
                            waveAlertText.text = "⚡ DALGA 3/5: GECE FIRTINASI & KAMİKAZE BASKINI BAŞLADI! ⚡";
                        }
                        else
                        {
                            waveAlertText.color = new Color(0.25f, 0.95f, 1f, 1f);
                            waveAlertText.text = $"⚔ DALGA {currentWave}/5 BAŞLADI — KALEYİ SAVUN! ⚔";
                        }
                    }
                }

                if (waveAlertTimer > 0f && waveAlertText != null)
                {
                    waveAlertTimer = Mathf.Max(0f, waveAlertTimer - Time.deltaTime);
                    if (lastAnnouncedWave == 5)
                    {
                        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
                        waveAlertText.color = Color.Lerp(new Color(1f, 0.2f, 0.15f, 1f), new Color(1f, 0.92f, 0.2f, 1f), pulse);
                    }
                    if (waveAlertTimer <= 0f)
                        waveAlertText.gameObject.SetActive(false);
                }

                int remainingEnemies = waveSpawner != null ? waveSpawner.ActiveEnemyCount : 0;
                waveText.text = $"SEVİYE {gameManager.CurrentLevel}/5  •  DALGA {currentWave}/5 (5. Dalga Bitince Kazanırsın)";
                timerText.text = $"KALAN DÜŞMAN: {remainingEnemies}   |   SÜRE: {FormatTime(gameManager.ElapsedSeconds)}";
                scoreText.text = $"TOPLAM SKOR: {gameManager.TotalScore:N0}";
                terminalText.text = $"BONUS UYDU: {gameManager.CompletedTerminals}/3 (+75 Kristal & +Can)";
            }

            if (weaponSystem != null)
            {
                heatBar.value = weaponSystem.MaximumHeat <= 0f ? 0f : weaponSystem.CurrentHeat / weaponSystem.MaximumHeat;
                heatText.text = weaponSystem.IsOverheated
                    ? $"AŞIRI ISINMA - KİLİTLİ ({weaponSystem.OverheatRemaining:0.0}s)"
                    : $"ISI {weaponSystem.CurrentHeat:0}/{weaponSystem.MaximumHeat:0}";
                heatText.color = weaponSystem.IsOverheated ? Color.red : Color.white;
                missileBar.value = weaponSystem.MissileCooldownNormalized;
                missileText.text = weaponSystem.MissileCooldownRemaining > 0f
                    ? $"FÜZE {weaponSystem.MissileCooldownRemaining:0.0}s"
                    : "FÜZE HAZIR";
            }

            if (mechController != null)
            {
                dashBar.value = mechController.DashCooldownNormalized;
                dashText.text = mechController.DashCooldownRemaining > 0f
                    ? $"DASH {mechController.DashCooldownRemaining:0.0}s"
                    : "DASH HAZIR";
            }

            if (resourceBank != null)
                crystalText.text = $"ENERJİ KRİSTALİ: {resourceBank.CurrentBalance}";
            if (buildManager != null)
                buildSelectionText.text = $"[1] LAZER 50   [2] EMP 75   [3] ONARIM 100   [H] İLAÇ / CAN DOLDUR (+150 CAN)   | SEÇİLİ: {buildManager.SelectedStructureType}";
            if (interactionScanner != null)
            {
                bool lowMech = mechHealth != null && mechHealth.HealthRatio < 0.38f;
                bool lowReactor = reactorHealth != null && reactorHealth.HealthRatio < 0.38f;
                if (!string.IsNullOrEmpty(interactionScanner.CurrentPrompt))
                {
                    interactPromptText.color = new Color(0.2f, 1f, 0.55f, 1f);
                    interactPromptText.text = $"[E] {interactionScanner.CurrentPrompt}";
                }
                else if (lowMech)
                {
                    interactPromptText.color = new Color(1f, 0.35f, 0.25f, 1f);
                    interactPromptText.text = "DİKKAT: ROBOT CANIN DÜŞÜK! Düşmanlardan Uzaklaş veya [3]+[E] ile Onarım Kulesi Kur!";
                }
                else if (lowReactor)
                {
                    interactPromptText.color = new Color(1f, 0.35f, 0.25f, 1f);
                    interactPromptText.text = "DİKKAT: REAKTÖR CANI DÜŞÜK! Ortadaki Mavi Reaktörü Koru!";
                }
                else
                {
                    interactPromptText.text = string.Empty;
                }
            }
        }

        private void UpdateDamageAlertFeedback()
        {
            bool lowMech = mechHealth != null && !mechHealth.IsDead && mechHealth.HealthRatio < 0.35f;
            bool lowReactor = reactorHealth != null && !reactorHealth.IsDead && reactorHealth.HealthRatio < 0.35f;
            if (damageVignette != null)
                damageVignette.SetCriticalPulse(lowMech || lowReactor || castleUnderAttackTimer > 0.1f, lowReactor ? 7.5f : 5.5f);

            if (castleUnderAttackText == null)
                return;

            if (castleUnderAttackTimer > 0f)
            {
                castleUnderAttackTimer = Mathf.Max(0f, castleUnderAttackTimer - Time.deltaTime);
                if (!castleUnderAttackText.gameObject.activeSelf)
                    castleUnderAttackText.gameObject.SetActive(true);

                float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f);
                castleUnderAttackText.color = Color.Lerp(
                    new Color(1f, 0.16f, 0.14f, 0.92f),
                    new Color(1f, 0.85f, 0.2f, 1f),
                    blink);
                int reactorHp = reactorHealth != null ? Mathf.CeilToInt(reactorHealth.CurrentHealth) : 0;
                castleUnderAttackText.text = $"⚠ KALE SALDIRI ALTINDA! (KALAN CAN: {reactorHp}) ⚠";
            }
            else if (castleUnderAttackText.gameObject.activeSelf)
            {
                castleUnderAttackText.gameObject.SetActive(false);
            }
        }

        private void HandleGameStateChanged(GameStateChangedEvent change)
        {
            SetVisibleState(change.CurrentState);
            if (change.CurrentState == GameState.Victory || change.CurrentState == GameState.Defeat)
                UpdateResultSummary(change.CurrentState);
        }

        private void HandleUpgradeOptionsPresented(UpgradeModuleCardConfig[] cards)
        {
            presentedCards = cards ?? Array.Empty<UpgradeModuleCardConfig>();
            for (int i = 0; i < upgradeCardTexts.Length; i++)
            {
                if (i < presentedCards.Length && presentedCards[i] != null)
                    upgradeCardTexts[i].text = $"{i + 1}.  {presentedCards[i].DisplayName}\n\n{presentedCards[i].Description}";
                else
                    upgradeCardTexts[i].text = "KART YOK";
            }
        }

        private void SelectUpgrade(int optionIndex)
        {
            if (upgradeManager != null)
                upgradeManager.SelectUpgrade(optionIndex);
        }

        private void SetVisibleState(GameState state)
        {
            SetAllPanelsInactive();
            SetPanel(mainPanel, state == GameState.MainMenu);
            SetPanel(briefingPanel, state == GameState.Briefing);
            SetPanel(hudPanel, state == GameState.WaveActive);
            SetPanel(upgradePanel, state == GameState.WaveUpgradeSelection);
            SetPanel(pausePanel, state == GameState.Paused);
            SetPanel(resultPanel, state == GameState.Victory || state == GameState.Defeat);
            SetPanel(levelSelectPanel, false);
            SetPanel(howToPanel, false);
            SetPanel(settingsPanel, false);
            if (state == GameState.Briefing)
                UpdateBriefingTexts();
            if (state == GameState.Victory || state == GameState.Defeat)
                UpdateResultSummary(state);
        }

        private void SetAllPanelsInactive()
        {
            SetPanel(mainPanel, false);
            SetPanel(levelSelectPanel, false);
            SetPanel(briefingPanel, false);
            SetPanel(howToPanel, false);
            SetPanel(settingsPanel, false);
            SetPanel(hudPanel, false);
            SetPanel(upgradePanel, false);
            SetPanel(pausePanel, false);
            SetPanel(resultPanel, false);
        }

        private void UpdateResultSummary(GameState state)
        {
            if (resultTitle == null || resultDetails == null || gameManager == null)
                return;

            bool victory = state == GameState.Victory;
            bool hasNext = victory && gameManager.HasNextLevel;
            if (nextLevelButtonObject != null)
                nextLevelButtonObject.SetActive(hasNext);

            if (resultTopBannerImage != null)
            {
                resultTopBannerImage.color = victory
                    ? new Color(0.95f, 0.72f, 0.12f, 0.98f)
                    : new Color(0.75f, 0.15f, 0.15f, 0.96f);
            }

            if (resultBadgeText != null)
            {
                resultBadgeText.color = victory ? new Color(0.06f, 0.08f, 0.12f, 1f) : Color.white;
                resultBadgeText.text = victory
                    ? $"★ ★ ★   TEBRİKLER KOMUTAN! SEVİYE {gameManager.CurrentLevel} ZAFERİ!   ★ ★ ★"
                    : $"[ ! ]   SİSTEM UYARISI — SEVİYE {gameManager.CurrentLevel} SAVUNMASI DÜŞTÜ   [ ! ]";
            }

            if (resultUnlockBox != null)
                resultUnlockBox.SetActive(victory);

            if (victory && resultUnlockText != null)
            {
                if (hasNext)
                {
                    SectorLevelData nextData = SectorLevelConfig.GetLevel(gameManager.CurrentLevel + 1);
                    if (nextLevelButtonText != null)
                        nextLevelButtonText.text = $"► SONRAKİ SEVİYEYE GEÇ ({nextData.SectorName}) ◄";

                    resultUnlockText.text =
                        $"★ YENİ SEVİYE VE YENİ SİLAH EVRİMİ AÇILDI! ★\n" +
                        $"► AÇILAN SEKTÖR: {nextData.SectorName} ({nextData.Subtitle})\n" +
                        $"► YENİ {nextData.WeaponEvolutionTitle}\n" +
                        $"► YENİ {nextData.SpecialPerkTitle}";
                }
                else
                {
                    resultUnlockText.text =
                        "★ ★ ★   EFSANEVİ KOMUTAN MADALYASI KAZANILDI!   ★ ★ ★\n" +
                        "Tüm 5 Sektörü ve Overlord Ana Üssünü Başarıyla Kurtardın!";
                }
            }

            resultTitle.text = victory
                ? (hasNext
                    ? $"♦ SEVİYE {gameManager.CurrentLevel} TAMAMLANDI  •  SEVİYE {gameManager.CurrentLevel + 1} KİLİDİ AÇILDI! ♦"
                    : "★ TÜM 5 SEKTÖR KURTARILDI: KUSURSUZ ZAFER! ★")
                : $"SEVİYE {gameManager.CurrentLevel} BAŞARISIZ OLDU";
            resultTitle.color = victory ? new Color(0.3f, 1f, 0.65f, 1f) : new Color(1f, 0.35f, 0.3f);

            string reasonLine = victory
                ? "✓ BAŞARI RAPORU: 5 Dalgadaki Tüm Düşmanlar Yok Edildi ve Kuantum Reaktörü Korundu!"
                : (!string.IsNullOrEmpty(gameManager.FailureReason)
                    ? gameManager.FailureReason
                    : "KAYBETME NEDENİ: Robot veya Reaktör Canı Sıfırlandı!");

            resultDetails.text =
                $"{reasonLine}\n\n" +
                $"★ TAMAMLANAN SEKTÖR: {gameManager.CurrentLevelData.SectorName}\n" +
                $"★ TOPLAM SKOR: {gameManager.TotalScore:N0} PUAN   |   GEÇEN SÜRE: {FormatTime(gameManager.ElapsedSeconds)}\n" +
                $"★ SAVUNULAN DALGA: {gameManager.CompletedWaves}/5   |   BONUS UYDU HACK: {gameManager.CompletedTerminals}/3";
        }

        private void ShowAuxiliaryPanel(GameObject panel)
        {
            stateBeforeAuxiliaryPanel = gameManager != null ? gameManager.CurrentState : GameState.MainMenu;
            SetAllPanelsInactive();
            SetPanel(panel, true);
        }

        private void RestartCurrentScene()
        {
            if (gameManager != null)
                gameManager.StartRun();
            ReloadActiveScene();
        }

        private void ReturnToMainMenu()
        {
            if (gameManager != null)
                gameManager.ReturnToMainMenu();
            ReloadActiveScene();
        }

        private GameObject CreatePanel(string name, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasTransform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            Stretch(rect);
            Image image = panel.GetComponent<Image>();
            image.sprite = runtimeWhiteSprite;
            image.color = color;
            image.raycastTarget = color.a > 0.05f;
            return panel;
        }

        private Transform CreateVerticalContent(Transform parent, float width, float height)
        {
            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            content.transform.SetParent(parent, false);
            RectTransform rect = content.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            Image cardBg = content.GetComponent<Image>();
            cardBg.sprite = runtimeWhiteSprite;
            cardBg.color = new Color(0.01f, 0.03f, 0.065f, 0.90f);
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 28, 28);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return content.transform;
        }

        private Text CreateText(Transform parent, string value, int size, Color color, float preferredHeight, FontStyle style)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, preferredHeight);
            Text text = textObject.GetComponent<Text>();
            text.font = runtimeFont;
            text.text = value;
            text.fontSize = size;
            text.lineSpacing = 1.18f;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            LayoutElement layoutElement = textObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = preferredHeight;
            layoutElement.preferredHeight = preferredHeight;
            return text;
        }

        private Text AddButton(Transform parent, string label, Action onClick, float height, int fontSize = 22)
        {
            GameObject buttonObject = new GameObject("Button - " + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(0f, height);
            Image image = buttonObject.GetComponent<Image>();
            image.sprite = runtimeWhiteSprite;
            image.color = PanelColor;
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = PanelColor;
            colors.highlightedColor = new Color(0.05f, 0.35f, 0.48f, 1f);
            colors.pressedColor = AccentColor;
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            Stretch(labelObject.GetComponent<RectTransform>());
            Text text = labelObject.GetComponent<Text>();
            text.font = runtimeFont;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = label;

            button.onClick.AddListener(() => AudioManager.Instance?.PlayUiClick());
            button.onClick.AddListener(() => onClick?.Invoke());
            return text;
        }

        private void AddVolumeSlider(Transform parent, string label, float defaultValue, Action<float> changed)
        {
            CreateText(parent, label, 19, Color.white, 32f, FontStyle.Bold);
            GameObject sliderObject = new GameObject("Slider " + label, typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            sliderObject.transform.SetParent(parent, false);
            LayoutElement sliderLayout = sliderObject.GetComponent<LayoutElement>();
            sliderLayout.minHeight = 32f;
            sliderLayout.preferredHeight = 32f;
            RectTransform rect = sliderObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(600f, 32f);
            Image background = sliderObject.AddComponent<Image>();
            background.sprite = runtimeWhiteSprite;
            background.color = PanelColor;
            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(sliderObject.transform, false);
            Image fill = fillObject.GetComponent<Image>();
            fill.sprite = runtimeWhiteSprite;
            fill.color = AccentColor;
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fillRect;
            slider.targetGraphic = background;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = defaultValue;
            slider.onValueChanged.AddListener(value => changed?.Invoke(value));
        }

        private Text CreateHudText(Transform parent, string value, Vector2 position, Vector2 anchor, int size, TextAnchor alignment)
        {
            GameObject textObject = new GameObject("HUD Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor.x > 0.5f ? new Vector2(1f, 1f) : new Vector2(anchor.x, anchor.y > 0.5f ? 1f : 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(700f, 34f);
            Text text = textObject.GetComponent<Text>();
            text.font = runtimeFont;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private Slider CreateHudBar(Transform parent, Vector2 position, Vector2 anchor, Vector2 size, Color fillColor)
        {
            GameObject barObject = new GameObject("HUD Bar", typeof(RectTransform), typeof(Image), typeof(Slider));
            barObject.transform.SetParent(parent, false);
            RectTransform rect = barObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor.y > 0.5f ? new Vector2(anchor.x, 1f) : new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image background = barObject.GetComponent<Image>();
            background.sprite = runtimeWhiteSprite;
            background.color = PanelColor;
            background.raycastTarget = false;
            GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObject.transform.SetParent(barObject.transform, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            Image fill = fillObject.GetComponent<Image>();
            fill.sprite = runtimeWhiteSprite;
            fill.color = fillColor;
            Slider slider = barObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.interactable = false;
            slider.fillRect = fillRect;
            slider.targetGraphic = background;
            return slider;
        }

        private void SetHealthBar(HealthComponent health, Slider bar, Text label, string title)
        {
            if (health == null)
            {
                bar.value = 0f;
                label.text = $"{title} HP --";
                return;
            }
            bar.value = health.HealthRatio;
            label.text = $"{title} HP {health.CurrentHealth:0}/{health.MaxHealth:0}";
        }

        private static void SetPanel(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static HealthComponent FindHealthRole(HealthRole role)
        {
            HealthComponent[] components = FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].Role == role && !components[i].IsDead)
                    return components[i];
            }
            return null;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
