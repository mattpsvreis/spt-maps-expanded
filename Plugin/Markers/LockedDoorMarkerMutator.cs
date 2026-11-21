using System;
using System.Collections.Generic;
using System.IO;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Integration;
using MapsExtended.Presentation.Overlays;
using EFT.InventoryLogic;

namespace MapsExtended.Markers
{
    using MapMarker = Presentation.Overlays.MapMarker;

    public class LockedDoorMarkerMutator : PinSource
    {
        private static readonly string DoorWithKey = Path.Combine(Plugin.Path, "Markers/door_with_key.png");
        private static readonly string DoorWithLock = Path.Combine(Plugin.Path, "Markers/door_with_lock.png");
        private static readonly string CarKey = Path.Combine(Plugin.Path, "Markers/car_key.png");
        private static readonly string CarDoor = Path.Combine(Plugin.Path, "Markers/car_door.png");
        private static readonly string SafeWithKey = Path.Combine(Plugin.Path, "Markers/safe_with_key.png");
        private static readonly string SafeWithLock = Path.Combine(Plugin.Path, "Markers/safe_with_lock.png");
        private static readonly string KeycardWithCard = Path.Combine(Plugin.Path, "Markers/door_keycard_with_keycard.png");
        private static readonly string KeycardWithLock = Path.Combine(Plugin.Path, "Markers/door_keycard_with_lock.png");

        private readonly Dictionary<string, List<MapMarker>> _doors = new(StringComparer.Ordinal);
        private readonly HashSet<MapMarker> _cars = [];
        private readonly HashSet<MapMarker> _safes = [];
        private readonly HashSet<MapMarker> _keycards = [];
        private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
        private readonly HashSet<Item> _tracked = [];
        private InventoryController _inventory;
        private InventoryEquipment _equipment;

        protected override void ShowInRaid(MapCanvas map)
        {
            Stop();
            var player = GameUtils.GetMainPlayer();
            if (player == null)
            {
                return;
            }

            _equipment = player.Inventory.Equipment;
            _inventory = player.InventoryController;
            foreach (var marker in map.GetMapMarkersByCategory("LockedDoor"))
            {
                if (string.IsNullOrWhiteSpace(marker.AssociatedItemId))
                {
                    continue;
                }

                Remember(marker);
                if (!_doors.TryGetValue(marker.AssociatedItemId, out var list))
                {
                    list = [];
                    _doors[marker.AssociatedItemId] = list;
                }

                list.Add(marker);
            }

            if (_doors.Count == 0)
            {
                return;
            }

            foreach (var item in _equipment.GetAllItems())
            {
                Track(item);
            }

            foreach (var keyId in _doors.Keys)
            {
                Paint(keyId);
            }

            if (_inventory == null)
            {
                return;
            }

            _inventory.AddItemEvent += OnAdded;
            _inventory.RemoveItemEvent += OnRemoved;
        }

        protected override void ShowOutOfRaid(MapCanvas map)
        {
            var profile = GameUtils.GetPlayerProfile();
            if (profile?.Inventory == null)
            {
                return;
            }

            var markers = new List<MapMarker>();
            foreach (var marker in map.GetMapMarkersByCategory("LockedDoor"))
            {
                if (string.IsNullOrWhiteSpace(marker.AssociatedItemId))
                {
                    continue;
                }

                Remember(marker);
                markers.Add(marker);
            }

            if (markers.Count == 0)
            {
                return;
            }

            var stash = new HashSet<string>(StringComparer.Ordinal);
            var gear = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in profile.Inventory.Stash.GetAllItems())
            {
                stash.Add(item.StringTemplateId);
            }

            foreach (var item in profile.Inventory.Equipment.GetAllItems())
            {
                gear.Add(item.StringTemplateId);
            }

            foreach (var marker in markers)
            {
                var inStash = stash.Contains(marker.AssociatedItemId);
                var inGear = gear.Contains(marker.AssociatedItemId);
                Draw(marker, inStash || inGear);
                marker.Color = inGear
                    ? Settings.LockedDoorHasKeyColor.Value
                    : inStash
                        ? Settings.LockedDoorInStashColor.Value
                        : Settings.LockedDoorMissingKeyColor.Value;
            }
        }

        protected override void MapChanged(MapCanvas map, MapDef mapDef)
        {
            if (GameUtils.IsInRaid())
            {
                ShowInRaid(map);
            }
            else
            {
                ShowOutOfRaid(map);
            }
        }

        protected override void Disable(MapCanvas map)
        {
            Stop();
            foreach (var marker in map.GetMapMarkersByCategory("LockedDoor"))
            {
                if (string.IsNullOrWhiteSpace(marker.AssociatedItemId))
                {
                    continue;
                }

                Remember(marker);
                Draw(marker, false);
                marker.Color = Settings.LockedDoorDefaultColor.Value;
            }
        }

