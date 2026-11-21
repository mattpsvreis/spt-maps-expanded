using BepInEx.Configuration;
using MapsExtended.Config;
using MapsExtended.Presentation;
using UnityEngine;

namespace MapsExtended.Presentation.Overlays
{
    internal class MapPeekComponent : MonoBehaviour
    {
        public RaidMapHost MapScreen { get; set; }
        internal RectTransform RectTransform { get; private set; }
        public KeyboardShortcut PeekShortcut { get; set; }
        public KeyboardShortcut HideMinimapShortcut { get; set; }
        public bool HoldForPeek { get; set; }
        public bool WasMiniMapActive { get; set; }

        private bool _cornerHidden;

        private bool Peeking => MapScreen != null && MapScreen.DisplayMode == MapDisplayMode.Peek;

        internal bool ShouldShowMiniMap => Settings.MiniMapEnabled.Value && !_cornerHidden;

        internal static MapPeekComponent Create(GameObject parent)
        {
            var go = UIUtils.CreateUIGameObject(parent, "MapPeek");
            go.GetRectTransform().sizeDelta = parent.GetRectTransform().sizeDelta;
            return go.AddComponent<MapPeekComponent>();
        }

        private void Awake()
        {
            RectTransform = gameObject.GetRectTransform();
        }

        private void Update()
        {
            if (MapScreen == null || MapScreen.IsShowingMapScreen)
            {
                return;
            }

            if (!MapScreen.EnsureInRaidMapAvailable())
            {
                WasMiniMapActive = false;
                MapScreen.RequestMode(MapDisplayMode.Hidden, false);
                return;
            }

            if (HideMinimapShortcut.BetterIsDown())
            {
                _cornerHidden = !_cornerHidden;
            }

            if (HoldForPeek)
            {
                if (PeekShortcut.BetterIsPressed())
                {
                    BeginPeek();
                }
                else if (Peeking)
                {
                    EndPeek();
                }
            }
            else if (PeekShortcut.BetterIsDown())
            {
                if (Peeking)
                {
                    EndPeek();
                }
                else
                {
                    BeginPeek();
                }
            }

            if (!Peeking)
            {
                MapScreen.RequestMode(ShouldShowMiniMap ? MapDisplayMode.Mini : MapDisplayMode.Hidden, true);
            }
        }

        private void BeginPeek()
        {
            if (Peeking)
            {
                return;
            }

            WasMiniMapActive = ShouldShowMiniMap;
            MapScreen.RequestMode(MapDisplayMode.Peek, true);
        }

        private void EndPeek()
        {
            if (Peeking)
            {
                MapScreen.RequestMode(ShouldShowMiniMap ? MapDisplayMode.Mini : MapDisplayMode.Hidden, true);
            }
        }
    }
}
