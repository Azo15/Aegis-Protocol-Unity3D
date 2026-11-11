using System;
using UnityEngine;
using UnityEngine.UI;
using AegisProtocol.Core;

namespace AegisProtocol.UI
{
    /// <summary>
    /// Renders a 3D world-space rectangular progress bar above any unit with a HealthComponent
    /// (Player Mech, Castle Reactor, Enemies, and Bosses), shrinking when hit and filling up when healed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldHealthBar : MonoBehaviour
    {
        private HealthComponent health;
        private RectTransform canvasRect;
        private RectTransform fillRect;
        private RectTransform trailRect;
        private Image fillImage;
        private Image trailImage;
        private Text labelText;
        private Camera cachedCamera;

        private float displayedTrailRatio = 1f;
        private float healFlashRemaining;
        private float worldHeightOffset = 2.2f;
        private float baseWorldScale = 0.011f;

        private static Sprite sharedWhiteSprite;
        private static Font sharedFont;

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            health = GetComponent<HealthComponent>();
            if (health == null)
            {
                enabled = false;
                return;
            }

            EnsureSharedResources();
            BuildWorldBarVisuals();

            health.OnHealthChanged += HandleHealthChanged;
            displayedTrailRatio = health.HealthRatio;
            RefreshBarImmediate();
        }

        private void OnDestroy()
        {
            if (health != null)
                health.OnHealthChanged -= HandleHealthChanged;
        }

        private void LateUpdate()
        {
            if (health == null || canvasRect == null)
                return;

            if (health.IsDead || !gameObject.activeInHierarchy)
            {
                if (canvasRect.gameObject.activeSelf)
                    canvasRect.gameObject.SetActive(false);
                return;
            }

            if (!canvasRect.gameObject.activeSelf)
                canvasRect.gameObject.SetActive(true);

            UpdateDimensionsForRole();

            if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
                cachedCamera = Camera.main;

            float parentScale = Mathf.Max(0.1f, transform.lossyScale.x);
            Vector3 barWorldPos = transform.position + Vector3.up * worldHeightOffset;
            if (health.Role == HealthRole.Reactor && cachedCamera != null)
            {
                Vector3 toCam = cachedCamera.transform.position - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.001f)
                    barWorldPos += toCam.normalized * 3.85f;
            }

            canvasRect.position = barWorldPos;
            canvasRect.localScale = Vector3.one * (baseWorldScale / parentScale);

            if (cachedCamera != null)
                canvasRect.rotation = cachedCamera.transform.rotation;

            float targetRatio = Mathf.Clamp01(health.HealthRatio);
            if (displayedTrailRatio > targetRatio)
            {
                displayedTrailRatio = Mathf.MoveTowards(displayedTrailRatio, targetRatio, 0.55f * Time.deltaTime);
            }
            else
            {
                displayedTrailRatio = targetRatio;
            }

            if (trailRect != null)
                trailRect.anchorMax = new Vector2(displayedTrailRatio, 1f);

