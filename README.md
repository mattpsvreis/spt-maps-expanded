# Maps Expanded

**Maps Expanded** is a full replacement for Tarkov’s map screen on **SPT 4.1.x**. It gives you a real interactive map in hideout and in raid - multi-floor layers, peeks, a minimap, and live markers for extracts, quests, keys, bots, and more - plus bundled map packs and automatic switching when you (optionally) use `Map Variants` by LennoxP90, `Classic Maps` by LennoxP90, or `Manimal's backports`.

## Features

### Map UI
- Replaces the vanilla inventory map screen (toggleable)
- Browse every map out of raid with a map selector
- In raid: full map, hold/toggle peek, and optional corner minimap
- Multi-level maps with auto floor select from player height, or manual slider / hotkeys
- Pan, zoom (mouse or hotkeys), center on player, retain position between opens
- Animated transitions between full map / peek / minimap

### Markers (in raid)
- **You** — player arrow
- **Colours** — every marker type has its own colour under Colours in F12, and changes apply while the map is open
- **Others** — friendlies, PMCs, scavs, bosses, non-vanilla / modded bots (server config can restrict certain categories)
- **Quests** — objective markers in raid; out of raid after you’ve visited that map once (zone cache). Hover a marker to read the objective
- **Extracts** — extracts, transit points, secret extracts; optional open / closed / requirements colouring. Hover an extract that is not open yet to see what it still needs, such as a code-word note, a switch, or a payment
- **Locked doors** — lock / key / keycard / car / safe icons coloured by whether the key is on you, in stash, or missing
- **World** — BTR, airdrops, your dropped backpacks, hidden stashes
- **Corpses** — friendly, player-killed, boss, and other (each toggleable)
- Optional marker icon packs via `Addons/*/Markers` (other modders are welcome to add their own stuff as addons!)

### Faction colours
- Tint bot / player markers by faction when Custom Faction Colours is enabled
- Built-in groups such as Black Division, UNTAR, RUAF, Remnants, ISB, White Tusks
- Discovers extra roles via MoreBotsAPI when present
- Per-faction colours and optional grouping in F12

### Bundled map packs (`Addons/Extended`)
- **Satellite** and **Abstract** styles for stock maps (coverage varies by location)
- **Factory Classic** pack for Factory Classic by LennoxP90
- **Manimal** Interchange / Labs backports oriented packs
- **Icebreaker** (and RE3MR) packs when that map is installed
- **Per-location preference in F12:** Satellite / Abstract / Automatic
- **BONUS:** Other modders are welcome to add their own map packs via addons! Just follow the structure already exemplified in this mod.

### Soft integrations
- **Map Variants** (LennoxP90) — picks the matching Extended pack when a variant resolves
- **Classic Maps** (LennoxP90) — Factory Classic alias when classic Factory is active
- **Manimal**'s Interchange / Labs / Icebreaker backports — matching packs when those maps load
- **FIKA** — headless clients skip the UI; co-op friendly markers when allowed
- **SamSWAT HeliCrash** — optional crash-site marker
- **MoreBotsAPI** — extra faction dynamic colour discovery

## Install

1. Download the release files.
2. Extract the files from the provided .zip into the root folder of your SPT installation (the folder where SPT_Runtime and BepInEx resides at)

## Requirements

- SPT 4.1.x
- **Soft dependencies (optional, unlock related map packs, or features):** Map Variants by LennoxP90, Classic Maps ports by LennoxP90, Manimal's Interchange / Labs / Icebreaker backports, MoreBotsAPI, FIKA

## Compatibility

This mod is not compatible with:

- Dynamic Maps
- Dynamic Maps Extended
- Dynamic Maps Plus

This mod is compatible with:

- LennoxP90's Map Variants
- Manimal's backports
- LennoxP90's Classic ports

## F12 config

Everything above is toggled in Configuration Manager under Maps Expanded: General, Markers, In-Raid, Minimap, Colours, Extra mods, Map styles, and Faction Colours.

## Licensing

| Piece | Licence |
|---|---|
| Plugin / server C# | **MIT** — `LICENSE`, `NOTICE` |
| Map artwork / data packs | **Not MIT** — `THIRD-PARTY.md` |
| Tarkov.dev SVG / Icebreaker adaptations | **CC BY-NC-SA 4.0** |

Maps Expanded is MIT-licensed. Upstream copyright notices for incorporated software are retained in `LICENSE` / `NOTICE`.