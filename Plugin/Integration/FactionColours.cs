using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using EFT;
using HarmonyLib;
using MapsExtended.Presentation.Overlays;
using UnityEngine;
using UnityEngine.UI;

namespace MapsExtended.Integration
{
    /// <summary>
    /// F12 colours for recognised custom bot packs. Marker categories still hide
    /// soldiers through the pin board; this only changes the RGB of markers that stay visible.
    /// </summary>
    internal static class FactionColours
    {
        internal const string AutomaticGroup = "Automatic";
        internal const string NativeGroup = "Maps Expanded default";

        internal static ConfigEntry<bool> Enabled;
        internal static readonly Dictionary<string, ConfigEntry<Color>> Colours = new();

        private static readonly Dictionary<int, string> RoleToGroup = new();
        private static readonly Dictionary<int, ConfigEntry<string>> RoleOverrides = new();
        private static readonly HashSet<int> UntarBossIds = new();
        private static readonly Dictionary<string, MemberInfo> MemberCache = new();
        private static readonly HashSet<string> LoggedOnce = new();

        private static ConfigFile _file;
        private static PropertyInfo _observedAi;
        private static bool _observedAiLookedUp;
        private static bool _notedMissingApi;
        private static bool _rolesImported;
        private static bool _ready;

        private readonly struct BuiltIn
        {
            internal readonly string Name;
            internal readonly Color32 Colour;
            internal readonly int Order;

            internal BuiltIn(string name, byte r, byte g, byte b, int order)
            {
                Name = name;
                Colour = new Color32(r, g, b, 255);
                Order = order;
            }
        }

        private static readonly BuiltIn[] Palette =
        {
            new("Black Division", 0x33, 0x33, 0x33, 90),
            new("UNTAR", 0, 191, 255, 70),
            new("RUAF", 137, 155, 69, 60),
            new("Remnant", 0x5B, 0x80, 0x45, 50),
            new("ISB", 0x1A, 0xFF, 0xC2, 40),
            new("White Tusks", 232, 232, 240, 30),
        };

        // First-time defaults for roles that land as Detected - <token>.
        private static readonly Dictionary<string, Color32> DetectedDefaults = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Detected - chaosInsurgency"] = new Color32(0x7C, 0x50, 0x5E, 0xFF),
            ["Detected - civilian"] = new Color32(0xFF, 0xFF, 0xFF, 0xFF),
        };

        internal static void Initialize(ConfigFile config)
        {
            if (_ready)
            {
                return;
            }

            _ready = true;
            _file = config;
            Enabled = config.Bind(
                "8. Faction Colours",
                "Enable faction colours",
                true,
                new ConfigDescription(
                    "Colour recognised addon factions, such as Black Division, UNTAR, RUAF, and ISB. Hiding a marker category in What to show still hides them.",
                    null,
                    new ConfigurationManagerAttributes { Order = 100 }));
            Enabled.SettingChanged += OnSettingChanged;

            foreach (var swatch in Palette)
            {
                BindColour(swatch.Name, swatch.Colour, swatch.Order);
            }

            ImportRoles();
            Plugin.Log?.LogInfo("[MapsExpanded] Faction colours ready.");
        }

        internal static void Shutdown() => FactionMarkerTint.RestoreAll();

        internal static bool IsUntarBossRole(IPlayer player)
        {
            try
            {
                if (!LooksLikeBot(player) || player.Profile.Side != EPlayerSide.Savage)
                {
                    return false;
                }

                ImportRoles();
                var role = player.Profile.Info.Settings.Role;
                return UntarBossIds.Contains((int)role) || FactionRolePolicy.IsUntarBoss(role.ToString());
            }
            catch (Exception ex)
            {
                Note("untar-boss", ex);
                return false;
            }
        }

        /// <summary>
        /// Pins a live tint onto a player marker. The Enabled toggle is read when the tint
        /// is applied, so an F12 change updates markers that are already on the map.
        /// </summary>
        internal static bool TryTintMarker(IPlayer player, PlayerMapMarker marker, Color nativeColour)
        {
            try
            {
                if (marker == null || !LooksLikeBot(player))
                {
                    return false;
                }

                ImportRoles();
                var role = player.Profile.Info.Settings.Role;
                var roleId = (int)role;
                if (!RoleToGroup.TryGetValue(roleId, out var group))
                {
                    group = FactionRolePolicy.Resolve(role.ToString(), null);
                }

                if (group == null)
                {
                    return false;
                }

                var tint = marker.GetComponent<FactionMarkerTint>();
                if (tint == null)
                {
                    tint = marker.gameObject.AddComponent<FactionMarkerTint>();
                }

                tint.Initialize(marker.Image, marker.Label, group, nativeColour, roleId);
                return true;
            }
            catch (Exception ex)
            {
                Note("marker-tint", ex);
                return false;
            }
        }

