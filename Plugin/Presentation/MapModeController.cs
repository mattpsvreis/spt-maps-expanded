using System.Linq;
using Comfort.Common;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Presentation.Overlays;
using EFT;
using EFT.UI;
using UnityEngine;

namespace MapsExtended.Presentation
{
    /// <summary>
    /// Owns Main / Mini / Peek / Hidden mode state, controllers, and layout math.
    /// </summary>
    internal sealed class MapModeController
    {
        private static readonly Vector2 MaskSizeModifierInRaid = new(0, -42f);
        private static readonly Vector2 MaskPositionInRaid = new(0, -20f);
        private static readonly Vector2 MaskSizeModifierOutOfRaid = new(0, -70f);
        private static readonly Vector2 MaskPositionOutOfRaid = new(0, -5f);

        private readonly MainMapController _main = new();
        private readonly MiniMapController _mini = new();

        internal MapDisplayMode Mode { get; set; } = MapDisplayMode.Hidden;
        internal bool IsShown { get; set; }
        internal bool ChangingMode { get; set; }
        internal bool ApplyingViewportState { get; set; }
        internal bool MapChanged { get; set; }
        internal bool TransitionAnimations { get; set; }
        internal float TransitionProgress { get; set; } = 1f;
        internal MapViewportState TransitionFromState { get; set; }
        internal MapLayout TransitionFromLayout { get; set; }
        internal MapLayout TargetLayout { get; set; }

        internal MainMapController Main => _main;
        internal MiniMapController Mini => _mini;
        internal bool IsTransitioning => TransitionProgress < 1f;

        internal struct MapLayout
        {
            internal Vector2 Position;
            internal Vector2 Size;
        }

        internal void ReadConfig()
        {
            _main.ReadConfig();
            _mini.ReadConfig();
            TransitionAnimations = Settings.MapTransitionEnabled.Value;
        }

        internal void Invalidate(MapDef definition)
        {
            _main.Invalidate(definition);
            _mini.Invalidate(definition);
        }

        internal void CloseCurrent()
        {
            switch (Mode)
            {
                case MapDisplayMode.Main:
                case MapDisplayMode.Peek:
                    _main.Close();
                    break;
                case MapDisplayMode.Mini:
                    _mini.Close();
                    break;
            }
        }

        internal void ClosePrevious(MapDisplayMode previousMode)
        {
            if (previousMode == MapDisplayMode.Mini)
            {
                _mini.Close();
            }
            else if (previousMode == MapDisplayMode.Main || previousMode == MapDisplayMode.Peek)
            {
                _main.Close();
            }
        }

        internal void UpdateActiveMap(MapView mapView, Player player, bool allowInput, bool dropdownOpen)
        {
            switch (Mode)
            {
                case MapDisplayMode.Mini:
                    _mini.UpdateMap(mapView, player, Time.deltaTime, allowInput);
                    break;
                case MapDisplayMode.Main:
                case MapDisplayMode.Peek:
                    _main.UpdateMap(mapView, player, Time.deltaTime, Mode == MapDisplayMode.Peek, allowInput,
                        Mode == MapDisplayMode.Main && dropdownOpen);
                    break;
            }
        }

        internal MapViewportState DestinationViewportState()
        {
            return Mode == MapDisplayMode.Mini ? _mini.State : _main.State;
        }

        internal MapLayout GetLayout(MapDisplayMode mode)
        {
            var size = Singleton<CommonUI>.Instance.InventoryScreen.GetRectTransform().rect.size;
            if (mode == MapDisplayMode.Main)
            {
                return new MapLayout
                {
                    Size = size + (GameUtils.IsInRaid() ? MaskSizeModifierInRaid : MaskSizeModifierOutOfRaid),
                    Position = GameUtils.IsInRaid() ? MaskPositionInRaid : MaskPositionOutOfRaid
                };
            }

            if (mode == MapDisplayMode.Peek)
            {
                return new MapLayout
                {
                    Size = size,
                    Position = Vector2.zero
                };
            }

            var miniSize = new Vector2(Settings.MiniMapSizeX.Value, Settings.MiniMapSizeY.Value);
            var corner = ConvertEnumToScreenPos(Settings.MiniMapPosition.Value);
            var offset = new Vector2(Settings.MiniMapScreenOffsetX.Value, Settings.MiniMapScreenOffsetY.Value)
                * ConvertEnumToScenePivot(Settings.MiniMapPosition.Value);
            return new MapLayout
            {
                Size = miniSize,
                Position = (corner - new Vector2(0.5f, 0.5f)) * size + offset
                    + (new Vector2(0.5f, 0.5f) - corner) * miniSize
            };
        }

        internal static Vector2 ConvertEnumToScreenPos(EMiniMapPosition pos)
        {
            return pos switch
            {
                EMiniMapPosition.TopRight => new Vector2(1, 1),
                EMiniMapPosition.BottomRight => new Vector2(1, 0),
                EMiniMapPosition.TopLeft => new Vector2(0, 1),
                EMiniMapPosition.BottomLeft => new Vector2(0, 0),
                _ => Vector2.zero
            };
        }

        internal static Vector2 ConvertEnumToScenePivot(EMiniMapPosition pos)
        {
            return pos switch
            {
                EMiniMapPosition.TopRight => new Vector2(-1, -1),
                EMiniMapPosition.BottomRight => new Vector2(-1, 1),
                EMiniMapPosition.TopLeft => new Vector2(1, -1),
                EMiniMapPosition.BottomLeft => new Vector2(1, 1),
                _ => Vector2.zero
            };
        }

        internal static bool HasMultipleLevels(MapDef definition)
        {
            return definition.Layers.Values.Select(layer => layer.Level).Distinct().Count() > 1;
        }
    }
}
