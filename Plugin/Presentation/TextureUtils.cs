using System;
using System.Collections.Generic;
using System.IO;
using MapsExtended.Config;
using MapsExtended.Core;
using UnityEngine;

namespace MapsExtended.Presentation
{
    public static class TextureUtils
    {
        private static readonly Dictionary<string, Sprite> MarkerSprites = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Sprite> MapSprites = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, MapTexture> MapTextures = new(StringComparer.OrdinalIgnoreCase);

        public static Sprite GetOrLoadMapSprite(MapLayerDef def)
        {
            var extension = Path.GetExtension(def.ImagePath);
            if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return SvgUtils.GetOrLoadCachedSprite(def);
            }

            if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Unsupported map image: {def.ImagePath}");
            }

            if (def.ImagePath == null)
            {
                throw new InvalidOperationException();
            }

            var absolutePath = Path.GetFullPath(Path.Combine(Plugin.Path, def.ImagePath));
            if (MapSprites.TryGetValue(absolutePath, out var sprite))
            {
                return sprite;
            }

            var texture = LoadPaddedMapTexture(absolutePath, out var imageRect);
            if (texture == null)
            {
                throw new InvalidDataException($"Unable to load map image: {absolutePath}");
            }

            sprite = Sprite.Create(texture, imageRect, new Vector2(0.5f, 0.5f), 1f);
            MapSprites[absolutePath] = sprite;
            return sprite;
        }

        public static void ClearMapCache()
        {
            DestroyAll(MapSprites.Values);
            MapSprites.Clear();

            foreach (var cached in MapTextures.Values)
            {
                UnityEngine.Object.Destroy(cached.Texture);
            }

            MapTextures.Clear();
        }

        public static Sprite GetOrLoadCachedSprite(string path)
        {
            var fileName = Path.GetFileName(path);
            if (!Path.GetExtension(fileName).Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Marker images must be PNG files: {path}");
            }

            var defaultPath = Path.Combine(Plugin.Path, "Markers", fileName);
            var pack = Settings.MarkerPack.Value;
            var cachePath = pack == "Default"
                ? defaultPath
                : Path.Combine(Plugin.Path, "Addons", pack, "Markers", fileName);

            if (MarkerSprites.TryGetValue(cachePath, out var sprite))
            {
                return sprite;
            }

            var imagePath = File.Exists(cachePath) ? cachePath : defaultPath;
            if (MarkerSprites.TryGetValue(imagePath, out sprite))
            {
                MarkerSprites[cachePath] = sprite;
                return sprite;
            }

            var texture = LoadRawTexture(imagePath);
            if (texture == null)
            {
                throw new InvalidDataException($"Unable to load marker image: {imagePath}");
            }

            var pivot = new Vector2(texture.width / 2f, texture.height / 2f);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), pivot);
            MarkerSprites[imagePath] = sprite;
            MarkerSprites[cachePath] = sprite;
            return sprite;
        }

        private static Texture2D LoadPaddedMapTexture(string absolutePath, out Rect imageRect)
        {
            imageRect = default;
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                return null;
            }

            if (MapTextures.TryGetValue(absolutePath, out var cached))
            {
                imageRect = cached.ImageRect;
                return cached.Texture;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(absolutePath)))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                imageRect = new Rect(0, 0, texture.width, texture.height);
                texture = PadToBlock(texture);
                texture.Compress(true);
                texture.Apply(false, true);
                MapTextures[absolutePath] = new MapTexture(texture, imageRect);
                return texture;
            }
            catch
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }

                throw;
            }
        }

        private static Texture2D PadToBlock(Texture2D source)
        {
            var width = Align4(source.width);
            var height = Align4(source.height);
            if (width == source.width && height == source.height)
            {
                return source;
            }

            var padded = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                padded.SetPixels(0, 0, source.width, source.height, source.GetPixels());
                padded.Apply();
            }
            catch
            {
                UnityEngine.Object.Destroy(padded);
                throw;
            }

            UnityEngine.Object.Destroy(source);
            return padded;
        }

        private static Texture2D LoadRawTexture(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(absolutePath));
            return texture;
        }

        private static int Align4(int size)
        {
            return (size + 3) & ~3;
        }

        private static void DestroyAll(IEnumerable<Sprite> sprites)
        {
            foreach (var sprite in sprites)
            {
                UnityEngine.Object.Destroy(sprite);
            }
        }

        private readonly struct MapTexture
        {
            public MapTexture(Texture2D texture, Rect imageRect)
            {
                Texture = texture;
                ImageRect = imageRect;
            }

            public Texture2D Texture { get; }
            public Rect ImageRect { get; }
        }
    }
}
