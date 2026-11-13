using UnityEngine;
using UnityEngine.UI;

namespace AegisProtocol.UI
{
    /// <summary>
    /// Renders a PUBG-style full-screen red edge vignette flash when the Castle or Player Mech
    /// takes damage, plus a continuous pulsing red alert border when health is critically low.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageVignetteOverlay : MonoBehaviour
    {
        private Image vignetteImage;
        private Texture2D vignetteTexture;
        private Sprite vignetteSprite;
        private float flashAlpha;
        private float decaySpeed = 1.9f;
        private bool criticalPulseActive;
        private float criticalPulseSpeed = 5.5f;

        public void BuildOverlay(Transform hudParent)
        {
            if (vignetteImage != null)
                return;

            CreateVignetteSprite();

            GameObject overlayObj = new GameObject("DamageVignetteOverlay", typeof(RectTransform), typeof(Image));
            overlayObj.transform.SetParent(hudParent, false);
            overlayObj.transform.SetAsFirstSibling();

            RectTransform rect = overlayObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            vignetteImage = overlayObj.GetComponent<Image>();
            vignetteImage.sprite = vignetteSprite;
            vignetteImage.type = Image.Type.Simple;
            vignetteImage.preserveAspect = false;
            vignetteImage.raycastTarget = false;
            vignetteImage.color = new Color(0.95f, 0.06f, 0.06f, 0f);
        }

        /// <summary>Triggers a bright red edge flash (stronger and longer for Castle hits).</summary>
        public void TriggerHitFlash(float intensity = 0.85f, bool isCastleHit = true)
        {
            float target = Mathf.Clamp(intensity, 0.25f, 0.96f);
            flashAlpha = Mathf.Max(flashAlpha, target);
            decaySpeed = isCastleHit ? 1.45f : 2.35f;
        }

        /// <summary>Enables continuous PUBG-style pulsing red border when Castle or Mech HP is critical.</summary>
        public void SetCriticalPulse(bool active, float pulseSpeed = 5.5f)
        {
            criticalPulseActive = active;
            criticalPulseSpeed = Mathf.Max(1f, pulseSpeed);
        }

        private void Update()
        {
            if (vignetteImage == null)
                return;

            if (flashAlpha > 0f)
                flashAlpha = Mathf.Max(0f, flashAlpha - decaySpeed * Time.unscaledDeltaTime);

            float pulseAlpha = 0f;
            if (criticalPulseActive)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * criticalPulseSpeed);
                pulseAlpha = Mathf.Lerp(0.18f, 0.58f, wave);
            }

            float finalAlpha = Mathf.Max(flashAlpha, pulseAlpha);
            vignetteImage.color = new Color(0.95f, 0.05f, 0.05f, finalAlpha);
        }

        private void OnDestroy()
        {
            if (vignetteSprite != null)
                Destroy(vignetteSprite);
            if (vignetteTexture != null)
                Destroy(vignetteTexture);
        }

        private void CreateVignetteSprite()
        {
            const int size = 128;
            vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "DamageVignetteTexture",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float edgeDist = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                    float radialDist = Mathf.Sqrt(nx * nx + ny * ny) * 0.78f;
                    float combined = Mathf.Max(edgeDist, radialDist);
                    float alpha = Mathf.SmoothStep(0.42f, 1.0f, combined);
                    alpha = Mathf.Pow(alpha, 1.45f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            vignetteTexture.SetPixels(pixels);
            vignetteTexture.Apply();
            vignetteSprite = Sprite.Create(vignetteTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            vignetteSprite.hideFlags = HideFlags.DontSave;
        }
    }
}

