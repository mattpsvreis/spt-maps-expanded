using System;
using System.Collections.Generic;
using EFT.Interactive;
using EFT.Interactive.SecretExfiltrations;
using MapsExtended.Integration;

namespace MapsExtended.Markers
{
    internal static class ExtractRequirementText
    {
        public static string Describe(ExfiltrationPoint extract)
        {
            if (extract == null)
            {
                return "";
            }

            switch (extract.Status)
            {
                case EExfiltrationStatus.UncompleteRequirements:
                case EExfiltrationStatus.Hidden:
                case EExfiltrationStatus.Pending:
                    break;
                default:
                    return "";
            }

            if (extract is SecretExfiltrationPoint secret)
            {
                return DescribeSecret(secret);
            }

            var player = GameUtils.GetMainPlayer();
            if (player == null)
            {
                return "";
            }

            string[] tips;
            try
            {
                tips = extract.GetTips(player.ProfileId);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Extract requirement failed for {extract.Settings?.Name}: {ex.Message}");
                return "";
            }

            return Join(tips);
        }

        private static string DescribeSecret(SecretExfiltrationPoint secret)
        {
            var id = secret.SecretTransferItemRequirement != null
                ? secret.SecretTransferItemRequirement.Id
                : secret.Settings?.Id;
            var name = ItemName(id);
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }

            var format = "EXFIL_Item".BsgLocalized();
            if (string.IsNullOrEmpty(format) || format.IndexOf("{0}", StringComparison.Ordinal) < 0)
            {
                return name;
            }

            return string.Format(format, name);
        }

        private static string ItemName(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return "";
            }

            var key = id + " Name";
            var name = key.BsgLocalized();
            if (string.IsNullOrEmpty(name) || name == key)
            {
                return "";
            }

            return name;
        }

        private static string Join(string[] tips)
        {
            if (tips == null || tips.Length == 0)
            {
                return "";
            }

            var lines = new List<string>();
            foreach (var tip in tips)
            {
                if (string.IsNullOrWhiteSpace(tip))
                {
                    continue;
                }

                var trimmed = tip.Trim();
                if (trimmed.EndsWith(" (0)"))
                {
                    trimmed = trimmed.Substring(0, trimmed.Length - 4).TrimEnd();
                }

                if (trimmed.Length == 0 || !HasLetter(trimmed) || lines.Contains(trimmed))
                {
                    continue;
                }

                lines.Add(trimmed);
            }

            return string.Join("\n", lines);
        }

        private static bool HasLetter(string text)
        {
            foreach (var c in text)
            {
                if (char.IsLetter(c))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