        protected override void EndRaid(MapCanvas map) => Stop();

        protected override void HideInRaid(MapCanvas map) => Stop();

        protected override void ColorsChanged(MapCanvas map)
        {
            if (map == null)
            {
                return;
            }

            if (GameUtils.IsInRaid())
            {
                foreach (var keyId in _doors.Keys)
                {
                    Paint(keyId);
                }

                return;
            }

            ShowOutOfRaid(map);
        }

        private void OnAdded(AddItemEventArgs args)
        {
            if (args.Status != CommandStatus.Succeed || !args.To.IsChildOf(_equipment))
            {
                return;
            }

            Move(args.Item, true);
        }

        private void OnRemoved(RemoveItemEventArgs args)
        {
            if (args.Status != CommandStatus.Succeed || !args.From.IsChildOf(_equipment))
            {
                return;
            }

            Move(args.Item, false);
        }

        private void Move(Item item, bool added)
        {
            if (item is ContainerCollection)
            {
                foreach (var child in item.GetAllItems())
                {
                    Change(child, added);
                }
            }
            else
            {
                Change(item, added);
            }
        }

        private void Change(Item item, bool added)
        {
            if (item == null || !_doors.ContainsKey(item.StringTemplateId))
            {
                return;
            }

            if (added)
            {
                if (Track(item) && _counts[item.StringTemplateId] == 1)
                {
                    Paint(item.StringTemplateId);
                }
            }
            else if (_tracked.Remove(item))
            {
                if (--_counts[item.StringTemplateId] == 0)
                {
                    Paint(item.StringTemplateId);
                }
            }
        }

        private bool Track(Item item)
        {
            if (item == null || !_doors.ContainsKey(item.StringTemplateId) || !_tracked.Add(item))
            {
                return false;
            }

            _counts.TryGetValue(item.StringTemplateId, out var count);
            _counts[item.StringTemplateId] = count + 1;
            return true;
        }

        private void Paint(string keyId)
        {
            var hasKey = _counts.TryGetValue(keyId, out var count) && count > 0;
            foreach (var marker in _doors[keyId])
            {
                Draw(marker, hasKey);
                marker.Color = hasKey
                    ? Settings.LockedDoorHasKeyColor.Value
                    : Settings.LockedDoorMissingKeyColor.Value;
            }
        }

        private void Remember(MapMarker marker)
        {
            if (IsCar(marker.ImagePath))
            {
                _cars.Add(marker);
            }

            if (IsSafe(marker.ImagePath))
            {
                _safes.Add(marker);
            }

            if (IsKeycard(marker.ImagePath))
            {
                _keycards.Add(marker);
            }
        }

        private void Draw(MapMarker marker, bool hasKey)
        {
            if (_cars.Contains(marker) || IsCar(marker.ImagePath))
            {
                _cars.Add(marker);
                marker.SetImagePath(hasKey ? CarKey : CarDoor);
                return;
            }

            if (_safes.Contains(marker) || IsSafe(marker.ImagePath))
            {
                _safes.Add(marker);
                marker.SetImagePath(hasKey ? SafeWithKey : SafeWithLock);
                return;
            }

            if (_keycards.Contains(marker) || IsKeycard(marker.ImagePath))
            {
                _keycards.Add(marker);
                marker.SetImagePath(hasKey ? KeycardWithCard : KeycardWithLock);
                return;
            }

            marker.SetImagePath(hasKey ? DoorWithKey : DoorWithLock);
        }

        private static bool IsCar(string path)
        {
            var file = FileName(path);
            return file.Equals("car_door", StringComparison.OrdinalIgnoreCase)
                   || file.Equals("car_key", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSafe(string path)
        {
            var file = FileName(path);
            return file.Equals("safe_with_lock", StringComparison.OrdinalIgnoreCase)
                   || file.Equals("safe_with_key", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsKeycard(string path)
        {
            var file = FileName(path);
            return file.Equals("door_keycard_with_lock", StringComparison.OrdinalIgnoreCase)
                   || file.Equals("door_keycard_with_keycard", StringComparison.OrdinalIgnoreCase);
        }

        private static string FileName(string path)
        {
            return string.IsNullOrEmpty(path) ? "" : Path.GetFileNameWithoutExtension(path);
        }

        private void Stop()
        {
            if (_inventory != null)
            {
                _inventory.AddItemEvent -= OnAdded;
                _inventory.RemoveItemEvent -= OnRemoved;
                _inventory = null;
            }

            _equipment = null;
            _doors.Clear();
            _cars.Clear();
            _safes.Clear();
            _keycards.Clear();
            _counts.Clear();
            _tracked.Clear();
        }
    }
}
