using System.Linq;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using EFT;
using UnityEngine;

namespace MapsExtended.Presentation.Overlays
{
    internal readonly struct ZoomRange
    {
        internal float Min { get; }
        internal float Max { get; }

        internal ZoomRange(MapDef definition, Vector2 frame)
        {
            var size = MathUtils.GetRotatedRectangle(definition.Bounds.Max - definition.Bounds.Min, definition.CoordinateRotation);
            Min = Mathf.Min(frame.x / size.x, frame.y / size.y) / 1.1f;
            Max = Min * 10f;
        }

        internal float FromNormalized(float value)
        {
            return Mathf.Lerp(Min, Max, Mathf.Clamp01(value));
        }

        internal float ToNormalized(float zoom)
        {
            return Mathf.InverseLerp(Min, Max, Mathf.Clamp(zoom, Min, Max));
        }

        internal float Clamp(float zoom)
        {
            return Mathf.Clamp(zoom, Min, Max);
        }
    }

    internal sealed class InventoryCamera
    {
        private MapDef _definition;
        private ZoomRange _range;
        private bool _ready;
        private bool _keepPlace;
        private bool _placeKept;
        private bool _followPlayer;
        private bool _resetOnCenter;
        private bool _pickFloor;
        private bool _zoomAtCursor;
        private bool _zoomPending = true;
        private bool _zoomHeld;
        private float _savedZoom;
        private float _zoomRate;
        private float _resetZoom;
        private float _wheelTarget;
        private Vector2 _wheelPoint;
        private bool _sliding;
        private float _slideTime;
        private MapViewportState _slideFrom;
        private MapViewportState _slideTo;

        internal MapViewportState State;
        internal bool HasViewportStateFor(MapDef definition) => _ready && _definition == definition;

        internal void ReadConfig()
        {
            var zoom = Settings.ZoomMainMap.Value;
            if (!Mathf.Approximately(_savedZoom, zoom))
            {
                _savedZoom = zoom;
                _zoomPending = true;
            }

            _keepPlace = Settings.RetainMapPosition.Value;
            _followPlayer = Settings.AutoCenterOnPlayerMarker.Value;
            _resetOnCenter = Settings.ResetZoomOnCenter.Value;
            _pickFloor = Settings.AutoSelectLevel.Value;
            _zoomAtCursor = Settings.ZoomMainMapToMouse.Value;
            _zoomRate = Settings.ZoomMapHotkeySpeed.Value;
            _resetZoom = Settings.CenteringZoomResetPoint.Value;
        }

        internal void Invalidate(MapDef definition)
        {
            _definition = definition;
            _ready = false;
            _placeKept = false;
            _sliding = false;
            _wheelTarget = 0f;
            _zoomHeld = false;
            _zoomPending = true;
        }

        internal void Open(MapDef definition, Vector2 size, Player player, bool inRaid)
        {
            Fit(definition, size, inRaid ? SavedOrLive() : 0f);
            _zoomPending = !inRaid;
            _wheelTarget = 0f;
            _sliding = false;
            if (!inRaid)
            {
                State.Center = Mid(definition);
                _placeKept = false;
                return;
            }

            if (!(_keepPlace && _placeKept))
            {
                State.Center = Mid(definition);
                if (_followPlayer && player != null)
                {
                    AimAt(player, false);
                }
                else if (!_followPlayer && !_resetOnCenter && !_keepPlace)
                {
                    SetNormalized(0f);
                }
            }

            if (_pickFloor && player != null && !(_keepPlace && _placeKept))
            {
                State.Level = definition.DefaultLevel;
            }
        }

        internal void Resize(MapDef definition, Vector2 size)
        {
            var inRaid = GameUtils.IsInRaid();
            Fit(definition, size, inRaid ? SavedOrLive() : 0f);
            _zoomPending = !inRaid;
            if (!inRaid)
            {
                State.Center = Mid(definition);
            }
        }

        internal void Close()
        {
            _placeKept = _keepPlace && GameUtils.IsInRaid() && _ready;
            _wheelTarget = 0f;
            _sliding = false;
            if (_zoomHeld)
            {
                StoreZoom();
            }

            _zoomHeld = false;
        }