            if (healFlashRemaining > 0f)
            {
                healFlashRemaining = Mathf.Max(0f, healFlashRemaining - Time.deltaTime);
                if (healFlashRemaining <= 0f)
                    UpdateFillColor(targetRatio);
            }
        }

        private void HandleHealthChanged(HealthComponent source, float previousHealth, float currentHealth)
        {
            float ratio = Mathf.Clamp01(source.HealthRatio);
            if (fillRect != null)
                fillRect.anchorMax = new Vector2(ratio, 1f);

            if (currentHealth > previousHealth)
            {
                healFlashRemaining = 0.45f;
                displayedTrailRatio = ratio;
                if (fillImage != null)
                    fillImage.color = new Color(0.25f, 1f, 0.55f, 1f);
            }
            else
            {
                UpdateFillColor(ratio);
            }

            UpdateLabelText();
        }

        private void RefreshBarImmediate()
        {
            if (health == null)
                return;

            float ratio = Mathf.Clamp01(health.HealthRatio);
            displayedTrailRatio = ratio;
            if (fillRect != null)
                fillRect.anchorMax = new Vector2(ratio, 1f);
            if (trailRect != null)
                trailRect.anchorMax = new Vector2(ratio, 1f);

            UpdateFillColor(ratio);
            UpdateLabelText();
        }

        private void UpdateDimensionsForRole()
        {
            if (health == null)
                return;

            switch (health.Role)
            {
                case HealthRole.Reactor:
                    worldHeightOffset = 3.65f;
                    baseWorldScale = 0.025f;
                    if (canvasRect != null)
                        canvasRect.sizeDelta = new Vector2(350f, 54f);
                    break;
                case HealthRole.PlayerMech:
                    worldHeightOffset = 2.55f;
                    baseWorldScale = 0.013f;
                    if (canvasRect != null)
                        canvasRect.sizeDelta = new Vector2(220f, 36f);
                    break;
                default:
                    if (health.MaxHealth >= 800f)
                    {
                        worldHeightOffset = 3.6f;
                        baseWorldScale = 0.0165f;
                        if (canvasRect != null)
                            canvasRect.sizeDelta = new Vector2(280f, 42f);
                    }
                    else if (health.MaxHealth >= 220f)
                    {
                        worldHeightOffset = 2.45f;
                        baseWorldScale = 0.012f;
                        if (canvasRect != null)
                            canvasRect.sizeDelta = new Vector2(220f, 34f);
                    }
                    else
                    {
                        worldHeightOffset = 1.85f;
                        baseWorldScale = 0.010f;
                        if (canvasRect != null)
                            canvasRect.sizeDelta = new Vector2(195f, 30f);
                    }
                    break;
            }
        }

        private void UpdateFillColor(float ratio)
        {
            if (fillImage == null)
                return;

            if ( ratio > 0.60f)
            {
                fillImage.color = health != null && health.Role == HealthRole.Reactor
                    ? new Color(0.12f, 0.95f, 0.65f, 1f)
                    : new Color(0.18f, 0.94f, 0.30f, 1f);
            }
            else if (ratio > 0.30f)
            {
                fillImage.color = new Color(1f, 0.78f, 0.12f, 1f);
            }
            else
            {
                fillImage.color = new Color(1f, 0.18f, 0.18f, 1f);
            }
        }

        private void UpdateLabelText()
        {
            if (labelText == null || health == null)
                return;

            int cur = Mathf.CeilToInt(health.CurrentHealth);
            int max = Mathf.CeilToInt(health.MaxHealth);
            int pct = Mathf.Clamp(Mathf.RoundToInt(health.HealthRatio * 100f), 0, 100);

            int stageTier = GameManager.Instance != null ? Mathf.Clamp(GameManager.Instance.CurrentLevel, 1, 5) : 1;

            switch (health.Role)
            {
                case HealthRole.PlayerMech:
                    labelText.text = healFlashRemaining > 0f
                        ? $"✚ CAN DOLDU! ({cur}/{max})"
                        : $"TANK MK-{stageTier}: {cur}/{max}";
                    break;
                case HealthRole.Reactor:
                    labelText.text = $"★ KALE CANI: {cur} / {max}  (%{pct}) ★";
                    break;
                default:
                    labelText.text = health.MaxHealth >= 800f
                        ? $"☠ MK-{stageTier} BOSS: {cur}/{max}"
                        : $"MK-{stageTier} • {cur}/{max}";
                    break;
            }
        }

        private void BuildWorldBarVisuals()
        {
            GameObject canvasObj = new GameObject("WorldHealthBarCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = health != null && health.Role == HealthRole.Reactor ? 40 : 25;

            canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(210f, 34f);
            UpdateDimensionsForRole();
            canvasRect.localScale = Vector3.one * baseWorldScale;

            bool isCastle = health != null && health.Role == HealthRole.Reactor;
            bool isPlayer = health != null && health.Role == HealthRole.PlayerMech;

            // High-contrast outer glow rim so the bar stands out clearly against glowing 3D geometry
            GameObject glowObj = new GameObject("OuterRim", typeof(RectTransform), typeof(Image));
            glowObj.transform.SetParent(canvasRect, false);
            RectTransform glowRect = glowObj.GetComponent<RectTransform>();
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = isCastle ? new Vector2(-5f, -5f) : new Vector2(-2.5f, -2.5f);
            glowRect.offsetMax = isCastle ? new Vector2(5f, 5f) : new Vector2(2.5f, 2.5f);
            Image glowImg = glowObj.GetComponent<Image>();
            glowImg.sprite = sharedWhiteSprite;
            glowImg.color = isCastle
                ? new Color(1f, 0.85f, 0.18f, 0.98f)
                : (isPlayer ? new Color(0.15f, 0.92f, 1f, 0.92f) : new Color(0.95f, 0.28f, 0.22f, 0.85f));

            if (isCastle)
            {
                GameObject bannerObj = new GameObject("CastleHeaderPlate", typeof(RectTransform), typeof(Image));
                bannerObj.transform.SetParent(canvasRect, false);
                RectTransform bannerRect = bannerObj.GetComponent<RectTransform>();
                bannerRect.anchorMin = new Vector2(0.08f, 1f);
                bannerRect.anchorMax = new Vector2(0.92f, 1f);
                bannerRect.pivot = new Vector2(0.5f, 0f);
                bannerRect.anchoredPosition = new Vector2(0f, 5f);
                bannerRect.sizeDelta = new Vector2(0f, 26f);
                Image bannerImg = bannerObj.GetComponent<Image>();
                bannerImg.sprite = sharedWhiteSprite;
                bannerImg.color = new Color(0.05f, 0.12f, 0.20f, 0.96f);

                GameObject bannerTextObj = new GameObject("CastleHeaderText", typeof(RectTransform), typeof(Text));
                bannerTextObj.transform.SetParent(bannerRect, false);
                RectTransform btRect = bannerTextObj.GetComponent<RectTransform>();
                btRect.anchorMin = Vector2.zero;
                btRect.anchorMax = Vector2.one;
                btRect.offsetMin = Vector2.zero;
                btRect.offsetMax = Vector2.zero;
                Text bt = bannerTextObj.GetComponent<Text>();
                bt.font = sharedFont;
                bt.fontSize = 16;
                bt.fontStyle = FontStyle.Bold;
                bt.alignment = TextAnchor.MiddleCenter;
                bt.color = new Color(1f, 0.90f, 0.25f, 1f);
                bt.text = "▼ KORUNAN ANA KALE (REAKTÖR) ▼";
            }

            // Dark outer frame
            GameObject borderObj = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(canvasRect, false);
            RectTransform borderRect = borderObj.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;
            Image borderImg = borderObj.GetComponent<Image>();
            borderImg.sprite = sharedWhiteSprite;
            borderImg.color = new Color(0.02f, 0.04f, 0.08f, 0.96f);

            // Empty dark-red track
            GameObject trackObj = new GameObject("Track", typeof(RectTransform), typeof(Image));
            trackObj.transform.SetParent(borderRect, false);
            RectTransform trackRect = trackObj.GetComponent<RectTransform>();
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = isCastle ? new Vector2(4.5f, 4.5f) : new Vector2(3f, 3f);
            trackRect.offsetMax = isCastle ? new Vector2(-4.5f, -4.5f) : new Vector2(-3f, -3f);
            Image trackImg = trackObj.GetComponent<Image>();
            trackImg.sprite = sharedWhiteSprite;
            trackImg.color = new Color(0.22f, 0.05f, 0.06f, 0.95f);

            // Delayed damage trail bar (white/gold shrink trail)
            GameObject trailObj = new GameObject("DamageTrail", typeof(RectTransform), typeof(Image));
            trailObj.transform.SetParent(trackRect, false);
            trailRect = trailObj.GetComponent<RectTransform>();
            trailRect.anchorMin = Vector2.zero;
            trailRect.anchorMax = Vector2.one;
            trailRect.offsetMin = Vector2.zero;
            trailRect.offsetMax = Vector2.zero;
            trailImage = trailObj.GetComponent<Image>();
            trailImage.sprite = sharedWhiteSprite;
            trailImage.color = new Color(1f, 0.88f, 0.38f, 0.92f);

            // Main rectangular health fill bar
            GameObject fillObj = new GameObject("HealthFill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(trackRect, false);
            fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillImage = fillObj.GetComponent<Image>();
            fillImage.sprite = sharedWhiteSprite;
            fillImage.color = new Color(0.18f, 0.94f, 0.30f, 1f);

            // Crisp numeric HP text overlay with dark outline for maximum legibility
            GameObject textObj = new GameObject("HPText", typeof(RectTransform), typeof(Text), typeof(Outline));
            textObj.transform.SetParent(borderRect, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            labelText = textObj.GetComponent<Text>();
            labelText.font = sharedFont;
            labelText.fontSize = isCastle ? 22 : 18;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;

            Outline textOutline = textObj.GetComponent<Outline>();
            textOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            textOutline.effectDistance = new Vector2(1.4f, -1.4f);
        }

        private static void EnsureSharedResources()
        {
            if (sharedWhiteSprite == null)
            {
                Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "WorldHealthBar White Pixel",
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
        }
    }
}

