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
    /// Per-faction marker colouring for recognised custom bot packs.
    /// Tracking / visibility stay with the core marker providers.
    /// </summary>
    internal static class FactionColours
    {
        internal static ConfigEntry<bool> Enabled;
        internal static readonly Dictionary<string, ConfigEntry<Color>> Colours = new();
        private static readonly Dictionary<int, string> Roles = new();
        private static readonly Dictionary<int, ConfigEntry<string>> GroupOverrides = new();
        private static ConfigFile _config;
        internal const string AutomaticGroup = "Automatic";
        internal const string NativeGroup = "Maps Expanded default";
        private static readonly HashSet<int> UntarBossRoles = new();
        private static readonly Dictionary<string, MemberInfo> Members = new();
        private static readonly HashSet<string> Warnings = new();
        private static PropertyInfo _observedAiProperty;
        private static bool _observedAiResolved;
        private static bool _registryAttempted;
        private static bool _registryLoaded;
        private static bool _initialized;

        internal static void Initialize(ConfigFile config)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _config = config;
            Enabled = config.Bind(
                "8. Faction Colours",
                "Enable faction colours",
                true,
                new ConfigDescription(
                    "Colour recognised addon factions, such as Black Division, UNTAR, RUAF, and ISB. Hiding a marker category in What to show still hides them.",
                    null,
                    new ConfigurationManagerAttributes { Order = 100 }));

            Bind(config, "Black Division", new Color32(119, 119, 119, 255), 90);
            Bind(config, "UNTAR", new Color32(0, 191, 255, 255), 70);
            Bind(config, "RUAF", new Color32(137, 155, 69, 255), 60);
            Bind(config, "Remnant", new Color32(137, 155, 69, 255), 50);
            Bind(config, "ISB", new Color32(26, 188, 156, 255), 40);
            Bind(config, "White Tusks", new Color32(232, 232, 240, 255), 30);

            Enabled.SettingChanged += SettingsChanged;
            TryReadRegistry();
            Plugin.Log?.LogInfo("[MapsExpanded] Faction colour support ready.");
        }

        internal static bool IsUntarBossRole(IPlayer player)
        {
            try
            {
                if (player?.Profile == null || !IsBot(player) || player.Profile.Side != EPlayerSide.Savage)
                {
                    return false;
                }

                TryReadRegistry();
                var role = player.Profile.Info.Settings.Role;
                return UntarBossRoles.Contains((int)role) || FactionRolePolicy.IsUntarBoss(role.ToString());
            }
            catch (Exception ex)
            {
                Warn("untar-boss", ex);
                return false;
            }
        }

        /// <summary>
        /// Attaches a faction tint after a player marker is created.
        /// Tint respects the Enabled toggle at apply-time so F12 changes refresh live.
        /// </summary>
        internal static bool TryTintMarker(IPlayer player, PlayerMapMarker marker, Color nativeColour)
        {
            try
            {
                if (marker == null || player?.Profile == null || !IsBot(player))
                {
                    return false;
                }

                TryReadRegistry();
                var role = player.Profile.Info.Settings.Role;
                var id = (int)role;
                if (!Roles.TryGetValue(id, out var faction))
                {
                    faction = FactionRolePolicy.Resolve(role.ToString(), null);
                }

                if (faction == null)
                {
                    return false;
                }

                var tint = marker.GetComponent<FactionMarkerTint>() ?? marker.gameObject.AddComponent<FactionMarkerTint>();
                tint.Initialize(marker.Image, marker.Label, faction, nativeColour, id);
                return true;
            }
            catch (Exception ex)
            {
                Warn("marker-tint", ex);
                return false;
            }
        }

        private static bool IsBot(IPlayer player)
        {
            if (player == null)
            {
                return false;
            }

            if (player.IsAI)
            {
                return true;
            }

            if (!_observedAiResolved)
            {
                _observedAiResolved = true;
                _observedAiProperty = AccessTools.Property(player.GetType(), "IsObservedAI")
                    ?? AccessTools.Property(typeof(IPlayer), "IsObservedAI");
            }

            try
            {
                return _observedAiProperty?.GetValue(player) is bool observed && observed;
            }
            catch
            {
                return false;
            }
        }

        private static void Bind(ConfigFile config, string name, Color colour, int order)
        {
            var entry = config.Bind(
                "8. Faction Colours",
                name,
                colour,
                new ConfigDescription(
                    "Colour for this faction's soldiers, bosses, and followers. Markers on other floors still fade.",
                    null,
                    new ConfigurationManagerAttributes { Order = order }));
            Colours[name] = entry;
            entry.SettingChanged += SettingsChanged;
        }

        private static void SettingsChanged(object sender, EventArgs args) => FactionMarkerTint.RefreshAll();

        private static object Read(object target, string name)
        {
            if (target == null)
            {
                return null;
            }

            var type = target.GetType();
            var key = type.AssemblyQualifiedName + "|" + name;
            if (!Members.TryGetValue(key, out var member))
            {
                member = (MemberInfo)type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                Members[key] = member;
            }

            return member is PropertyInfo p ? p.GetValue(target) : (member as FieldInfo)?.GetValue(target);
        }

        private static void TryReadRegistry()
        {
            if (_registryLoaded)
            {
                return;
            }

            var manager = AccessTools.TypeByName("MoreBotsAPI.CustomWildSpawnTypeManager");
            if (manager == null)
            {
                // Soft dependency may load later — allow another attempt next call.
                if (!_registryAttempted)
                {
                    _registryAttempted = true;
                    Plugin.Log?.LogInfo("[MapsExpanded] MoreBotsAPI absent; using known role-name fallback for faction colours.");
                }

                return;
            }

            _registryAttempted = true;

            try
            {
                var getter = AccessTools.Method(manager, "GetCustomWildSpawnTypes");
                if (getter == null || getter.Invoke(null, null) is not IEnumerable entries)
                {
                    _registryLoaded = true;
                    return;
                }

                var discovered = new List<(int id, string role, string metadata)>();
                foreach (var entry in entries)
                {
                    try
                    {
                        var role = Read(entry, "WildSpawnTypeName") as string;
                        if (string.IsNullOrWhiteSpace(role))
                        {
                            continue;
                        }

                        var id = Convert.ToInt32(Read(entry, "WildSpawnTypeValue"));
                        var group = Read(entry, "ScavRole") as string;
                        discovered.Add((id, role, group));
                    }
                    catch (Exception ex)
                    {
                        Warn("registry-entry", ex);
                    }
                }

                foreach (var bot in discovered.OrderBy(x => x.role, StringComparer.Ordinal))
                {
                    if (Roles.ContainsKey(bot.id))
                    {
                        continue;
                    }

                    var faction = FactionRolePolicy.Resolve(bot.role, bot.metadata) ?? ("Detected - " + ConfigName(bot.role));
                    if (!Colours.ContainsKey(faction))
                    {
                        Bind(_config, faction, GeneratedColour(bot.role), 0);
                    }

                    Roles[bot.id] = faction;
                    if (FactionRolePolicy.IsUntarBoss(bot.role))
                    {
                        UntarBossRoles.Add(bot.id);
                    }
                }

                var choices = new[] { AutomaticGroup, NativeGroup }
                    .Concat(Colours.Keys.OrderBy(x => x, StringComparer.Ordinal))
                    .ToArray();

                foreach (var bot in discovered.OrderBy(x => x.role, StringComparer.Ordinal))
                {
                    if (GroupOverrides.ContainsKey(bot.id))
                    {
                        continue;
                    }

                    var entry = _config.Bind(
                        "8a. Dynamic faction grouping",
                        "Group with - " + ConfigName(bot.role),
                        AutomaticGroup,
                        new ConfigDescription(
                            $"Automatic uses {Roles[bot.id]}. Pick another faction to share its colour, or Maps Expanded default to keep the normal marker colour.",
                            new AcceptableValueList<string>(choices)));
                    GroupOverrides[bot.id] = entry;
                    entry.SettingChanged += SettingsChanged;
                }

                _registryLoaded = true;
                Plugin.Log?.LogInfo($"[MapsExpanded] Discovered {discovered.Count} custom roles across {Colours.Count} colour groups.");
            }
            catch (Exception ex)
            {
                // Keep retryable on transient failures; permanent API mismatches still warn once.
                Warn("registry", ex);
            }
        }

        private static string ConfigName(string value)
        {
            var result = new StringBuilder();
            foreach (var c in value)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                {
                    result.Append(c);
                }
                else
                {
                    result.Append('~').Append(((int)c).ToString("X4"));
                }
            }

            return result.ToString();
        }

        private static Color GeneratedColour(string role)
        {
            uint hash = 2166136261;
            foreach (var c in role)
            {
                hash = unchecked((hash ^ c) * 16777619);
            }

            return new Color32(
                (byte)(80 + hash % 160),
                (byte)(80 + (hash >> 8) % 160),
                (byte)(80 + (hash >> 16) % 160),
                255);
        }

        internal static string ResolveGroup(int id, string fallback)
        {
            if (GroupOverrides.TryGetValue(id, out var entry))
            {
                if (entry.Value == NativeGroup)
                {
                    return null;
                }

                if (entry.Value != AutomaticGroup && Colours.ContainsKey(entry.Value))
                {
                    return entry.Value;
                }
            }

            return Roles.TryGetValue(id, out var group) ? group : fallback;
        }

        internal static void Warn(string key, Exception ex)
        {
            if (Warnings.Add(key))
            {
                Plugin.Log?.LogWarning($"[MapsExtended:Factions] {key}: {ex.Message}");
            }
        }

        internal static void Shutdown() => FactionMarkerTint.RestoreAll();
    }

    internal sealed class FactionMarkerTint : MonoBehaviour
    {
        private static readonly HashSet<FactionMarkerTint> Active = new();
        private Image _image;
        private Graphic _label;
        private string _faction;
        private Color _native;
        private int _roleId;

        internal void Initialize(Image image, Graphic label, string faction, Color native, int roleId)
        {
            _image = image;
            _label = label;
            _faction = faction;
            _native = native;
            _roleId = roleId;
            Active.Add(this);
            Apply();
        }

        internal void NativeColourChanged(Color colour)
        {
            // Preserve the latest floor/alpha drive from MapMarker; Apply re-applies faction RGB.
            _native = colour;
            Apply();
        }

        internal void Apply(bool restore = false)
        {
            if (_image == null)
            {
                return;
            }

            var group = FactionColours.ResolveGroup(_roleId, _faction);
            ConfigEntry<Color> entry = null;
            var enabled = !restore
                          && FactionColours.Enabled != null
                          && FactionColours.Enabled.Value
                          && group != null
                          && FactionColours.Colours.TryGetValue(group, out entry);

            var colour = enabled ? entry.Value : _native;
            SetRgb(_image, colour);
            if (_label != null)
            {
                SetRgb(_label, colour);
            }
        }

        private static void SetRgb(Graphic graphic, Color colour)
        {
            colour.a = graphic.color.a;
            if (graphic.color != colour)
            {
                graphic.color = colour;
            }
        }

        internal static void RefreshAll()
        {
            Active.RemoveWhere(tint => tint == null);
            foreach (var tint in Active.ToArray())
            {
                try
                {
                    tint.Apply();
                }
                catch (Exception ex)
                {
                    FactionColours.Warn("refresh", ex);
                }
            }
        }

        internal static void RestoreAll()
        {
            Active.RemoveWhere(tint => tint == null);
            foreach (var tint in Active.ToArray())
            {
                try
                {
                    tint.Apply(true);
                }
                catch (Exception ex)
                {
                    FactionColours.Warn("restore", ex);
                }
            }
        }

        private void OnDestroy() => Active.Remove(this);
    }
}
