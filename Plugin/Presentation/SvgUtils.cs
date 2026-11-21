using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using MapsExtended.Core;
using Unity.VectorGraphics;
using UnityEngine;

namespace MapsExtended.Presentation
{
    public static class SvgUtils
    {
        private const int VertexBudget = 65500;
        private const float SvgPixelsPerUnit = 1f;
        private const int GradientResolution = 32;

        private static readonly Dictionary<(string Path, int Quality), Sprite> Sprites = new();
        private static readonly Regex ViewBoxPattern = new(@"<svg[^>]*\sviewBox=""([^""]+)""", RegexOptions.Compiled);

        private static readonly VectorUtils.TessellationOptions[] QualityPresets = BuildQualityPresets();

        public static Sprite GetOrLoadCachedSprite(MapLayerDef def)
        {
            var key = (def.ImagePath, def.TesselationIndex);
            if (Sprites.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var absolutePath = Path.Combine(Plugin.Path, def.ImagePath);
            var built = BuildSprite(def, absolutePath);
            Sprites[key] = built;
            return built;
        }

        private static Sprite BuildSprite(MapLayerDef def, string absolutePath)
        {
            var svg = File.ReadAllText(absolutePath);
            if (!TryReadViewBox(svg, out var viewBox))
            {
                return null;
            }

            var scene = ImportScene(svg);
            var firstPreset = Mathf.Clamp(def.TesselationIndex, 0, QualityPresets.Length - 1);
            for (var preset = firstPreset; preset < QualityPresets.Length; preset++)
            {
                var geometry = VectorUtils.TessellateScene(scene.Scene, QualityPresets[preset], scene.NodeOpacity);
                if (ExceedsVertexBudget(geometry))
                {
                    Plugin.Log.LogWarning($"Preset {preset} over budget for {absolutePath}, trying next.");
                    continue;
                }

                return VectorUtils.BuildSprite(
                    geometry,
                    viewBox,
                    SvgPixelsPerUnit,
                    VectorUtils.Alignment.Center,
                    Vector2.zero,
                    GradientResolution,
                    flipYAxis: true);
            }

            return null;
        }

        private static SVGParser.SceneInfo ImportScene(string svg)
        {
            using var reader = new StringReader(svg);
            return SVGParser.ImportSVG(
                reader,
                ViewportOptions.OnlyApplyRootViewBox,
                dpi: 0,
                pixelsPerUnit: SvgPixelsPerUnit,
                windowWidth: 0,
                windowHeight: 0);
        }

        private static bool ExceedsVertexBudget(List<VectorUtils.Geometry> geometry)
        {
            var count = 0;
            foreach (var shape in geometry)
            {
                count += shape.Vertices?.Length ?? 0;
                if (count > VertexBudget)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadViewBox(string svg, out Rect viewBox)
        {
            viewBox = default;
            var match = ViewBoxPattern.Match(svg);
            if (!match.Success)
            {
                return false;
            }

            var parts = match.Groups[1].Value.Split(new[] { ' ', '\t', '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 4)
            {
                return false;
            }

            if (!TryParse(parts[0], out var x) ||
                !TryParse(parts[1], out var y) ||
                !TryParse(parts[2], out var width) ||
                !TryParse(parts[3], out var height) ||
                width <= 0f ||
                height <= 0f)
            {
                return false;
            }

            viewBox = new Rect(x, y, width, height);
            return true;
        }

        private static bool TryParse(string token, out float value)
        {
            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static VectorUtils.TessellationOptions[] BuildQualityPresets()
        {
            return new[]
            {
                Preset(1.5f, 0.2f, 0.2f, 0.04f),
                Preset(2f, 0.3f, 0.25f, 0.05f),
                Preset(4f, 0.4f, 0.3f, 0.06f),
                Preset(6f, 0.5f, 0.4f, 0.07f),
                Preset(8f, 0.6f, 0.5f, 0.08f),
            };
        }

        private static VectorUtils.TessellationOptions Preset(float step, float cord, float tangent, float sample)
        {
            return new VectorUtils.TessellationOptions
            {
                StepDistance = step,
                MaxCordDeviation = cord,
                MaxTanAngleDeviation = tangent,
                SamplingStepSize = sample,
            };
        }
    }
}
