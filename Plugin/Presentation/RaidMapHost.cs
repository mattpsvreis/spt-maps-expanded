using System;
using System.Linq;
using Comfort.Common;
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
        private readonly MapModeController _modes = new();
        private readonly MarkerOrchestrator _markers = new();
        private readonly MapCatalog _catalog = new();

        private MapScrollRect _scrollRect;
        private Mask _scrollMask;
        private MapView _mapView;
        private LevelSelectSlider _levelSelectSlider;
        private MapSelectDropdown _mapSelectDropdown;
        private CursorPositionText _cursorPositionText;
        private PlayerPositionText _playerPositionText;
        private MapPeekComponent _peekComponent;
        private RectTransform _mainParent;

        private RectTransform RectTransform => gameObject.GetRectTransform();
        private RectTransform ParentTransform => transform.parent as RectTransform;

        internal MapView MapView => _mapView;
        internal MapDisplayMode DisplayMode => _modes.Mode;
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

            _mapView = MapView.Create(scrollMaskGo, "MapView");

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
                if (!_modes.ApplyingViewportState && !_modes.ChangingMode && !_modes.IsTransitioning
                    && _modes.Mode == MapDisplayMode.Main)
                {
                    _modes.Main.AcceptDrag(_mapView);
                    ApplyDisplayedViewportState(_modes.Main.State);
                }
            };
            _scrollRect.OnBeginDragCallback = () =>
            {
                if (_modes.Mode == MapDisplayMode.Main && !_modes.IsTransitioning)
                {
                    _modes.Main.AcceptDrag(_mapView);
                }
            };

            var sliderPrefab = Singleton<CommonUI>.Instance.transform
                .Find("Common UI/InventoryScreen/Map Panel/MapBlock/ZoomScroll").gameObject;
            _levelSelectSlider = LevelSelectSlider.Create(sliderPrefab, RectTransform);
            _levelSelectSlider.OnLevelSelectedBySlider += level =>
            {
                if (_modes.Mode == MapDisplayMode.Main && !_modes.IsTransitioning)
                {
                    _modes.Main.ChangeLevel(level);
                }
            };
            _mapView.OnLevelSelected += level => _levelSelectSlider.SelectedLevel = level;
            _levelSelectSlider.gameObject.SetActive(false);

            var selectPrefab = Singleton<CommonUI>.Instance.transform
                .Find("Common UI/InventoryScreen/SkillsAndMasteringPanel/BottomPanel/SkillsPanel/Options/Filter")
                .gameObject;
            _mapSelectDropdown = MapSelectDropdown.Create(selectPrefab, RectTransform);
            _catalog.Bind(_mapSelectDropdown, _mapView, ChangeMap);
            _markers.Bind(_mapView);

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
            if (!_modes.IsShown)
            {
                return;
            }

            if (GameUtils.IsInRaid() && (!EnsureInRaidMapAvailable() || !GameUtils.ShouldShowMapInRaid()))
            {
                RequestMode(MapDisplayMode.Hidden, false);
                return;
            }

            var layout = _modes.GetLayout(_modes.Mode);
            if (layout.Size != _modes.TargetLayout.Size || layout.Position != _modes.TargetLayout.Position)
            {
                RequestMode(_modes.Mode, false, true);
            }

            var player = GameUtils.GetMainPlayer();
            UpdateActiveMap(player, !_modes.IsTransitioning);
            if (_modes.IsTransitioning)
            {
                _modes.TransitionProgress = Mathf.Min(1f, _modes.TransitionProgress + Time.deltaTime / 0.35f);
                var progress = Mathf.SmoothStep(0f, 1f, _modes.TransitionProgress);
                ApplyLayout(new MapModeController.MapLayout
                {
                    Position = Vector2.Lerp(_modes.TransitionFromLayout.Position, _modes.TargetLayout.Position, progress),
                    Size = Vector2.Lerp(_modes.TransitionFromLayout.Size, _modes.TargetLayout.Size, progress)
                });
                ApplyDisplayedViewportState(MapViewportState.Lerp(
                    _modes.TransitionFromState, _modes.DestinationViewportState(), progress));
            }
            else
            {
                ApplyDisplayedViewportState(_modes.DestinationViewportState());
            }

            _scrollRect.enabled = _modes.Mode == MapDisplayMode.Main && !_modes.IsTransitioning;
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
                _peekComponent.WasMiniMapActive = _modes.Mode == MapDisplayMode.Mini
                    || (_modes.Mode == MapDisplayMode.Peek && _peekComponent.ShouldShowMiniMap);
            }

            IsShowingMapScreen = true;
            _mainParent.Find("MapBlock").gameObject.SetActive(false);
            _mainParent.Find("EmptyBlock").gameObject.SetActive(false);
            _mainParent.gameObject.SetActive(true);
            if (!_modes.Main.HasViewportStateFor(_mapView.CurrentMapDef))
            {
                Canvas.ForceUpdateCanvases();
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
                _markers.NotifyRaidEnd();
                _mapView.UnloadMap();
                _modes.Invalidate(null);
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
            if (_modes.ChangingMode || (!resize && _modes.Mode == mode))
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

            _modes.ChangingMode = true;
            try
            {
                var previousMode = _modes.Mode;
                var wasVisible = _modes.IsShown;
                var fromState = _mapView.CaptureViewportState();
                var oldScale = RectTransform.lossyScale.x;

                if (wasVisible)
                {
                    _scrollMask.GetRectTransform().GetWorldCorners(_maskCorners);
                }

                if (previousMode != mode)
                {
                    _modes.ClosePrevious(previousMode);
                }

                if (!_modes.IsShown)
                {
                    _catalog.LoadAll();
                }

                if (!EnsureInRaidMapAvailable() || !GameUtils.ShouldShowMapInRaid())
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

                _modes.Mode = mode;
                _modes.IsShown = true;
                gameObject.SetActive(true);
                PositionControls();
                _modes.TargetLayout = _modes.GetLayout(mode);

                var definition = _mapView.CurrentMapDef;
                var player = GameUtils.GetMainPlayer();
                if (mode == MapDisplayMode.Mini)
                {
                    _modes.Mini.Open(definition, _modes.TargetLayout.Size);
                }
                else
                {
                    if (resize && previousMode == mode && !_modes.MapChanged)
                    {
                        _modes.Main.Resize(definition, _modes.TargetLayout.Size);
                    }
                    else
                    {
                        _modes.Main.Open(definition, _modes.TargetLayout.Size, player, GameUtils.IsInRaid());
                    }

                    if (GameUtils.IsInRaid() && !(resize && previousMode == mode))
                    {
                        _modes.Main.SelectPlayerLevel(_mapView, player);
                    }
                }

                UpdateActiveMap(player, false);
                _markers.SetHostShown(true);
                _markers.SetVisibility(true, GameUtils.IsInRaid());

                _cursorPositionText.gameObject.SetActive(mode == MapDisplayMode.Main);
                _playerPositionText.gameObject.SetActive(mode == MapDisplayMode.Main && GameUtils.IsInRaid());
                _levelSelectSlider.gameObject.SetActive(
                    mode == MapDisplayMode.Main && MapModeController.HasMultipleLevels(definition));
                _mapSelectDropdown.gameObject.SetActive(
                    mode == MapDisplayMode.Main && !GameUtils.IsInRaid() && _catalog.GetMapDefs().Count() > 1);

                _modes.TransitionProgress = 1f;
                if (wasVisible && !_modes.MapChanged && animate && _modes.TransitionAnimations)
                {
                    var lower = RectTransform.InverseTransformPoint(_maskCorners[0]);
                    var upper = RectTransform.InverseTransformPoint(_maskCorners[2]);
                    _modes.TransitionFromLayout = new MapModeController.MapLayout
                    {
                        Position = (lower + upper) * 0.5f,
                        Size = new Vector2(Mathf.Abs(upper.x - lower.x), Mathf.Abs(upper.y - lower.y))
                    };
                    fromState.Zoom *= oldScale / Mathf.Max(RectTransform.lossyScale.x, 0.0001f);
                    _modes.TransitionFromState = fromState;
                    _modes.TransitionProgress = 0f;
                    ApplyLayout(_modes.TransitionFromLayout);
                    ApplyDisplayedViewportState(fromState);
                }
                else
                {
                    ApplyLayout(_modes.TargetLayout);
                    ApplyDisplayedViewportState(_modes.DestinationViewportState());
                }

                _modes.MapChanged = false;
                _scrollRect.enabled = mode == MapDisplayMode.Main && !_modes.IsTransitioning;
            }
            finally
            {
                _modes.ChangingMode = false;
            }
        }

        private void HideDisplay()
        {
            _catalog.TryClose();
            _modes.CloseCurrent();
            _markers.SetHostShown(false);
            _markers.SetVisibility(false, false);

            _modes.TransitionProgress = 1f;
            _modes.Mode = MapDisplayMode.Hidden;
            _modes.IsShown = false;
            _scrollRect?.StopMovement();
            gameObject.SetActive(false);

            if (_mainParent != null)
            {
                transform.SetParent(_mainParent, false);
            }
        }

        private void UpdateActiveMap(Player player, bool allowInput)
        {
            _modes.UpdateActiveMap(_mapView, player, allowInput, _catalog.IsDropdownOpen());
        }

        private void ApplyDisplayedViewportState(MapViewportState state)
        {
            _modes.ApplyingViewportState = true;
            try
            {
                _mapView.ApplyViewportState(state);
            }
            finally
            {
                _modes.ApplyingViewportState = false;
            }
        }

        private void ApplyLayout(MapModeController.MapLayout layout)
        {
            _modes.ApplyingViewportState = true;
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
                _modes.ApplyingViewportState = false;
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
            _markers.NotifyRaidEnd();
            if (_peekComponent != null)
            {
                Destroy(_peekComponent.gameObject);
            }

            _peekComponent = null;
            _mapView.UnloadMap();
            _modes.Invalidate(null);
        }

        internal void ReadConfig()
        {
            _modes.ReadConfig();
            if (_peekComponent != null)
            {
                _peekComponent.PeekShortcut = Settings.PeekShortcut.Value;
                _peekComponent.HoldForPeek = Settings.HoldForPeek.Value;
                _peekComponent.HideMinimapShortcut = Settings.MiniMapShowOrHide.Value;
            }

            if (_modes.IsShown && !_modes.ChangingMode)
            {
                var layout = _modes.GetLayout(_modes.Mode);
                if (layout.Size != _modes.TargetLayout.Size || layout.Position != _modes.TargetLayout.Position)
                {
                    RequestMode(_modes.Mode, false, true);
                }
            }

            _markers.ApplyConfig(ServerConfig);
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
                _markers.NotifyRaidEnd();
                _mapView.UnloadMap();
                _modes.Invalidate(null);
                return;
            }

            _modes.Invalidate(mapDef);
            _modes.MapChanged = true;
            _catalog.NotifyLoaded(mapDef);
            _markers.NotifyMapChanged(mapDef);
            if (_modes.IsShown && !_modes.ChangingMode)
            {
                RequestMode(_modes.Mode, false, true);
            }
        }
    }
}
