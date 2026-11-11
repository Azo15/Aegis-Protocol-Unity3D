using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using AegisProtocol.Core;
using AegisProtocol.Systems;

namespace AegisProtocol.UI
{
    /// <summary>
    /// Renders a real-time 2D tactical radar on the HUD showing the Castle, Player Tank,
    /// Satellite Terminals, and all active Enemies/Bosses across the 100x100m arena.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TacticalMinimapHUD : MonoBehaviour
    {
        private const float ArenaHalfExtent = 48f;
        private const float RadarSizePixels = 176f;

        private RectTransform radarContainer;
        private RectTransform playerBlip;
        private readonly List<RectTransform> enemyBlipPool = new List<RectTransform>();
        private readonly List<RectTransform> terminalBlips = new List<RectTransform>();
        private readonly List<SatelliteTerminalObjective> cachedTerminals = new List<SatelliteTerminalObjective>();
        private Sprite whiteSprite;
        private Font radarFont;
        private float nextCacheRefreshTime;

        public void BuildRadar(Transform hudParent, Sprite sprite, Font font)
        {
            whiteSprite = sprite;
            radarFont = font;

            GameObject frameObj = new GameObject("TacticalRadarMinimap", typeof(RectTransform), typeof(Image), typeof(Outline));
            frameObj.transform.SetParent(hudParent, false);
            radarContainer = frameObj.GetComponent<RectTransform>();
            radarContainer.anchorMin = new Vector2(1f, 0f);
            radarContainer.anchorMax = new Vector2(1f, 0f);
            radarContainer.pivot = new Vector2(1f, 0f);
            radarContainer.anchoredPosition = new Vector2(-28f, 26f);
            radarContainer.sizeDelta = new Vector2(RadarSizePixels, RadarSizePixels);

            Image bg = frameObj.GetComponent<Image>();
            bg.sprite = whiteSprite;
            bg.color = new Color(0.02f, 0.06f, 0.11f, 0.88f);
            bg.raycastTarget = false;

            Outline border = frameObj.GetComponent<Outline>();
            border.effectColor = new Color(0.12f, 0.85f, 1f, 0.92f);
            border.effectDistance = new Vector2(2f, -2f);

            // Crosshair grid lines inside radar
            CreateRadarLine("GridH", new Vector2(RadarSizePixels - 8f, 1.2f), new Color(0.1f, 0.55f, 0.7f, 0.35f));
            CreateRadarLine("GridV", new Vector2(1.2f, RadarSizePixels - 8f), new Color(0.1f, 0.55f, 0.7f, 0.35f));

            // Header title
            GameObject titleObj = new GameObject("RadarTitle", typeof(RectTransform), typeof(Text));
            titleObj.transform.SetParent(radarContainer, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 0f);
            titleRect.anchoredPosition = new Vector2(0f, 4f);
            titleRect.sizeDelta = new Vector2(0f, 22f);
            Text titleText = titleObj.GetComponent<Text>();
            titleText.font = radarFont;
            titleText.fontSize = 14;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.25f, 0.92f, 1f, 0.95f);
            titleText.text = "📡 TAKTİK RADAR (HARİTA)";
            titleText.raycastTarget = false;

            // Central Castle Blip
            RectTransform castleBlip = CreateBlip("CastleBlip", new Vector2(11f, 11f), new Color(0.15f, 0.92f, 1f, 1f));
            castleBlip.anchoredPosition = Vector2.zero;

            // Player Tank Blip
            playerBlip = CreateBlip("PlayerTankBlip", new Vector2(9f, 9f), new Color(0.2f, 1f, 0.35f, 1f));
        }

        private void Update()
        {
            if (radarContainer == null || !radarContainer.gameObject.activeInHierarchy)
                return;

            RefreshTerminalBlips();
            RefreshUnitBlips();
        }

