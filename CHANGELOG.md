# Changelog

All notable changes to Maps Expanded are recorded here. Newer versions are at the top. This file starts with 1.0.0, the first version in this repository.

## 1.1.0 — 2026-11-20

### Added

- Keyed valuables safes on Shoreline and Streets, using the safe lock and safe key icons. They follow the same key-status colours as locked doors.
- Keycard doors use the keycard lock and keycard icons. Labs coloured cards, the Labs residential unit, the blue-marked card, Interchange Object #11SR and #21WS, and the Factory storage room are included. Mechanical keys, including the Labs manager, weapon-testing, and arsenal keys, stay on the door icon.
- A colour setting for every marker type that still used a fixed tint:
  - Friendly players
  - Friendly corpses
  - Corpses killed by a friendly player
  - Quest objectives
  - Locked doors when the key is on you, in your stash, or missing
  - Locked doors when status colouring is turned off
  - Crashed helicopter sites, shown in F12 only when SamSWAT HeliCrash is installed
- Colour changes in **6. Marker Colors** apply to markers already on the map, including the colours that already had a setting (you, Bear, USEC, scavs, bosses, modded bots, extracts, transits, secrets, loot, backpacks, airdrops, the BTR, hidden stashes, and corpses).
- Hovering a quest marker shows the objective, using the same description as the quest log. The quest name stays under the icon. Several objectives on the same spot are listed together.
- Hovering an extract that is not open yet shows what it still needs. That is the code-word note for a secret extract, the switch that has to be thrown, a payment, or another requirement taken from the extract itself. The note clears once the extract can be used.

Boss corpses still share **Killed boss corpse marker color**. With Custom Faction Colours enabled, a faction tint still replaces the Bear, USEC, scav, boss, and friendly colours for that bot.

## 1.0.0 — 2026-11-20

First release. Maps Expanded replaces the inventory map screen on SPT 4.1.x with an in-raid and out-of-raid map, live markers, and bundled map packs. It is incompatible with DynamicMaps, DynamicMaps Extended, and DynamicMaps Plus.

### Added

- Inventory map screen replacement, with a map selector out of raid.
- In raid: full map, hold or toggle peek, and an optional corner minimap.
- Multi-level maps, with floor select from player height or from the slider and hotkeys.
- Pan, zoom, center on player, and optional remembered map position.
- Markers for you, friendlies, PMCs, scavs, bosses, modded bots, quests, extracts, transits, secret extracts, locked doors, the BTR, airdrops, dropped backpacks, wishlist loot, hidden stashes, and corpses.
- Marker icon packs from `Addons/*/Markers`.
- Optional paper-map requirement, and Intel Center level gates for several marker categories.
- Custom faction colours, including Black Division, UNTAR, RUAF, Remnants, ISB, and White Tusks, plus extra roles discovered through MoreBotsAPI.
- Bundled Satellite and Abstract packs, Factory Classic, Manimal Interchange and Labs packs, and Icebreaker packs, with a per-location Satellite / Abstract / Automatic choice in F12.
- Automatic pack selection for Map Variants, Classic Maps, and Manimal's Interchange, Labs, and Icebreaker backports.
- FIKA headless clients skip the map UI. SamSWAT HeliCrash can show a crash-site marker.
- A server mod that can restrict which player and bot marker categories clients are allowed to show.

### Changed

- Renamed from Maps Extended to Maps Expanded. The plugin id is `com.dark.mapsexpanded`.
- Quest markers can show out of raid after you have visited that map once. Zone positions are cached in `Cache/quest_zones.json`.
- Car extracts use the car door and car key icons.
- The F12 marker section is named Markers.

### Fixed

- Extracts that place two triggers a few metres apart for the same exit now share one marker. A second entrance farther away still gets its own marker. A shared marker uses the more open of the two statuses.
- Changing a marker colour keeps the floor fade. Hidden and lower floors stay faded.
- If opening the map screen fails, the vanilla map stays closed instead of drawing behind the inventory tabs.
- A TextMeshPro error during layout no longer aborts the map screen and reveals the vanilla map.
- Addon maps whose display name already contains " by " are not labeled with the addon name a second time.
- The Factory Classic pack is listed as "Factory Classic by LennoxP90".
