# Maps Extended

Standalone SPT map plugin for **SPT 4.1.x**. One install covers the full map UI, faction marker colours, Map Variants / Factory Classic aliases, and bundled Extended map packs — no companion plugin required.

## Features

- Full in-raid / out-of-raid map UI (peek, minimap, markers, extracts, quests, etc.)
- **Per-faction marker colours** — Black Division, UNTAR, RUAF, Remnant, ISB, White Tusks, plus MoreBotsAPI discovery
- **Map Variants / Factory Classic / Manimal backport** auto-selection for matching Extended packs
- Bundled **map packs** under `Addons/Extended` (Satellite, Abstract, Icebreaker, Classic, Manimal, …)

## Install

1. Remove older map plugins if present (DynamicMaps, DynamicMaps Extended, DynamicMaps Plus) — Maps Extended replaces them and is incompatible with them.
2. Copy `BepInEx/plugins/MapsExtended` (and `SPT_Runtime/user/mods/MapsExtended` if included) into your SPT root.
3. Ensure `Unity.VectorGraphics` is installed as required by the map renderer.

## Requirements

- SPT 4.1.x
- Soft dependencies (optional, unlock related packs / colours): Map Variants, Factory Classic, Manimal Interchange / Labs / Icebreaker, MoreBotsAPI, Fika

## F12 config

- **Faction Colours** — enable + per-faction colours / optional grouping
- **Map Selection** — Satellite / Abstract / Automatic preferences per location
- Backport / Classic maps auto-select when Map Variants or Factory Classic activates them

## Licensing

| Piece | Licence |
|---|---|
| Plugin / server C# | **MIT** — `LICENSE`, `NOTICE` |
| Map artwork / data packs | **Not MIT** — `THIRD-PARTY.md` |
| Tarkov.dev SVG / Icebreaker adaptations | **CC BY-NC-SA 4.0** |

Maps Extended is MIT-licensed. Upstream copyright notices for incorporated software are retained in `LICENSE` / `NOTICE`.
