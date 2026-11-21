using MapsExtended.Core;
using UnityEngine;

namespace MapsExtended.Presentation.Overlays
{
    public class TransformMapMarker : MapMarker
    {
        private const float CallbackSeconds = 0.5f;
        private static readonly Vector2 Pivot = new(0.5f, 0.5f);

        private Transform _follow;
        private float _wait = CallbackSeconds;
        private bool _warned;

        public MathUtils.RotationAxis RotationAxis { get; set; } = MathUtils.RotationAxis.Y;

        public static TransformMapMarker Create(
            Transform followingTransform,
            GameObject parent,
            string imagePath,
            Color color,
            string name,
            string category,
            Vector2 size,
            float degreesRotation,
            float scale,
            Sprite sprite = null)
        {
            var marker = Create<TransformMapMarker>(
                parent, name, category, imagePath, color,
                MathUtils.ConvertToMapPosition(followingTransform),
                size, Pivot, degreesRotation, scale, true, sprite);
            marker.IsDynamic = true;
            marker._follow = followingTransform;
            return marker;
        }

        public TransformMapMarker()
        {
            ImageAlphaLayerStatus[LayerStatus.Hidden] = 0.25f;
            ImageAlphaLayerStatus[LayerStatus.Underneath] = 0.25f;
            ImageAlphaLayerStatus[LayerStatus.OnTop] = 1f;
            ImageAlphaLayerStatus[LayerStatus.FullReveal] = 1f;
            LabelAlphaLayerStatus[LayerStatus.Hidden] = 0f;
            LabelAlphaLayerStatus[LayerStatus.Underneath] = 0f;
            LabelAlphaLayerStatus[LayerStatus.OnTop] = 0f;
            LabelAlphaLayerStatus[LayerStatus.FullReveal] = 1f;
        }

        private void LateUpdate()
        {
            if (_follow == null)
            {
                return;
            }

            if (!_follow.gameObject.activeInHierarchy)
            {
                if (!_warned)
                {
                    Plugin.Log?.LogWarning("Follow transform for a map marker was disabled without removing the marker");
                    Color = Color.red;
                    _warned = true;
                }

                return;
            }

            _warned = false;
            var position = MathUtils.ConvertToMapPosition(_follow);
            var rotation = MathUtils.ConvertToMapRotation(_follow, RotationAxis);
            if (MathUtils.ApproxEquals(Position.x, position.x)
                && MathUtils.ApproxEquals(Position.y, position.y)
                && MathUtils.ApproxEquals(Position.z, position.z)
                && MathUtils.ApproxEquals(Rotation, rotation))
            {
                return;
            }

            _wait += Time.deltaTime;
            var callback = _wait >= CallbackSeconds;
            if (callback)
            {
                _wait = 0f;
            }

            MoveAndRotate(position, rotation, callback);
        }
    }
}
