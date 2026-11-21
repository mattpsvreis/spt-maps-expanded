using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using MapsExtended.Core;
using MapsExtended.Presentation;
using EFT;
using UnityEngine;

namespace MapsExtended.Presentation.Overlays
{
    public class MapCanvas : MonoBehaviour
    {
        private static readonly Vector2 PinSize = new(30, 30);

        public event Action<int> OnLevelSelected;

        public RectTransform RectTransform => transform as RectTransform;
        public MapDef CurrentMapDef { get; private set; }

        private float _rotation;
        private int _floor;
        private float _zoom = 1f;
        private GameObject _floorRoot;
        private GameObject _pinRoot;
        private GameObject _labelRoot;
        private readonly List<MapLayer> _floors = [];
        private readonly List<MapMarker> _pins = [];
        private readonly List<MapLabel> _labels = [];

        public static MapCanvas Create(GameObject parent, string name)
        {
            var go = UIUtils.CreateUIGameObject(parent, name);
            go.AddComponent<Canvas>();
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            return go.AddComponent<MapCanvas>();
        }

        private void Awake()
        {
            _floorRoot = UIUtils.CreateUIGameObject(gameObject, "Floors");
            _pinRoot = UIUtils.CreateUIGameObject(gameObject, "Pins");
            _labelRoot = UIUtils.CreateUIGameObject(gameObject, "Labels");
            _floorRoot.transform.SetAsFirstSibling();
            _pinRoot.transform.SetAsLastSibling();
        }

        private void OnDestroy()
        {
            TextureUtils.ClearMapCache();
        }

        public void LoadMap(MapDef mapDef)
        {
            if (mapDef == null || CurrentMapDef == mapDef)
            {
                return;
            }

            if (CurrentMapDef != null)
            {
                UnloadMap();
            }

            CurrentMapDef = mapDef;
            _rotation = mapDef.CoordinateRotation;
            var size = mapDef.Bounds.Max - mapDef.Bounds.Min;
            RectTransform.sizeDelta = MathUtils.GetRotatedRectangle(size, _rotation);
            RectTransform.localRotation = Quaternion.Euler(0, 0, _rotation);
            _zoom = 1f;
            RectTransform.localScale = Vector3.one;
            RectTransform.anchoredPosition = Vector2.zero;

            foreach (var pair in mapDef.Layers.OrderBy(pair => pair.Value.Level))
            {
                var layer = MapLayer.Create(_floorRoot, pair.Key, pair.Value, -_rotation);
                layer.IsOnDefaultLevel = pair.Value.Level == mapDef.DefaultLevel;
                _floors.Add(layer);
            }

            SelectTopLevel(mapDef.DefaultLevel);
            foreach (var marker in mapDef.StaticMarkers)
            {
                AddMapMarker(marker);
            }

            foreach (var label in mapDef.Labels)
            {
                AddMapLabel(label);
            }
        }

        public void UnloadMap()
        {
            if (CurrentMapDef == null)
            {
                return;
            }

            foreach (var pin in _pins.ToList())
            {
                RemoveMapMarker(pin);
            }

            foreach (var label in _labels.ToList())
            {
                RemoveMapLabel(label);
            }

            foreach (var floor in _floors)
            {
                Destroy(floor.gameObject);
            }

            _floors.Clear();
            TextureUtils.ClearMapCache();
            _zoom = 1f;
            CurrentMapDef = null;
        }

        public void SelectTopLevel(int level)
        {
            foreach (var floor in _floors)
            {
                floor.OnTopLevelSelected(level);
            }

            _floor = level;
            RefreshPinFloors();
            OnLevelSelected?.Invoke(level);
        }

        public void SelectLevelByCoords(Vector3 coords)
        {
            var floor = FloorAt(coords);
            if (floor != null)
            {
                SelectTopLevel(floor.Level);
            }
        }

        public MapMarker AddMapMarker(MapMarkerDef markerDef)
        {
            var marker = MapMarker.Create(_pinRoot, markerDef, PinSize, -_rotation, 1f / _zoom);
            Track(marker);
            return marker;
        }

        public void AddMapMarker(MapMarker marker)
        {
            Track(marker);
        }

        public TransformMapMarker AddTransformMarker(Transform followingTransform, string markerName, string category, Color color,
            string imagePath, Vector2 size, Sprite sprite = null)
        {
            var marker = TransformMapMarker.Create(followingTransform, _pinRoot, imagePath, color, markerName, category, size, -_rotation, 1f / _zoom, sprite);
            Track(marker);
            return marker;
        }

        public TransformMapMarker AddTransformMarker(Transform followingTransform, string markerName, string category, Color color, Sprite sprite)
        {
            return AddTransformMarker(followingTransform, markerName, category, color, null, PinSize, sprite);
        }

