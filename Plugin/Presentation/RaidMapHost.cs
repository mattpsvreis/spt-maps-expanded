using System;
using System.Linq;
using Comfort.Common;
using MapsExtended.App;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Hooks;
using MapsExtended.Integration;
using MapsExtended.Presentation.Controls;
using MapsExtended.Presentation.Overlays;
using EFT;
using EFT.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MapsExtended.Presentation
{
    public class RaidMapHost : MonoBehaviour
    {
        private static readonly Vector2 LevelSliderPosition = new(15f, 750f);
        private static readonly Vector2 MapSelectDropdownPosition = new(-780f, -50f);
        private static readonly Vector2 MapSelectDropdownSize = new(540f, 31f);
        private static readonly Vector2 TextAnchor = new(0f, 1f);
        private static readonly Vector2 CursorPositionTextOffset = new(15f, -52f);
        private static readonly Vector2 PlayerPositionTextOffset = new(15f, -68f);
        private const float PositionTextFontSize = 15f;

        private readonly Vector3[] _maskCorners = new Vector3[4];
        private readonly MapSession _session = new();
        private readonly PinBoard _pins = new();
        private readonly MapCatalog _catalog = new();

        private MapScrollRect _scrollRect;
        private Mask _scrollMask;
        private MapCanvas _mapView;
        private LevelSelectSlider _levelSelectSlider;
        private MapSelectDropdown _mapSelectDropdown;
        private CursorPositionText _cursorPositionText;
        private PlayerPositionText _playerPositionText;
        private MapPeekComponent _peekComponent;
        private RectTransform _mainParent;

        private RectTransform RectTransform => gameObject.GetRectTransform();
        private RectTransform ParentTransform => transform.parent as RectTransform;

        internal MapCanvas MapCanvas => _mapView;
        internal MapDisplayMode DisplayMode => _session.Surface;
        public bool IsShowingMapScreen { get; private set; }
        public static ServerMapPolicy ServerConfig;

        internal static RaidMapHost Create(GameObject parent)
        {
            var go = UIUtils.CreateUIGameObject(parent, "MapsExpandedHost");
            return go.AddComponent<RaidMapHost>();
        }

        private void Awake()
        {
            var scrollRectGo = UIUtils.CreateUIGameObject(gameObject, "Scroll");
            var scrollMaskGo = UIUtils.CreateUIGameObject(scrollRectGo, "ScrollMask");

            _mapView = MapCanvas.Create(scrollMaskGo, "MapCanvas");

            var scrollMaskImage = scrollMaskGo.AddComponent<Image>();
            scrollMaskImage.color = new Color(0f, 0f, 0f, 0.5f);
            _scrollMask = scrollMaskGo.AddComponent<Mask>();

            _scrollRect = scrollRectGo.AddComponent<MapScrollRect>();
            _scrollRect.scrollSensitivity = 0;
            _scrollRect.movementType = ScrollRect.MovementType.Unrestricted;
            _scrollRect.inertia = false;
            _scrollRect.viewport = _scrollMask.GetRectTransform();
            _scrollRect.content = _mapView.RectTransform;
            _scrollRect.OnDragCallback = () =>
            {
                if (!_session.WritingCamera && !_session.Switching && !_session.Gliding
                    && _session.Surface == MapDisplayMode.Main)
                {
                    _session.Inventory.AcceptDrag(_mapView);
                    ApplyDisplayedViewportState(_session.Inventory.State);
                }
            };
            _scrollRect.OnBeginDragCallback = () =>
            {
                if (_session.Surface == MapDisplayMode.Main && !_session.Gliding)
                {
                    _session.Inventory.AcceptDrag(_mapView);
                }
            };

            var sliderPrefab = Singleton<CommonUI>.Instance.transform
                .Find("Common UI/InventoryScreen/Map Panel/MapBlock/ZoomScroll").gameObject;
            _levelSelectSlider = LevelSelectSlider.Create(sliderPrefab, RectTransform);
            _levelSelectSlider.OnLevelSelectedBySlider += level =>
            {
                if (_session.Surface == MapDisplayMode.Main && !_session.Gliding)
                {
                    _session.Inventory.ChangeLevel(level);
                }
            };
            _mapView.OnLevelSelected += level => _levelSelectSlider.SelectedLevel = level;
            _levelSelectSlider.gameObject.SetActive(false);

            var selectPrefab = Singleton<CommonUI>.Instance.transform
                .Find("Common UI/InventoryScreen/SkillsAndMasteringPanel/BottomPanel/SkillsPanel/Options/Filter")
                .gameObject;
            _mapSelectDropdown = MapSelectDropdown.Create(selectPrefab, RectTransform);
            _catalog.Bind(_mapSelectDropdown, _mapView, ChangeMap);
            _pins.Bind(_mapView);

            _cursorPositionText = CursorPositionText.Create(gameObject, _mapView.RectTransform, PositionTextFontSize);
            _cursorPositionText.RectTransform.anchorMin = TextAnchor;
            _cursorPositionText.RectTransform.anchorMax = TextAnchor;

            _playerPositionText = PlayerPositionText.Create(gameObject, PositionTextFontSize);
            _playerPositionText.RectTransform.anchorMin = TextAnchor;
            _playerPositionText.RectTransform.anchorMax = TextAnchor;
            _playerPositionText.gameObject.SetActive(false);

            ReadConfig();

            GameWorldOnDestroyPatch.OnRaidEnd += OnRaidEnd;

            _catalog.LoadAll();
            _catalog.PrecacheBundledLayers();
        }

        private void OnDestroy()
        {
            if (_scrollRect != null)
            {
                _scrollRect.OnBeginDragCallback = null;
                _scrollRect.OnDragCallback = null;
            }

            GameWorldOnDestroyPatch.OnRaidEnd -= OnRaidEnd;
        }

        private void Update()
        {
            if (!_session.Visible)
            {
                return;
            }

            if (GameUtils.IsInRaid() && !EnsureInRaidMapAvailable())
            {
                RequestMode(MapDisplayMode.Hidden, false);
                return;
            }

            var layout = _session.FrameFor(_session.Surface);
            if (layout.Size != _session.TargetFrame.Size || layout.Position != _session.TargetFrame.Position)
            {
                RequestMode(_session.Surface, false, true);
            }

            var player = GameUtils.GetMainPlayer();
            UpdateActiveMap(player, !_session.Gliding);
            if (_session.Gliding)
            {
                _session.GlideProgress = Mathf.Min(1f, _session.GlideProgress + Time.deltaTime / 0.35f);
                var progress = Mathf.SmoothStep(0f, 1f, _session.GlideProgress);
                ApplyLayout(new MapSession.Frame
                {
                    Position = Vector2.Lerp(_session.GlideFromFrame.Position, _session.TargetFrame.Position, progress),
                    Size = Vector2.Lerp(_session.GlideFromFrame.Size, _session.TargetFrame.Size, progress)
                });
                ApplyDisplayedViewportState(MapViewportState.Lerp(
                    _session.GlideFromCamera, _session.CameraState(), progress));
            }
            else
            {
                ApplyDisplayedViewportState(_session.CameraState());
            }

            _scrollRect.enabled = _session.Surface == MapDisplayMode.Main && !_session.Gliding;
            if (Settings.DumpInfoHotkey.Value.BetterIsDown())
            {
                DumpUtils.DumpExtracts();
                DumpUtils.DumpSwitches();
                DumpUtils.DumpLocks();
            }
        }

        internal void OnMapScreenShow()
        {
            if (_mainParent == null)
            {
                _mainParent = ParentTransform;
            }

            if (_peekComponent != null)
            {
                _peekComponent.WasMiniMapActive = _session.Surface == MapDisplayMode.Mini
                    || (_session.Surface == MapDisplayMode.Peek && _peekComponent.ShouldShowMiniMap);
            }

            IsShowingMapScreen = true;
            var mapBlock = _mainParent.Find("MapBlock");
            var emptyBlock = _mainParent.Find("EmptyBlock");
            if (mapBlock != null)
            {
                mapBlock.gameObject.SetActive(false);
            }

            if (emptyBlock != null)
            {
                emptyBlock.gameObject.SetActive(false);
            }

            _mainParent.gameObject.SetActive(true);
            if (!_session.Inventory.HasViewportStateFor(_mapView.CurrentMapDef))
            {
                // A broken TextMeshPro elsewhere on the canvas throws from here.
                // That used to abort the replacement and let the vanilla map show.
                try
                {
                    Canvas.ForceUpdateCanvases();
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Map layout refresh skipped: {e.Message}");
                }
            }

            RequestMode(MapDisplayMode.Main, true);
        }

        internal void OnMapScreenClose()
        {
            IsShowingMapScreen = false;
            var restoreMiniMap = _peekComponent != null && _peekComponent.WasMiniMapActive && _peekComponent.ShouldShowMiniMap;
            RequestMode(restoreMiniMap ? MapDisplayMode.Mini : MapDisplayMode.Hidden, true);
        }

        internal bool EnsureInRaidMapAvailable()
        {
            if (!GameUtils.IsInRaid() || _catalog.HasMapForLocation(GameUtils.GetCurrentMapInternalName()))
            {
                return true;
            }

            HideDisplay();
            if (_mapView.CurrentMapDef != null)
            {
                _pins.NotifyRaidEnd();
                _mapView.UnloadMap();
                _session.Forget(null);
            }

            return false;
        }

        /// <summary>Re-apply location filter when MapVariants resolves a new pack.</summary>
        internal void RefreshLocationFilter(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId))
            {
                return;
            }

            _catalog.ApplyRaidLocation(locationId);
        }

        internal void RequestMode(MapDisplayMode mode, bool animate, bool resize = false)
        {
            if (_session.Switching || (!resize && _session.Surface == mode))
            {
                return;
            }

            if (mode == MapDisplayMode.Hidden)
            {
                HideDisplay();
                return;
            }

            if (_mainParent == null)
            {
                _mainParent = ParentTransform;
            }

            var targetParent = mode == MapDisplayMode.Main ? _mainParent : _peekComponent?.RectTransform;
            if (targetParent == null)
            {
                return;
            }

            _session.Switching = true;
            try
            {
                var previousMode = _session.Surface;
                var wasVisible = _session.Visible;
                var fromState = _mapView.CaptureViewportState();
                var oldScale = RectTransform.lossyScale.x;

                if (wasVisible)
                {
                    _scrollMask.GetRectTransform().GetWorldCorners(_maskCorners);
                }

                if (previousMode != mode)
                {
                    _session.Release(previousMode);
                }

                if (!_session.Visible)
                {
                    _catalog.LoadAll();
                }

                if (!EnsureInRaidMapAvailable())
                {
                    HideDisplay();
                    return;
                }

                if (GameUtils.IsInRaid())
                {
                    _catalog.ApplyRaidLocation(GameUtils.GetCurrentMapInternalName());
                }
                else
                {
                    _catalog.ApplyOutOfRaid();
                }

                if (_mapView.CurrentMapDef == null)
                {
                    HideDisplay();
                    return;
                }

                transform.SetParent(targetParent, false);
                transform.SetAsLastSibling();
                RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                RectTransform.anchorMax = RectTransform.anchorMin;
                RectTransform.pivot = RectTransform.anchorMin;
                RectTransform.sizeDelta = Singleton<CommonUI>.Instance.InventoryScreen.GetRectTransform().rect.size;
                RectTransform.anchoredPosition = Vector2.zero;
                RectTransform.localScale = Vector3.one;
                _scrollRect.GetRectTransform().anchorMin = Vector2.zero;
                _scrollRect.GetRectTransform().anchorMax = Vector2.one;
                _scrollRect.GetRectTransform().sizeDelta = Vector2.zero;
                _scrollRect.GetRectTransform().anchoredPosition = Vector2.zero;
                _scrollRect.StopMovement();
                _scrollRect.enabled = false;

                _session.Surface = mode;
                _session.Visible = true;
                gameObject.SetActive(true);
                PositionControls();
                _session.TargetFrame = _session.FrameFor(mode);

                var definition = _mapView.CurrentMapDef;
                var player = GameUtils.GetMainPlayer();
                if (mode == MapDisplayMode.Mini)
                {
                    _session.Corner.Open(definition, _session.TargetFrame.Size);
                }
                else
                {
                    if (resize && previousMode == mode && !_session.MapSwapped)
                    {
                        _session.Inventory.Resize(definition, _session.TargetFrame.Size);
                    }
                    else
                    {
                        _session.Inventory.Open(definition, _session.TargetFrame.Size, player, GameUtils.IsInRaid());
                    }

                    if (GameUtils.IsInRaid() && !(resize && previousMode == mode))
                    {
                        _session.Inventory.SelectPlayerLevel(_mapView, player);
                    }
                }

                UpdateActiveMap(player, false);
                _pins.SetHostShown(true);
                _pins.SetVisibility(true, GameUtils.IsInRaid());

                _cursorPositionText.gameObject.SetActive(mode == MapDisplayMode.Main);
                _playerPositionText.gameObject.SetActive(mode == MapDisplayMode.Main && GameUtils.IsInRaid());
                _levelSelectSlider.gameObject.SetActive(
                    mode == MapDisplayMode.Main && MapSession.SpansFloors(definition));
                _mapSelectDropdown.gameObject.SetActive(
                    mode == MapDisplayMode.Main && !GameUtils.IsInRaid() && _catalog.GetMapDefs().Count() > 1);

                _session.GlideProgress = 1f;
                if (wasVisible && !_session.MapSwapped && animate && _session.Glide)
                {
                    var lower = RectTransform.InverseTransformPoint(_maskCorners[0]);
                    var upper = RectTransform.InverseTransformPoint(_maskCorners[2]);
                    _session.GlideFromFrame = new MapSession.Frame
                    {
                        Position = (lower + upper) * 0.5f,
                        Size = new Vector2(Mathf.Abs(upper.x - lower.x), Mathf.Abs(upper.y - lower.y))
                    };
                    fromState.Zoom *= oldScale / Mathf.Max(RectTransform.lossyScale.x, 0.0001f);
                    _session.GlideFromCamera = fromState;
                    _session.GlideProgress = 0f;
                    ApplyLayout(_session.GlideFromFrame);
                    ApplyDisplayedViewportState(fromState);
                }
                else
                {
                    ApplyLayout(_session.TargetFrame);
                    ApplyDisplayedViewportState(_session.CameraState());
                }

                _session.MapSwapped = false;
                _scrollRect.enabled = mode == MapDisplayMode.Main && !_session.Gliding;
            }
            finally
            {
                _session.Switching = false;
            }
        }

        private void HideDisplay()
        {
            _catalog.TryClose();
            _session.ReleaseCurrent();
            _pins.SetHostShown(false);
            _pins.SetVisibility(false, false);

            _session.GlideProgress = 1f;
            _session.Surface = MapDisplayMode.Hidden;
            _session.Visible = false;
            _scrollRect?.StopMovement();
            gameObject.SetActive(false);

            if (_mainParent != null)
            {
                transform.SetParent(_mainParent, false);
            }
        }

        private void UpdateActiveMap(Player player, bool allowInput)
        {
            _session.Advance(_mapView, player, allowInput, _catalog.IsDropdownOpen());
        }

        private void ApplyDisplayedViewportState(MapViewportState state)
        {
            _session.WritingCamera = true;
            try
            {
                _mapView.ApplyViewportState(state);
            }
            finally
            {
                _session.WritingCamera = false;
            }
        }

        private void ApplyLayout(MapSession.Frame layout)
        {
            _session.WritingCamera = true;
            try
            {
                var mask = _scrollMask.GetRectTransform();
                mask.anchorMin = new Vector2(0.5f, 0.5f);
                mask.anchorMax = mask.anchorMin;
                mask.pivot = mask.anchorMin;
                mask.sizeDelta = layout.Size;
                mask.anchoredPosition = layout.Position;
            }
            finally
            {
                _session.WritingCamera = false;
            }
        }

        private void PositionControls()
        {
            _levelSelectSlider.RectTransform.anchoredPosition = LevelSliderPosition;
            _mapSelectDropdown.RectTransform.sizeDelta = MapSelectDropdownSize;
            _mapSelectDropdown.RectTransform.anchoredPosition = MapSelectDropdownPosition;
            _cursorPositionText.RectTransform.anchorMin = TextAnchor;
            _cursorPositionText.RectTransform.anchorMax = TextAnchor;
            _cursorPositionText.RectTransform.anchoredPosition = CursorPositionTextOffset;
            _playerPositionText.RectTransform.anchorMin = TextAnchor;
            _playerPositionText.RectTransform.anchorMax = TextAnchor;
            _playerPositionText.RectTransform.anchoredPosition = PlayerPositionTextOffset;
        }

        private void OnRaidEnd()
        {
            HideDisplay();
            _pins.NotifyRaidEnd();
            if (_peekComponent != null)
            {
                Destroy(_peekComponent.gameObject);
            }

            _peekComponent = null;
            _mapView.UnloadMap();
            _session.Forget(null);
        }

        internal void ReadConfig()
        {
            _session.ReadConfig();
            if (_peekComponent != null)
            {
                _peekComponent.PeekShortcut = Settings.PeekShortcut.Value;
                _peekComponent.HoldForPeek = Settings.HoldForPeek.Value;
                _peekComponent.HideMinimapShortcut = Settings.MiniMapShowOrHide.Value;
            }

            if (_session.Visible && !_session.Switching)
            {
                var layout = _session.FrameFor(_session.Surface);
                if (layout.Size != _session.TargetFrame.Size || layout.Position != _session.TargetFrame.Position)
                {
                    RequestMode(_session.Surface, false, true);
                }
            }

            _pins.ApplyConfig(ServerConfig);
        }

        internal void TryAddPeekComponent(EftBattleUIScreen battleUI)
        {
            if (_peekComponent != null)
            {
                return;
            }

            if (_mainParent == null)
            {
                _mainParent = ParentTransform;
            }

            _peekComponent = MapPeekComponent.Create(battleUI.gameObject);
            _peekComponent.MapScreen = this;
            ReadConfig();
        }

        private void ChangeMap(MapDef mapDef)
        {
            if (mapDef == null || _mapView.CurrentMapDef == mapDef)
            {
                return;
            }

            Plugin.Log.LogInfo($"Maps Expanded: Loading map {mapDef.DisplayName}");
            try
            {
                _levelSelectSlider.OnLoadMap(mapDef, mapDef.DefaultLevel);
                _mapView.LoadMap(mapDef);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Failed to load map {mapDef.DisplayName}: {e}");
                _catalog.RemoveUnavailable(mapDef);
                HideDisplay();
                _pins.NotifyRaidEnd();
                _mapView.UnloadMap();
                _session.Forget(null);
                return;
            }

            _session.Forget(mapDef);
            _session.MapSwapped = true;
            _catalog.NotifyLoaded(mapDef);
            _pins.NotifyMapChanged(mapDef);
            if (_session.Visible && !_session.Switching)
            {
                RequestMode(_session.Surface, false, true);
            }
        }
    }
}
