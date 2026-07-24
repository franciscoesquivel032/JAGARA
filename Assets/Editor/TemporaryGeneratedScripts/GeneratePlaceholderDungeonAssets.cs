using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Jagara.Editor.TemporaryGeneratedScripts
{
    public static class GeneratePlaceholderDungeonAssets
    {
        private const string TextureFolder = "Assets/Art/Placeholders/Tiles";
        private const string TileAssetFolder = "Assets/ScriptableObjects/Tiles";
        private const int TextureSize = 64;

        private struct TileSpec
        {
            public string Name;
            public Color32 Color;

            public TileSpec(string name, Color32 color)
            {
                Name = name;
                Color = color;
            }
        }

        [MenuItem("Tools/Jāgara/TEMP - Generate Placeholder Dungeon Assets")]
        private static void Generate()
        {
            Directory.CreateDirectory(TextureFolder);
            Directory.CreateDirectory(TileAssetFolder);

            var specs = new[]
            {
                new TileSpec("Wall", new Color32(40, 40, 40, 255)),
                new TileSpec("Floor", new Color32(200, 200, 200, 255)),
                new TileSpec("Corridor", new Color32(150, 130, 90, 255)),
                new TileSpec("StairsDown", new Color32(255, 215, 0, 255)),
            };

            foreach (var spec in specs)
            {
                CreateTileAsset(spec.Name, spec.Color);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Generated placeholder dungeon tile assets under " + TileAssetFolder);
        }

        private static void CreateTileAsset(string name, Color32 color)
        {
            string texturePath = $"{TextureFolder}/Tile_{name}.png";
            WritePng(texturePath, color);

            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.spritePixelsPerUnit = TextureSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.None;

            string tileAssetPath = $"{TileAssetFolder}/Tile_{name}.asset";
            AssetDatabase.CreateAsset(tile, tileAssetPath);
        }

        private static void WritePng(string path, Color32 color)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[TextureSize * TextureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