        private void RefreshTerminalBlips()
        {
            if (Time.time >= nextCacheRefreshTime || cachedTerminals.Count == 0)
            {
                nextCacheRefreshTime = Time.time + 2f;
                cachedTerminals.Clear();
                SatelliteTerminalObjective[] found = FindObjectsByType<SatelliteTerminalObjective>(FindObjectsSortMode.None);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null)
                        cachedTerminals.Add(found[i]);
                }
            }

            while (terminalBlips.Count < cachedTerminals.Count)
            {
                terminalBlips.Add(CreateBlip("TerminalBlip", new Vector2(7.5f, 7.5f), new Color(1f, 0.55f, 0.1f, 1f)));
            }

            for (int i = 0; i < terminalBlips.Count; i++)
            {
                if (i >= cachedTerminals.Count || cachedTerminals[i] == null)
                {
                    terminalBlips[i].gameObject.SetActive(false);
                    continue;
                }

                SatelliteTerminalObjective term = cachedTerminals[i];
                terminalBlips[i].gameObject.SetActive(true);
                terminalBlips[i].anchoredPosition = WorldToRadarPosition(term.transform.position);
                Image img = terminalBlips[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = term.IsCompleted
                        ? new Color(0.2f, 1f, 0.45f, 1f)
                        : new Color(1f, 0.58f, 0.12f, 1f);
                }
            }
        }

        private void RefreshUnitBlips()
        {
            HealthComponent[] allHealth = FindObjectsByType<HealthComponent>(FindObjectsSortMode.None);
            int enemyCount = 0;

            for (int i = 0; i < allHealth.Length; i++)
            {
                HealthComponent h = allHealth[i];
                if (h == null || h.IsDead)
                    continue;

                if (h.Role == HealthRole.PlayerMech)
                {
                    if (playerBlip != null)
                    {
                        playerBlip.anchoredPosition = WorldToRadarPosition(h.transform.position);
                        playerBlip.SetAsLastSibling();
                    }
                }
                else if (h.Role == HealthRole.Enemy || h.Role == HealthRole.Boss)
                {
                    RectTransform blip = GetOrCreateEnemyBlip(enemyCount);
                    blip.gameObject.SetActive(true);
                    blip.anchoredPosition = WorldToRadarPosition(h.transform.position);
                    bool isBoss = h.MaxHealth >= 800f || h.Role == HealthRole.Boss;
                    blip.sizeDelta = isBoss ? new Vector2(11f, 11f) : new Vector2(6.5f, 6.5f);
                    Image img = blip.GetComponent<Image>();
                    if (img != null)
                        img.color = isBoss ? new Color(1f, 0.15f, 0.85f, 1f) : new Color(1f, 0.22f, 0.22f, 1f);
                    enemyCount++;
                }
            }

            for (int i = enemyCount; i < enemyBlipPool.Count; i++)
            {
                if (enemyBlipPool[i] != null && enemyBlipPool[i].gameObject.activeSelf)
                    enemyBlipPool[i].gameObject.SetActive(false);
            }
        }

        private RectTransform GetOrCreateEnemyBlip(int index)
        {
            while (enemyBlipPool.Count <= index)
            {
                enemyBlipPool.Add(CreateBlip($"EnemyBlip_{enemyBlipPool.Count}", new Vector2(6.5f, 6.5f), new Color(1f, 0.22f, 0.22f, 1f)));
            }
            return enemyBlipPool[index];
        }

        private Vector2 WorldToRadarPosition(Vector3 worldPos)
        {
            float halfRadar = (RadarSizePixels * 0.5f) - 8f;
            float nx = Mathf.Clamp(worldPos.x / ArenaHalfExtent, -1f, 1f);
            float nz = Mathf.Clamp(worldPos.z / ArenaHalfExtent, -1f, 1f);
            return new Vector2(nx * halfRadar, nz * halfRadar);
        }

        private RectTransform CreateBlip(string blipName, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(blipName, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(radarContainer, false);
            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            Image img = obj.GetComponent<Image>();
            img.sprite = whiteSprite;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private void CreateRadarLine(string lineName, Vector2 size, Color color)
        {
            RectTransform line = CreateBlip(lineName, size, color);
            line.anchoredPosition = Vector2.zero;
        }
    }
}

