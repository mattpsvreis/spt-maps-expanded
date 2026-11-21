using System.Diagnostics;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using MapsExtended.Config;
using UnityEngine;

namespace MapsExtended.App
{
    internal static class GameBuild
    {
        internal static bool Matches(ManualLogSource log, ConfigFile config)
        {
            var running = FileVersionInfo.GetVersionInfo(BepInEx.Paths.ExecutablePath).FilePrivatePart;
            if (running == Plugin.TarkovVersion)
            {
                return true;
            }

            var message = $"Maps Expanded {BuildInfo.Version} was built for Tarkov {Plugin.TarkovVersion}. This game is build {running}.";
            log.LogError(message);
            Chainloader.DependencyErrors.Add(message);
            config.Bind("", "TarkovVersion", "", new ConfigDescription(
                message,
                null,
                new ConfigurationManagerAttributes
                {
                    CustomDrawer = DrawMismatch,
                    ReadOnly = true,
                    HideDefaultButton = true,
                    HideSettingName = true,
                    Category = null
                }));
            return false;
        }

        private static void DrawMismatch(ConfigEntryBase entry)
        {
            var body = new GUIStyle(GUI.skin.label) { wordWrap = true, stretchWidth = true };
            var title = new GUIStyle(GUI.skin.label)
            {
                stretchWidth = true,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            title.normal.textColor = Color.red;
            GUILayout.BeginVertical();
            GUILayout.Label(entry.Description.Description, body, GUILayout.ExpandWidth(true));
            GUILayout.Label("Maps Expanded is not running.", title, GUILayout.ExpandWidth(true));
            GUILayout.EndVertical();
        }
    }
}
