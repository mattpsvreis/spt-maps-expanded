using System.Linq;
using Comfort.Common;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Presentation;
using MapsExtended.Presentation.Overlays;
using EFT;
using EFT.UI;
using UnityEngine;

namespace MapsExtended.App
{
    internal sealed class MapSession
    {
        private static readonly Vector2 InventoryShrinkInRaid = new(0, -42f);
        private static readonly Vector2 InventoryShiftInRaid = new(0, -20f);
        private static readonly Vector2 InventoryShrinkInHideout = new(0, -70f);
        private static readonly Vector2 InventoryShiftInHideout = new(0, -5f);

        private readonly InventoryCamera _inventory = new();
        private readonly CornerCamera _corner = new();

        internal MapDisplayMode Surface { get; set; } = MapDisplayMode.Hidden;
        internal bool Visible { get; set; }
        internal bool Switching { get; set; }
        internal bool WritingCamera { get; set; }
        internal bool MapSwapped { get; set; }
        internal bool Glide { get; set; }
        internal float GlideProgress { get; set; } = 1f;
        internal MapViewportState GlideFromCamera { get; set; }
        internal Frame GlideFromFrame { get; set; }
        internal Frame TargetFrame { get; set; }

        internal InventoryCamera Inventory => _inventory;
        internal CornerCamera Corner => _corner;
        internal bool Gliding => GlideProgress < 1f;

        internal struct Frame
        {
            internal Vector2 Position;
            internal Vector2 Size;
        }

        internal void ReadConfig()
        {
            _inventory.ReadConfig();
            _corner.ReadConfig();
            Glide = Settings.MapTransitionEnabled.Value;
        }

        internal void Forget(MapDef definition)
        {
            _inventory.Invalidate(definition);
            _corner.Invalidate(definition);
        }

        internal void ReleaseCurrent()
        {
            Release(Surface);
        }

        internal void Release(MapDisplayMode surface)
        {
            if (surface == MapDisplayMode.Mini)
            {
                _corner.Close();
            }
            else if (surface == MapDisplayMode.Main || surface == MapDisplayMode.Peek)
            {
                _inventory.Close();
            }
        }

        internal void Advance(MapCanvas map, Player player, bool allowInput, bool selectorOpen)
        {
            if (Surface == MapDisplayMode.Mini)
            {
                _corner.UpdateMap(map, player, Time.deltaTime, allowInput);
                return;
            }

            if (Surface == MapDisplayMode.Main || Surface == MapDisplayMode.Peek)
            {
                _inventory.UpdateMap(map, player, Time.deltaTime, Surface == MapDisplayMode.Peek, allowInput,
                    Surface == MapDisplayMode.Main && selectorOpen);
            }
        }

        internal MapViewportState CameraState()
        {
            return Surface == MapDisplayMode.Mini ? _corner.State : _inventory.State;
        }

        internal Frame FrameFor(MapDisplayMode surface)
        {
            var screen = Singleton<CommonUI>.Instance.InventoryScreen.GetRectTransform().rect.size;
            if (surface == MapDisplayMode.Main)
            {
                var inRaid = GameUtils.IsInRaid();
                return new Frame
                {
                    Size = screen + (inRaid ? InventoryShrinkInRaid : InventoryShrinkInHideout),
                    Position = inRaid ? InventoryShiftInRaid : InventoryShiftInHideout
                };
            }

            if (surface == MapDisplayMode.Peek)
            {
                return new Frame { Size = screen, Position = Vector2.zero };
            }

            var cornerSize = new Vector2(Settings.MiniMapSizeX.Value, Settings.MiniMapSizeY.Value);
            var anchor = CornerAnchor(Settings.MiniMapPosition.Value);
            var nudge = new Vector2(Settings.MiniMapScreenOffsetX.Value, Settings.MiniMapScreenOffsetY.Value)
                * CornerNudge(Settings.MiniMapPosition.Value);
            return new Frame
            {
                Size = cornerSize,
                Position = (anchor - new Vector2(0.5f, 0.5f)) * screen + nudge
                    + (new Vector2(0.5f, 0.5f) - anchor) * cornerSize
            };
        }

        internal static bool SpansFloors(MapDef definition)
        {
            return definition.Layers.Values.Select(layer => layer.Level).Distinct().Count() > 1;
        }

        private static Vector2 CornerAnchor(EMiniMapPosition corner)
        {
            return corner switch
            {
                EMiniMapPosition.TopRight => new Vector2(1, 1),
                EMiniMapPosition.BottomRight => new Vector2(1, 0),
                EMiniMapPosition.TopLeft => new Vector2(0, 1),
                EMiniMapPosition.BottomLeft => new Vector2(0, 0),
                _ => Vector2.zero
            };
        }

        private static Vector2 CornerNudge(EMiniMapPosition corner)
        {
            return corner switch
            {
                EMiniMapPosition.TopRight => new Vector2(-1, -1),
                EMiniMapPosition.BottomRight => new Vector2(-1, 1),
                EMiniMapPosition.TopLeft => new Vector2(1, -1),
                EMiniMapPosition.BottomLeft => new Vector2(1, 1),
                _ => Vector2.zero
            };
        }
    }
}
