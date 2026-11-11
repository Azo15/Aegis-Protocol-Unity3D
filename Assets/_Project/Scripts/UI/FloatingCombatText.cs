using System;
using UnityEngine;
using UnityEngine.UI;

namespace AegisProtocol.UI
{
    /// <summary>
    /// Spawns rising, fading 3D world-space combat numbers (-35, KRİTİK!, +150 CAN) above hit or healed units.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FloatingCombatText : MonoBehaviour
    {
        private Text popupText;
        private Vector3 velocity;
        private float lifetime = 0.85f;
        private float elapsed;
        private Color baseColor;
        private Camera cachedCamera;

        private static Font sharedFont;

        public static void SpawnPopup(Vector3 worldPos, string message, Color color, int fontSize = 22)
        {
            if (!Application.isPlaying)
                return;

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

            GameObject root = new GameObject("FloatingCombatPopup", typeof(RectTransform), typeof(Canvas), typeof(FloatingCombatText));
            Vector2 randomJitter = UnityEngine.Random.insideUnitCircle * 0.45f;
            root.transform.position = worldPos + new Vector3(randomJitter.x, 0.25f, randomJitter.y);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 55;

            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 44f);
            rt.localScale = Vector3.one * 0.014f;

            GameObject textObj = new GameObject("PopupLabel", typeof(RectTransform), typeof(Text), typeof(Outline));
            textObj.transform.SetParent(rt, false);
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text txt = textObj.GetComponent<Text>();
            txt.font = sharedFont;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.text = message;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;

            Outline outline = textObj.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1.4f, -1.4f);

            FloatingCombatText popup = root.GetComponent<FloatingCombatText>();
            popup.popupText = txt;
            popup.baseColor = color;
            popup.velocity = new Vector3(randomJitter.x * 0.6f, 2.2f, randomJitter.y * 0.6f);
        }

        private void LateUpdate()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += velocity * Time.deltaTime;
            velocity.y = Mathf.Max(0.6f, velocity.y - 1.8f * Time.deltaTime);

            if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
                cachedCamera = Camera.main;
            if (cachedCamera != null)
                transform.rotation = cachedCamera.transform.rotation;

            if (popupText != null)
            {
                float alpha = Mathf.Clamp01(1f - Mathf.Pow(elapsed / lifetime, 1.8f));
                popupText.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            }
        }
    }
}
