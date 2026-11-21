using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Presentation.Overlays;
using EFT;
using UnityEngine;

namespace MapsExtended.Markers
{
    public class PlayerDotSpawner : MonoBehaviour
    {
        private static float _spawnTime = 0.25f;
        private float _timeAccumulator = 0f;

        public MapCanvas MapCanvas { get; set; }

        private void Update()
        {
            if (MapCanvas == null)
            {
                return;
            }

            if (!Input.GetKey(KeyCode.M) || !Input.GetKey(KeyCode.LeftShift))
            {
                return;
            }

            _timeAccumulator += Time.deltaTime;
            if (_timeAccumulator <= _spawnTime && !Input.GetKeyDown(KeyCode.M))
            {
                return;
            }

            var markerDef = new MapMarkerDef
            {
                ImagePath = "Markers/dot.png",
                Position = MathUtils.ConvertToMapPosition(((IPlayer)GameUtils.GetMainPlayer()).Position)
            };

            MapCanvas.AddMapMarker(markerDef);
            _timeAccumulator = 0f;
        }
    }
}