        internal static string ResolveGroup(int roleId, string fallback)
        {
            if (RoleOverrides.TryGetValue(roleId, out var choice))
            {
                if (choice.Value == NativeGroup)
                {
                    return null;
                }

                if (choice.Value != AutomaticGroup && Colours.ContainsKey(choice.Value))
                {
                    return choice.Value;
                }
            }

            return RoleToGroup.TryGetValue(roleId, out var mapped) ? mapped : fallback;
        }

        private static bool LooksLikeBot(IPlayer player)
        {
            if (player?.Profile == null)
            {
                return false;
            }

            if (player.IsAI)
            {
                return true;
            }

            if (!_observedAiLookedUp)
            {
                _observedAiLookedUp = true;
                _observedAi = AccessTools.Property(player.GetType(), "IsObservedAI")
                    ?? AccessTools.Property(typeof(IPlayer), "IsObservedAI");
            }

            try
            {
                return _observedAi != null && _observedAi.GetValue(player) is true;
            }
            catch
            {
                return false;
            }
        }

        private static void BindColour(string name, Color colour, int order)
        {
            var entry = _file.Bind(
                "8. Faction Colours",
                name,
                colour,
                new ConfigDescription(
                    "Colour for this faction's soldiers, bosses, and followers. Markers on other floors still fade.",
                    null,
                    new ConfigurationManagerAttributes { Order = order }));
            Colours[name] = entry;
            entry.SettingChanged += OnSettingChanged;
        }

        private static void OnSettingChanged(object sender, EventArgs args) => FactionMarkerTint.RefreshAll();

        private static object Member(object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            var type = target.GetType();
            var key = type.AssemblyQualifiedName + "|" + name;
            if (!MemberCache.TryGetValue(key, out var member))
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                member = (MemberInfo)type.GetField(name, flags) ?? type.GetProperty(name, flags);
                MemberCache[key] = member;
            }

