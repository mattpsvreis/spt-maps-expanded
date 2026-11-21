using System;
using System.Collections.Generic;
using System.IO;
using MapsExtended.Config;
using MapsExtended.Core;
using MapsExtended.Presentation.Overlays;
using MapsExtended.Integration;
using MapsExtended.Presentation;
using EFT.InventoryLogic;

namespace MapsExtended.Markers
{
    using MapMarker = Presentation.Overlays.MapMarker;

    public class LockedDoorMarkerMutator : MarkerProviderBase
    {
        private static readonly string DoorWithKeyPath = Path.Combine(Plugin.Path, "Markers/door_with_key.png");
        private static readonly string DoorWithLockPath = Path.Combine(Plugin.Path, "Markers/door_with_lock.png");
        private static readonly string CarKeyPath = Path.Combine(Plugin.Path, "Markers/car_key.png");
        private static readonly string CarDoorPath = Path.Combine(Plugin.Path, "Markers/car_door.png");
        private static readonly string SafeWithKeyPath = Path.Combine(Plugin.Path, "Markers/safe_with_key.png");
        private static readonly string SafeWithLockPath = Path.Combine(Plugin.Path, "Markers/safe_with_lock.png");
        private static readonly string KeycardWithCardPath = Path.Combine(Plugin.Path, "Markers/door_keycard_with_keycard.png");
        private static readonly string KeycardWithLockPath = Path.Combine(Plugin.Path, "Markers/door_keycard_with_lock.png");

        private readonly Dictionary<string, List<MapMarker>> _doorMarkersByKey = new(StringComparer.Ordinal);
        private readonly HashSet<MapMarker> _carDoorMarkers = [];
        private readonly HashSet<MapMarker> _safeMarkers = [];
        private readonly HashSet<MapMarker> _keycardMarkers = [];
        private readonly Dictionary<string, int> _keyCounts = new(StringComparer.Ordinal);
        private readonly HashSet<Item> _trackedKeys = [];
        private InventoryController _inventoryController;
        private InventoryEquipment _equipment;

        public override void OnShowInRaid(MapView map)
        {
            StopTracking();

            var player = GameUtils.GetMainPlayer();
            if (player == null)
                return;

            _equipment = player.Inventory.Equipment;
            _inventoryController = player.InventoryController;

            foreach (var marker in map.GetMapMarkersByCategory("LockedDoor"))
            {
                if (string.IsNullOrWhiteSpace(marker.AssociatedItemId))
                    continue;

                RememberSpecialIcon(marker);

                if (!_doorMarkersByKey.TryGetValue(marker.AssociatedItemId, out var markers))
                {
                    markers = [];
                    _doorMarkersByKey[marker.AssociatedItemId] = markers;
                }

                markers.Add(marker);
            }

            if (_doorMarkersByKey.Count == 0)
                return;

            foreach (var item in _equipment.GetAllItems())
            {
                TrackKey(item);
            }

            foreach (var keyId in _doorMarkersByKey.Keys)
            {
                UpdateDoors(keyId);
            }

            if (_inventoryController == null)
                return;

            _inventoryController.AddItemEvent += OnItemAdded;
            _inventoryController.RemoveItemEvent += OnItemRemoved;
        }

        public override void OnShowOutOfRaid(MapView map)
        {
            var profile = GameUtils.GetPlayerProfile();
            if (profile?.Inventory == null)
                return;

            var markers = new List<MapMarker>();

            foreach (var marker in map.GetMapMarkersByCategory("LockedDoor"))
            {
                if (string.IsNullOrWhiteSpace(marker.AssociatedItemId))
                    continue;

                RememberSpecialIcon(marker);
                markers.Add(marker);
            }

            if (markers.Count == 0)
                return;

            var stashItems = new HashSet<string>(StringComparer.Ordinal);
            var equipmentItems = new HashSet<string>(StringComparer.Ordinal);

            foreach (var item in profile.Inventory.Stash.GetAllItems())
            {
                stashItems.Add(item.StringTemplateId);
            }

            foreach (var item in profile.Inventory.Equipment.GetAllItems())
            {
                equipmentItems.Add(item.StringTemplateId);
            }

            foreach (var marker in markers)
            {
                var keyInStash = stashItems.Contains(marker.AssociatedItemId);
                var keyInEquipment = equipmentItems.Contains(marker.AssociatedItemId);

                ApplyDoorVisual(marker, keyInStash || keyInEquipment);
                marker.Color = keyInEquipment
                    ? Settings.LockedDoorHasKeyColor.Value
                    : keyInStash
                        ? Settings.LockedDoorInStashColor.Value
                        : Settings.LockedDoorMissingKeyColor.Value;
            }
        }

        public override void OnMapChanged(MapView map, MapDef mapDef)
        {
            if (GameUtils.IsInRaid())
            {
                OnShowInRaid(map);
            }
            else
            {
                OnShowOutOfRaid(map);
            }
        }

        public override void OnDisable(MapView map)
        {
            StopTracking();
            var markers = map.GetMapMarkersByCategory("LockedDoor");

            foreach (var marker in markers)
            {
                if (string.IsNullOrWhiteSpace(marker.AssociatedItemId))
                {
                    continue;
                }

                RememberSpecialIcon(marker);
                ApplyDoorVisual(marker, hasKey: false);
                marker.Color = Settings.LockedDoorDefaultColor.Value;
            }
        }