        internal void AcceptDrag(MapCanvas map)
        {
            if (!_ready)
            {
                return;
            }

            State.Center = Clamp(map.CaptureViewportState().Center);
            _wheelTarget = 0f;
            _sliding = false;
        }

        internal void ChangeLevel(int level)
        {
            State.Level = level;
        }

        internal void SelectPlayerLevel(MapCanvas map, Player player)
        {
            if (_pickFloor && player != null && !(_keepPlace && _placeKept))
            {
                State.Level = map.GetLevelAtPosition(MathUtils.ConvertToMapPosition(((IPlayer)player).Position), State.Level);
            }
        }

        internal void UpdateMap(MapCanvas map, Player player, float deltaTime, bool peek, bool allowInput, bool selectorOpen)
        {
            if (!_ready)
            {
                return;
            }

            if (_zoomPending && GameUtils.IsInRaid())
            {
                SetNormalized(_savedZoom);
                _zoomPending = false;
                _wheelTarget = 0f;
            }

            if (!allowInput)
            {
                return;
            }

            var floorStep = FloorStep(peek, selectorOpen);
            if (floorStep != 0)
            {
                StepFloor(floorStep);
            }

            if (GameUtils.IsInRaid() && Settings.CenterOnPlayerHotkey.Value.BetterIsDown() && player != null)
            {
                AimAt(player, Settings.MapTransitionEnabled.Value);
                _slideTo.Level = map.GetLevelAtPosition(MathUtils.ConvertToMapPosition(((IPlayer)player).Position), _slideTo.Level);
                if (!_sliding)
                {
                    State.Level = _slideTo.Level;
                }
            }

            var pan = Pan();
            if (pan != Vector2.zero)
            {
                _wheelTarget = 0f;
                _sliding = false;
                var bounds = _definition.Bounds;
                var step = Mathf.Min(bounds.Max.x - bounds.Min.x, bounds.Max.y - bounds.Min.y);
                State.Center -= MathUtils.GetRotatedVector2(pan, -_definition.CoordinateRotation)
                    * (step * Settings.MapMoveHotkeySpeed.Value * deltaTime);
                State.Center = Clamp(State.Center);
            }

            var keys = ZoomKeys();
            if (keys != 0f)
            {
                _wheelTarget = 0f;
                _sliding = false;
                ZoomAround(State.Zoom * (1f + keys * _zoomRate * deltaTime), ZoomPoint(map, player, peek));
                State.Center = Clamp(State.Center);
                _zoomHeld = true;
            }
            else
            {
                var wheel = peek || selectorOpen ? 0f : Input.GetAxis("Mouse ScrollWheel");
                if (Input.GetKey(KeyCode.LeftShift))
                {
                    wheel = 0f;
                }

                if (wheel != 0f)
                {
                    _sliding = false;
                    if (_wheelTarget == 0f)
                    {
                        _wheelTarget = State.Zoom;
                    }

                    _wheelTarget = Mathf.Clamp(_wheelTarget * (1f + wheel * 1.75f), _range.Min, _range.Max);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(map.RectTransform, Input.mousePosition, null, out _wheelPoint);
                    _zoomHeld = true;
                }
            }

            if (_wheelTarget != 0f)
            {
                var next = Mathf.Lerp(State.Zoom, _wheelTarget, 8f * deltaTime);
                if (Mathf.Abs(next - _wheelTarget) < 0.0001f)
                {
                    next = _wheelTarget;
                    _wheelTarget = 0f;
                }

                ZoomAround(next, _wheelPoint);
                State.Center = Clamp(State.Center);
            }

            if (_sliding)
            {
                _slideTime += deltaTime;
                var progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_slideTime / 0.25f));
                State = MapViewportState.Lerp(_slideFrom, _slideTo, progress);
                SetZoom(State.Zoom);
                if (progress >= 1f)
                {
                    _sliding = false;
                    SetZoom(_slideTo.Zoom);
                    StoreZoom();
                }
            }

