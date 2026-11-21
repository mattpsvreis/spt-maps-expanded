using System.Linq;
using Comfort.Common;
using MapsExtended.Config;
using MapsExtended.Integration;
using MapsExtended.Core;
using MapsExtended.Hooks;
using MapsExtended.Presentation.Overlays;
using EFT;
using EFT.Interactive;
using EFT.UI.DragAndDrop;
using UnityEngine;

namespace MapsExtended.Markers
{
    public class BackpackMarkerProvider : PinSource
    {
        private const string Icon = "Markers/backpack.png";
        private static readonly Vector2 Size = new(30f, 30f);
        private readonly PinBag<int> _pins = new();

        protected override void ShowInRaid(MapCanvas map)
        {
            DropMissing();
            PlaceThrown();
            LootItemInitPatch.OnRegisterLoot += OnLoot;
            GameWorldDestroyLootPatch.OnDestroyLoot += OnGone;
        }

        protected override void HideInRaid(MapCanvas map) => Unhook();

        protected override void MapChanged(MapCanvas map, MapDef mapDef)
        {
            var world = Singleton<GameWorld>.Instance;
            foreach (var netId in _pins.Keys.ToList())
            {
                _pins.Drop(netId);
                if (world.LootItems.ContainsKey(netId))
                {
                    Place(world.LootItems.GetByKey(netId));
                }
            }
        }

        protected override void EndRaid(MapCanvas map)
        {
            Unhook();
            _pins.Clear();
        }

        protected override void ColorsChanged(MapCanvas map) => _pins.Paint(Settings.BackpackColor.Value);

        protected override void Disable(MapCanvas map) => EndRaid(map);

        private void PlaceThrown()
        {
            var world = Singleton<GameWorld>.Instance;
            foreach (var pair in PlayerInventoryThrowItemPatch.ThrownItems)
            {
                if (_pins.Has(pair.Key) || ItemViewFactory.GetItemType(pair.Value.GetType()) != EItemType.Backpack)
                {
                    continue;
                }

                if (world.LootItems.ContainsKey(pair.Key))
                {
                    Place(world.LootItems.GetByKey(pair.Key));
                }
            }
        }

        private void OnLoot(LootItem loot)
        {
            if (loot?.Item == null)
            {
                return;
            }

            var netId = loot.GetNetId();
            if (_pins.Has(netId)
                || ItemViewFactory.GetItemType(loot.Item.GetType()) != EItemType.Backpack
                || !PlayerInventoryThrowItemPatch.ThrownItems.ContainsKey(netId))
            {
                return;
            }

            Place(loot);
        }

        private void OnGone(LootItem loot)
        {
            if (loot?.Item != null)
            {
                _pins.Drop(loot.GetNetId());
            }
        }

        private void DropMissing()
        {
            var world = Singleton<GameWorld>.Instance;
            foreach (var netId in _pins.Keys.ToList())
            {
                if (!world.LootItems.ContainsKey(netId))
                {
                    _pins.Drop(netId);
                }
            }
        }

        private void Place(LootItem loot)
        {
            if (Map == null || loot?.Item == null)
            {
                return;
            }

            var netId = loot.GetNetId();
            if (_pins.Has(netId))
            {
                return;
            }

            _pins.Put(netId, Map.AddTransformMarker(
                loot.TrackableTransform,
                loot.Item.ShortName.BsgLocalized(),
                "Backpack",
                Settings.BackpackColor.Value,
                Icon,
                Size));
        }

        private void Unhook()
        {
            LootItemInitPatch.OnRegisterLoot -= OnLoot;
            GameWorldDestroyLootPatch.OnDestroyLoot -= OnGone;
        }
    }
}