        public PlayerMapMarker AddPlayerMarker(IPlayer player, string category, Color color, string imagePath)
        {
            var marker = PlayerMapMarker.Create(player, _pinRoot, imagePath, color, category, PinSize, -_rotation, 1f / _zoom);
            Track(marker);
            return marker;
        }

        public IEnumerable<MapMarker> GetMapMarkersByCategory(string category)
        {
            return _pins.Where(pin => pin.Category == category);
        }

        public void ChangeMarkerCategoryStatus(string category, bool status)
        {
            foreach (var pin in _pins)
            {
                if (pin.Category == category)
                {
                    pin.gameObject.SetActive(status);
                }
            }
        }

        public void ChangeMarkerPartialCategoryStatus(string partial, bool status)
        {
            foreach (var pin in _pins)
            {
                if (pin.Category.Contains(partial))
                {
                    pin.gameObject.SetActive(status);
                }
            }
        }

        public void RemoveMapMarker(MapMarker marker)
        {
            if (!_pins.Remove(marker))
            {
                return;
            }

            marker.OnPositionChanged -= PlaceOnFloor;
            marker.ContainingMapView = null;
            DOTween.Kill(marker.transform);
            marker.gameObject.SetActive(false);
            Destroy(marker.gameObject);
        }

        public void AddMapLabel(MapLabelDef labelDef)
        {
            var label = MapLabel.Create(_labelRoot, labelDef, -_rotation, 1f / _zoom);
            PlaceOnFloor(label);
            _labels.Add(label);
        }

        public void RemoveMapLabel(MapLabel label)
        {
            if (!_labels.Remove(label))
            {
                return;
            }

            DOTween.Kill(label.transform);
            label.gameObject.SetActive(false);
            Destroy(label.gameObject);
        }

        internal MapViewportState CaptureViewportState()
        {
            var zoom = Mathf.Max(RectTransform.localScale.x, 0.0001f);
            return new MapViewportState
            {
                Center = MathUtils.GetRotatedVector2(-RectTransform.anchoredPosition / zoom, -_rotation),
                Zoom = zoom,
                Level = _floor
            };
        }

        internal void ApplyViewportState(MapViewportState state)
        {
            if (CurrentMapDef == null)
            {
                return;
            }

            if (!Mathf.Approximately(_zoom, state.Zoom))
            {
                _zoom = state.Zoom;
                RectTransform.localScale = Vector3.one * state.Zoom;
                var inverse = Vector3.one / state.Zoom;
                foreach (var pin in _pins)
                {
                    pin.GetRectTransform().localScale = inverse;
                }

                foreach (var label in _labels)
                {
                    label.GetRectTransform().localScale = inverse;
                }
            }

            RectTransform.anchoredPosition = -MathUtils.GetRotatedVector2(state.Center, _rotation) * state.Zoom;
            if (_floor != state.Level)
            {
                SelectTopLevel(state.Level);
            }
        }

        internal int GetLevelAtPosition(Vector3 position, int fallback)
        {
            return FloorAt(position)?.Level ?? fallback;
        }

        private void Track(MapMarker marker)
        {
            if (marker == null || _pins.Contains(marker))
            {
                return;
            }

            marker.OnPositionChanged += PlaceOnFloor;
            marker.ContainingMapView = this;
            PlaceOnFloor(marker);
            _pins.Add(marker);
        }

        private void PlaceOnFloor(ILayerBound bound)
        {
            var floor = FloorAt(bound.Position);
            if (floor != null)
            {
                bound.HandleNewLayerStatus(floor.Status);
            }
        }

        private void RefreshPinFloors()
        {
            foreach (var pin in _pins)
            {
                PlaceOnFloor(pin);
            }

            foreach (var label in _labels)
            {
                PlaceOnFloor(label);
            }
        }

        private MapLayer FloorAt(Vector3 coordinate)
        {
            return _floors.Where(floor => floor.IsCoordinateInLayer(coordinate))
                .OrderBy(floor => floor.GetMatchingBoundVolume(coordinate))
                .FirstOrDefault();
        }
    }

    internal struct MapViewportState
    {
        internal Vector2 Center;
        internal float Zoom;
        internal int Level;

        internal static MapViewportState Lerp(MapViewportState from, MapViewportState to, float progress)
        {
            return new MapViewportState
            {
                Center = Vector2.Lerp(from.Center, to.Center, progress),
                Zoom = Mathf.Lerp(from.Zoom, to.Zoom, progress),
                Level = progress < 1f ? from.Level : to.Level
            };
        }
    }

    internal enum MapDisplayMode
    {
        Hidden,
        Main,
        Peek,
        Mini
    }
}