            if (_zoomHeld && keys == 0f && _wheelTarget == 0f)
            {
                StoreZoom();
                _zoomHeld = false;
            }
        }

        private float SavedOrLive()
        {
            return _zoomPending || !_ready ? _savedZoom : Mathf.InverseLerp(_range.Min, _range.Max, State.Zoom);
        }

        private void Fit(MapDef definition, Vector2 size, float normalized)
        {
            if (!_ready || _definition != definition)
            {
                State.Center = Mid(definition);
                State.Level = definition.DefaultLevel;
            }

            _definition = definition;
            _range = new ZoomRange(definition, size);
            SetNormalized(normalized);
            _ready = true;
        }

        private void SetNormalized(float value)
        {
            State.Zoom = _range.FromNormalized(value);
        }

        private void SetZoom(float zoom)
        {
            State.Zoom = _range.Clamp(zoom);
        }

        private void ZoomAround(float zoom, Vector2 focus)
        {
            var before = State.Zoom;
            SetZoom(zoom);
            State.Center = focus + (State.Center - focus) * (before / State.Zoom);
        }

        private Vector2 Clamp(Vector2 center)
        {
            var bounds = _definition.Bounds;
            var size = MathUtils.GetRotatedRectangle(bounds.Max - bounds.Min, _definition.CoordinateRotation);
            var midpoint = MathUtils.GetRotatedVector2(Mid(_definition), _definition.CoordinateRotation);
            var rotated = MathUtils.GetRotatedVector2(center, _definition.CoordinateRotation);
            rotated.x = Mathf.Clamp(rotated.x, midpoint.x - size.x * 0.5f, midpoint.x + size.x * 0.5f);
            rotated.y = Mathf.Clamp(rotated.y, midpoint.y - size.y * 0.5f, midpoint.y + size.y * 0.5f);
            return MathUtils.GetRotatedVector2(rotated, -_definition.CoordinateRotation);
        }

        private void StepFloor(int delta)
        {
            var levels = _definition.Layers.Values.Select(layer => layer.Level).Distinct().OrderBy(level => level).ToArray();
            var index = System.Array.IndexOf(levels, State.Level) + delta;
            if (index >= 0 && index < levels.Length)
            {
                State.Level = levels[index];
            }
        }

        private static int FloorStep(bool peek, bool selectorOpen)
        {
            var step = 0;
            if (Settings.ChangeMapLevelUpHotkey.Value.BetterIsDown())
            {
                step++;
            }

            if (Settings.ChangeMapLevelDownHotkey.Value.BetterIsDown())
            {
                step--;
            }

            if (!peek && !selectorOpen && Input.GetKey(KeyCode.LeftShift))
            {
                var wheel = Input.GetAxis("Mouse ScrollWheel");
                if (wheel > 0f)
                {
                    step++;
                }
                else if (wheel < 0f)
                {
                    step--;
                }
            }

            return step;
        }

        private static Vector2 Pan()
        {
            var shift = Vector2.zero;
            if (Settings.MoveMapUpHotkey.Value.BetterIsPressed())
            {
                shift.y++;
            }

            if (Settings.MoveMapDownHotkey.Value.BetterIsPressed())
            {
                shift.y--;
            }

            if (Settings.MoveMapLeftHotkey.Value.BetterIsPressed())
            {
                shift.x--;
            }

            if (Settings.MoveMapRightHotkey.Value.BetterIsPressed())
            {
                shift.x++;
            }

            return shift;
        }

        private static float ZoomKeys()
        {
            var direction = 0f;
            if (Settings.ZoomMapInHotkey.Value.BetterIsPressed())
            {
                direction++;
            }

            if (Settings.ZoomMapOutHotkey.Value.BetterIsPressed())
            {
                direction--;
            }

            return direction;
        }

        private Vector2 ZoomPoint(MapCanvas map, Player player, bool peek)
        {
            if (peek && player != null)
            {
                return MathUtils.ConvertToMapPosition(((IPlayer)player).Position);
            }

            if (_zoomAtCursor)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(map.RectTransform, Input.mousePosition, null, out var local);
                return local;
            }

            return State.Center;
        }

        private void AimAt(Player player, bool animate)
        {
            _slideFrom = State;
            _slideTo = State;
            _slideTo.Center = MathUtils.ConvertToMapPosition(((IPlayer)player).Position);
            if (_resetOnCenter)
            {
                _slideTo.Zoom = Mathf.Lerp(_range.Min, _range.Max, _resetZoom);
            }

            _wheelTarget = 0f;
            _sliding = animate;
            _slideTime = 0f;
            if (!animate)
            {
                State = _slideTo;
                SetZoom(_slideTo.Zoom);
                StoreZoom();
            }
        }

        private void StoreZoom()
        {
            if (!GameUtils.IsInRaid() || !_ready)
            {
                return;
            }

            _savedZoom = _range.ToNormalized(State.Zoom);
            Settings.ZoomMainMap.Value = _savedZoom;
        }

        private static Vector2 Mid(MapDef definition)
        {
            return MathUtils.GetMidpoint(definition.Bounds.Min, definition.Bounds.Max);
        }
    }

    internal sealed class CornerCamera
    {
        private MapDef _definition;
        private ZoomRange _range;
        private bool _ready;
        private float _savedZoom;
        private bool _zoomPending = true;
        private bool _zoomHeld;
        private float _zoomRate;
        private float _followWait;

        internal MapViewportState State;

        internal void ReadConfig()
        {
            var zoom = Settings.ZoomMiniMap.Value;
            if (!Mathf.Approximately(_savedZoom, zoom))
            {
                _savedZoom = zoom;
                _zoomPending = true;
            }

            _zoomRate = Settings.ZoomMapHotkeySpeed.Value;
        }

        internal void Invalidate(MapDef definition)
        {
            _definition = definition;
            _ready = false;
            _zoomPending = true;
            _followWait = 0f;
            _zoomHeld = false;
        }

        internal void Open(MapDef definition, Vector2 size)
        {
            if (!_ready || _definition != definition)
            {
                State.Center = MathUtils.GetMidpoint(definition.Bounds.Min, definition.Bounds.Max);
                State.Level = definition.DefaultLevel;
            }

            _definition = definition;
            _range = new ZoomRange(definition, size);
            State.Zoom = _range.FromNormalized(_zoomPending ? _savedZoom : _range.ToNormalized(State.Zoom));
            _ready = true;
            _zoomPending = false;
            _followWait = 0f;
        }

        internal void Close()
        {
            if (_zoomHeld)
            {
                StoreZoom();
            }

            _zoomHeld = false;
        }

        internal void UpdateMap(MapCanvas map, Player player, float deltaTime, bool allowInput)
        {
            if (!_ready)
            {
                return;
            }

            if (_zoomPending)
            {
                State.Zoom = _range.FromNormalized(_savedZoom);
                _zoomPending = false;
            }

            var direction = 0f;
            if (allowInput)
            {
                if (Settings.ZoomInMiniMapHotkey.Value.BetterIsPressed())
                {
                    direction++;
                }

                if (Settings.ZoomOutMiniMapHotkey.Value.BetterIsPressed())
                {
                    direction--;
                }
            }

            if (direction != 0f)
            {
                State.Zoom = _range.Clamp(State.Zoom * (1f + direction * _zoomRate * deltaTime));
                _zoomHeld = true;
            }
            else if (_zoomHeld)
            {
                StoreZoom();
                _zoomHeld = false;
            }

            if (player == null)
            {
                return;
            }

            _followWait -= deltaTime;
            if (_followWait <= 0f || direction != 0f)
            {
                var position = MathUtils.ConvertToMapPosition(((IPlayer)player).Position);
                State.Center = position;
                State.Level = map.GetLevelAtPosition(position, State.Level);
                _followWait = 0.033f;
            }
        }

        private void StoreZoom()
        {
            if (!_ready)
            {
                return;
            }

            _savedZoom = _range.ToNormalized(State.Zoom);
            Settings.ZoomMiniMap.Value = _savedZoom;
        }
    }
}
