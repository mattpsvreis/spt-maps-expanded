using MapsExtended.Core;
using EFT;
using UnityEngine;

namespace MapsExtended.Presentation.Overlays
{
    public class PlayerMapMarker : MapMarker
    {
        private const float CallbackSeconds = 0.5f;
        private static readonly Vector2 Pivot = new(0.5f, 0.5f);

        private IPlayer _player;
        private float _wait = CallbackSeconds;

        public static PlayerMapMarker Create(
            IPlayer player,
            GameObject parent,
            string imagePath,
            Color color,
            string category,
            Vector2 size,
            float degreesRotation,
            float scale)
        {
            var marker = Create<PlayerMapMarker>(
                parent, player.Profile.GetCorrectedNickname(), category, imagePath, color,
                MathUtils.ConvertToMapPosition(player.Position),
                size, Pivot, degreesRotation, scale);
            marker.IsDynamic = true;
            marker._player = player;
            return marker;
        }

        public PlayerMapMarker()
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
            if (_player?.Transform?.Original == null)
            {
                return;
            }

            _wait += Time.deltaTime;
            var callback = _wait >= CallbackSeconds;
            if (callback)
            {
                _wait = 0f;
            }

            MoveAndRotate(MathUtils.ConvertToMapPosition(_player.Position), -_player.Rotation.x, callback);
        }
    }
}
