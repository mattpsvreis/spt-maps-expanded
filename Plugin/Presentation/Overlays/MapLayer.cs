using System;
using System.IO;
using MapsExtended.Core;
using MapsExtended.Presentation;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.UI;

namespace MapsExtended.Presentation.Overlays
{
    public enum LayerStatus
    {
        Hidden,
        Underneath,
        OnTop,
        FullReveal
    }

    public interface ILayerBound
    {
        Vector3 Position { get; set; }
        void HandleNewLayerStatus(LayerStatus status);
    }

    public class MapLayer : MonoBehaviour
    {
        private const float FadePerFloor = 0.5f;
        private const float DefaultFloorAlpha = 0.1f;

        private MapLayerDef _def = new();
        private MaskableGraphic _image;

        public int Level => _def.Level;
        public LayerStatus Status { get; private set; }
        public bool IsOnDefaultLevel { get; set; }
        public RectTransform RectTransform => gameObject.transform as RectTransform;

        public static MapLayer Create(GameObject parent, string name, MapLayerDef def, float degreesRotation)
        {
            var sprite = TextureUtils.GetOrLoadMapSprite(def)
                         ?? throw new InvalidDataException($"Unable to load map image: {def.ImagePath}");

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = parent.layer;
            go.transform.SetParent(parent.transform);
            go.ResetRectTransform();

            var layer = go.AddComponent<MapLayer>();
            var rect = go.GetRectTransform();
            var size = def.ImageBounds.Max - def.ImageBounds.Min;
            rect.sizeDelta = MathUtils.GetRotatedRectangle(size, degreesRotation);
            rect.anchoredPosition = MathUtils.GetMidpoint(def.ImageBounds.Min, def.ImageBounds.Max);
            rect.localRotation = Quaternion.Euler(0, 0, degreesRotation);

            layer._def = def;
            if (Path.GetExtension(def.ImagePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                var image = go.AddComponent<SVGImage>();
                image.preserveAspect = false;
                image.sprite = sprite;
                image.raycastTarget = false;
                layer._image = image;
            }
            else
            {
                var image = go.AddComponent<Image>();
                image.preserveAspect = false;
                image.sprite = sprite;
                image.raycastTarget = false;
                layer._image = image;
            }

            return layer;
        }

        public bool IsCoordinateInLayer(Vector3 coordinate)
        {
            foreach (var bound in _def.GameBounds)
            {
                if (Inside(coordinate, bound))
                {
                    return true;
                }
            }

            return false;
        }

        public float GetMatchingBoundVolume(Vector3 coordinate)
        {
            foreach (var bound in _def.GameBounds)
            {
                if (!Inside(coordinate, bound))
                {
                    continue;
                }

                return (bound.Max.x - bound.Min.x)
                       * (bound.Max.y - bound.Min.y)
                       * (bound.Max.z - bound.Min.z);
            }

            return float.MinValue;
        }

        public void OnTopLevelSelected(int newLevel)
        {
            Status = Level == newLevel
                ? LayerStatus.OnTop
                : Level < newLevel
                    ? LayerStatus.Underneath
                    : LayerStatus.Hidden;

            var active = Status != LayerStatus.Hidden;
            var shade = Mathf.Clamp01(Mathf.Pow(FadePerFloor, newLevel - Level));
            var alpha = 1f;
            if (Status == LayerStatus.Hidden && IsOnDefaultLevel)
            {
                active = true;
                alpha = DefaultFloorAlpha;
            }

            gameObject.SetActive(active);
            _image.color = new Color(shade, shade, shade, alpha);
        }

        private static bool Inside(Vector3 coordinate, BoundingRectangularSolid bound)
        {
            return coordinate.x > bound.Min.x && coordinate.x < bound.Max.x
                   && coordinate.y > bound.Min.y && coordinate.y < bound.Max.y
                   && coordinate.z > bound.Min.z && coordinate.z < bound.Max.z;
        }
    }
}