        public override void OnRaidEnd(MapView map)
        {
            StopTracking();
        }

        public override void OnColorsChanged(MapView map)
        {
            if (map == null)
            {
                return;
            }

            if (GameUtils.IsInRaid())
            {
                foreach (var keyId in _doorMarkersByKey.Keys)
                {
                    UpdateDoors(keyId);
                }

                return;
            }

            OnShowOutOfRaid(map);
        }

        public override void OnHideInRaid(MapView map)
        {
            StopTracking();
        }

        private void OnItemAdded(AddItemEventArgs args)
        {
            if (args.Status != CommandStatus.Succeed || !args.To.IsChildOf(_equipment))
                return;

            UpdateMovedItems(args.Item, true);
        }

        private void OnItemRemoved(RemoveItemEventArgs args)
        {
            if (args.Status != CommandStatus.Succeed || !args.From.IsChildOf(_equipment))
                return;

            UpdateMovedItems(args.Item, false);
        }

        private void UpdateMovedItems(Item item, bool added)
        {
            if (item is ContainerCollection)
            {
                foreach (var child in item.GetAllItems())
                {
                    UpdateKey(child, added);
                }
            }
            else
            {
                UpdateKey(item, added);
            }
        }

        private void UpdateKey(Item item, bool added)
        {
            if (item == null || !_doorMarkersByKey.ContainsKey(item.StringTemplateId))
                return;

            if (added)
            {
                if (TrackKey(item) && _keyCounts[item.StringTemplateId] == 1)
                    UpdateDoors(item.StringTemplateId);
            }
            else if (_trackedKeys.Remove(item))
            {
                var count = --_keyCounts[item.StringTemplateId];
                if (count == 0)
                    UpdateDoors(item.StringTemplateId);
            }
        }

        private bool TrackKey(Item item)
        {
            if (item == null || !_doorMarkersByKey.ContainsKey(item.StringTemplateId) || !_trackedKeys.Add(item))
                return false;

            _keyCounts.TryGetValue(item.StringTemplateId, out var count);
            _keyCounts[item.StringTemplateId] = count + 1;
            return true;
        }

        private void UpdateDoors(string keyId)
        {
            var hasKey = _keyCounts.TryGetValue(keyId, out var count) && count > 0;
            foreach (var marker in _doorMarkersByKey[keyId])
            {
                ApplyDoorVisual(marker, hasKey);
                marker.Color = hasKey
                    ? Settings.LockedDoorHasKeyColor.Value
                    : Settings.LockedDoorMissingKeyColor.Value;
            }
        }

        private void RememberSpecialIcon(MapMarker marker)
        {
            if (IsCarDoorPath(marker.ImagePath))
            {
                _carDoorMarkers.Add(marker);
            }

            if (IsSafePath(marker.ImagePath))
            {
                _safeMarkers.Add(marker);
            }

            if (IsKeycardPath(marker.ImagePath))
            {
                _keycardMarkers.Add(marker);
            }
        }

        private void ApplyDoorVisual(MapMarker marker, bool hasKey)
        {
            var isCar = _carDoorMarkers.Contains(marker) || IsCarDoorPath(marker.ImagePath);
            if (isCar)
            {
                _carDoorMarkers.Add(marker);
                marker.SetImagePath(hasKey ? CarKeyPath : CarDoorPath);
                return;
            }

            var isSafe = _safeMarkers.Contains(marker) || IsSafePath(marker.ImagePath);
            if (isSafe)
            {
                _safeMarkers.Add(marker);
                marker.SetImagePath(hasKey ? SafeWithKeyPath : SafeWithLockPath);
                return;
            }

            var isKeycard = _keycardMarkers.Contains(marker) || IsKeycardPath(marker.ImagePath);
            if (isKeycard)
            {
                _keycardMarkers.Add(marker);
                marker.SetImagePath(hasKey ? KeycardWithCardPath : KeycardWithLockPath);
                return;
            }

            marker.SetImagePath(hasKey ? DoorWithKeyPath : DoorWithLockPath);
        }

        private static bool IsCarDoorPath(string path)
        {
            var file = IconFileName(path);
            return file.Equals("car_door", StringComparison.OrdinalIgnoreCase)
                   || file.Equals("car_key", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSafePath(string path)
        {
            var file = IconFileName(path);
            return file.Equals("safe_with_lock", StringComparison.OrdinalIgnoreCase)
                   || file.Equals("safe_with_key", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsKeycardPath(string path)
        {
            var file = IconFileName(path);
            return file.Equals("door_keycard_with_lock", StringComparison.OrdinalIgnoreCase)
                   || file.Equals("door_keycard_with_keycard", StringComparison.OrdinalIgnoreCase);
        }

        private static string IconFileName(string path)
        {
            return string.IsNullOrEmpty(path) ? "" : Path.GetFileNameWithoutExtension(path);
        }

        private void StopTracking()
        {
            if (_inventoryController != null)
            {
                _inventoryController.AddItemEvent -= OnItemAdded;
                _inventoryController.RemoveItemEvent -= OnItemRemoved;
                _inventoryController = null;
            }

            _equipment = null;
            _doorMarkersByKey.Clear();
            _carDoorMarkers.Clear();
            _safeMarkers.Clear();
            _keycardMarkers.Clear();
            _keyCounts.Clear();
            _trackedKeys.Clear();
        }
    }
}