            switch (member)
            {
                case PropertyInfo property:
                    return property.GetValue(target);
                case FieldInfo field:
                    return field.GetValue(target);
                default:
                    return null;
            }
        }

        private static void ImportRoles()
        {
            if (_rolesImported)
            {
                return;
            }

            var manager = AccessTools.TypeByName("MoreBotsAPI.CustomWildSpawnTypeManager");
            if (manager == null)
            {
                if (!_notedMissingApi)
                {
                    _notedMissingApi = true;
                    Plugin.Log?.LogInfo("[MapsExpanded] MoreBotsAPI is not loaded; faction colours use known role names.");
                }

                return;
            }

            try
            {
                var getter = AccessTools.Method(manager, "GetCustomWildSpawnTypes");
                if (getter?.Invoke(null, null) is not IEnumerable rows)
                {
                    _rolesImported = true;
                    return;
                }

                var found = new List<RoleRow>();
                foreach (var row in rows)
                {
                    try
                    {
                        if (Member(row, "WildSpawnTypeName") is not string role || string.IsNullOrWhiteSpace(role))
                        {
                            continue;
                        }

                        found.Add(new RoleRow(
                            Convert.ToInt32(Member(row, "WildSpawnTypeValue")),
                            role,
                            Member(row, "ScavRole") as string));
                    }
                    catch (Exception ex)
                    {
                        Note("registry-entry", ex);
                    }
                }

                found.Sort((a, b) => string.CompareOrdinal(a.Role, b.Role));
                foreach (var bot in found)
                {
                    if (RoleToGroup.ContainsKey(bot.Id))
                    {
                        continue;
                    }

                    var group = FactionRolePolicy.Resolve(bot.Role, bot.ScavRole)
                        ?? "Detected - " + ConfigToken(bot.Role);
                    if (!Colours.ContainsKey(group))
                    {
                        var colour = DetectedDefaults.TryGetValue(group, out var preferred)
                            ? (Color)preferred
                            : ColourFromRole(bot.Role);
                        BindColour(group, colour, 0);
                    }

                    RoleToGroup[bot.Id] = group;
                    if (FactionRolePolicy.IsUntarBoss(bot.Role))
                    {
                        UntarBossIds.Add(bot.Id);
                    }
                }

                var options = new List<string> { AutomaticGroup, NativeGroup };
                options.AddRange(Colours.Keys.OrderBy(name => name, StringComparer.Ordinal));
                var list = new AcceptableValueList<string>(options.ToArray());

                foreach (var bot in found)
                {
                    if (RoleOverrides.ContainsKey(bot.Id))
                    {
                        continue;
                    }

                    var entry = _file.Bind(
                        "8a. Dynamic faction grouping",
                        "Group with - " + ConfigToken(bot.Role),
                        AutomaticGroup,
                        new ConfigDescription(
                            $"Automatic uses {RoleToGroup[bot.Id]}. Pick another faction to share its colour, or Maps Expanded default to keep the normal marker colour.",
                            list));
                    RoleOverrides[bot.Id] = entry;
                    entry.SettingChanged += OnSettingChanged;
                }

                _rolesImported = true;
                Plugin.Log?.LogInfo($"[MapsExpanded] Faction colours: {found.Count} custom roles, {Colours.Count} groups.");
            }
            catch (Exception ex)
            {
                Note("registry", ex);
            }
        }

        /// <summary>
        /// BepInEx setting ids cannot contain every character a role name might.
        /// Letters, digits, '-' and '_' stay as-is; anything else becomes ~XXXX.
        /// </summary>
        private static string ConfigToken(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                {
                    builder.Append(c);
                }
                else
                {
                    builder.Append('~').Append(((int)c).ToString("X4"));
                }
            }

            return builder.ToString();
        }

        private static Color ColourFromRole(string role)
        {
            uint hash = 2166136261;
            foreach (var c in role)
            {
                hash = unchecked((hash ^ c) * 16777619);
            }

            return new Color32(
                (byte)(80 + (hash % 160)),
                (byte)(80 + ((hash >> 8) % 160)),
                (byte)(80 + ((hash >> 16) % 160)),
                255);
        }

        private static void Note(string key, Exception ex)
        {
            if (LoggedOnce.Add(key))
            {
                Plugin.Log?.LogWarning($"[MapsExpanded] Faction colours ({key}): {ex.Message}");
            }
        }

        private readonly struct RoleRow
        {
            internal readonly int Id;
            internal readonly string Role;
            internal readonly string ScavRole;

            internal RoleRow(int id, string role, string scavRole)
            {
                Id = id;
                Role = role;
                ScavRole = scavRole;
            }
        }
    }

    internal sealed class FactionMarkerTint : MonoBehaviour
    {
        private static readonly List<FactionMarkerTint> Live = new();

        private Image _image;
        private Graphic _label;
        private string _group;
        private Color _floorColour;
        private int _roleId;

        internal void Initialize(Image image, Graphic label, string faction, Color native, int roleId)
        {
            _image = image;
            _label = label;
            _group = faction;
            _floorColour = native;
            _roleId = roleId;
            if (!Live.Contains(this))
            {
                Live.Add(this);
            }

            Apply();
        }

        internal void NativeColourChanged(Color colour)
        {
            _floorColour = colour;
            Apply();
        }

        internal void Apply(bool restore = false)
        {
            if (_image == null)
            {
                return;
            }

            var colour = _floorColour;
            var group = FactionColours.ResolveGroup(_roleId, _group);
            if (!restore
                && FactionColours.Enabled is { Value: true }
                && group != null
                && FactionColours.Colours.TryGetValue(group, out var entry))
            {
                colour = entry.Value;
            }

            Paint(_image, colour);
            if (_label != null)
            {
                Paint(_label, colour);
            }
        }

        private static void Paint(Graphic graphic, Color colour)
        {
            colour.a = graphic.color.a;
            if (graphic.color != colour)
            {
                graphic.color = colour;
            }
        }

        internal static void RefreshAll() => Visit(restore: false);

        internal static void RestoreAll() => Visit(restore: true);

        private static void Visit(bool restore)
        {
            for (var i = Live.Count - 1; i >= 0; i--)
            {
                var tint = Live[i];
                if (tint == null)
                {
                    Live.RemoveAt(i);
                    continue;
                }

                try
                {
                    tint.Apply(restore);
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[MapsExpanded] Faction tint: {ex.Message}");
                }
            }
        }

        private void OnDestroy() => Live.Remove(this);
    }
}
