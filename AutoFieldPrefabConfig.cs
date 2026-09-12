using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using SeedTotem.Utils;
using System;
using System.IO;
using UnityEngine;
using static SeedTotem.SeedTotemMod;
using Logger = Jotunn.Logger;
using Object = UnityEngine.Object;

namespace SeedTotem
{
    internal class AutoFieldPrefabConfig
    {
        private const string localizationName = "seed_totem";
        public const string ravenTopic = "$tutorial_" + localizationName + "_topic";
        public const string ravenText = "$tutorial_" + localizationName + "_text";
        public const string ravenLabel = "$tutorial_" + localizationName + "_label";
        public const string requirementsFile = "seed-totem-custom-requirements.json";
        public const string prefabName = "piece_seed_totem_auto_field";
        internal static ConfigEntry<PieceLocation> configLocation; 
        internal static ConfigEntry<String> configRecipe;

        private GameObject currentPiece;

        public AutoFieldPrefabConfig()
        {
        }

        private static RequirementConfig[] ParseRequirements()
        {
            string[] entries = configRecipe.Value.Split(',');
            RequirementConfig[] result = new RequirementConfig[entries.Length];
            int i = 0;
            foreach (string pair in entries)
            {
                string[] components = pair.Split(':');
                result[i++] = new RequirementConfig()
                {
                    Item = components[0],
                    Amount = int.Parse(components[1]),
                    Recover = true
                };
            }
            return result;
        }

        public void UpdateCopiedPrefab(AssetBundle assetBundle)
        {
            Sprite advancedIcon = LoadAdvancedIcon(assetBundle.LoadAsset<Sprite>("seed_totem_icon"));

            // Start from the already-working normal Seed Totem prefab. This keeps the
            // complete vanilla model, renderer hierarchy, colliders and interaction
            // wiring intact. The old approach started from the bundle placeholder and
            // tried to copy only part of the model, which could produce an invisible
            // placed piece even though the prefab registered successfully.
            GameObject autoFieldPrefab = PrefabManager.Instance.CreateClonedPrefab(prefabName, SeedTotemPrefabConfig.prefabName);
            ConfigureAutoFieldPrefab(autoFieldPrefab, assetBundle);

            PieceManager.Instance.AddPiece(new CustomPiece(autoFieldPrefab, true, new PieceConfig
            {
                PieceTable = "Hammer",
                CraftingStation = "piece_artisanstation",
                Requirements = ParseRequirements(),
                Icon = advancedIcon
            }));
        }

        private static void ConfigureAutoFieldPrefab(GameObject autoFieldPrefab, AssetBundle assetBundle)
        {
            if (!autoFieldPrefab)
            {
                Logger.LogError("Could not prepare the Advanced Seed Totem: normal Seed Totem clone was not created");
                return;
            }

            SeedTotem seedTotem = autoFieldPrefab.GetComponent<SeedTotem>() ?? autoFieldPrefab.AddComponent<SeedTotem>();
            seedTotem.m_shape = SeedTotem.FieldShape.Rectangle;
            seedTotem.m_pinkGlow = true;

            Piece piece = autoFieldPrefab.GetComponent<Piece>();
            if (piece)
            {
                piece.m_name = "$piece_seed_totem_auto_field_name";
                piece.m_description = "$piece_seed_totem_auto_field_description";
            }
            foreach (GuidePoint guidePoint in autoFieldPrefab.GetComponentsInChildren<GuidePoint>(true))
            {
                guidePoint.m_text.m_key = "auto_field";
                guidePoint.m_text.m_topic = "$tutorial_auto_field_topic";
                guidePoint.m_text.m_text = "$tutorial_auto_field_text";
                guidePoint.m_text.m_label = "$tutorial_auto_field_label";
            }

            // Replace the normal circular marker with the rectangle marker from the
            // original advanced prefab. The normal Seed Totem model is left untouched.
            Transform oldMarker = autoFieldPrefab.transform.Find("AreaMarker");
            if (oldMarker)
            {
                Object.DestroyImmediate(oldMarker.gameObject);
            }

            GameObject advancedSkeleton = assetBundle.LoadAsset<GameObject>(prefabName);
            Transform markerSource = advancedSkeleton ? advancedSkeleton.transform.Find("AreaMarker") : null;
            if (!markerSource)
            {
                Logger.LogError("Could not prepare the Advanced Seed Totem rectangle marker: AreaMarker is missing");
                return;
            }

            GameObject marker = Object.Instantiate(markerSource.gameObject, autoFieldPrefab.transform);
            marker.name = "AreaMarker";
            RectangleProjector rectangleProjector = marker.GetComponent<RectangleProjector>() ?? marker.AddComponent<RectangleProjector>();
            seedTotem.m_rectangleProjector = rectangleProjector;
            seedTotem.m_model = autoFieldPrefab.transform.Find("new/default")?.GetComponent<MeshRenderer>();
            SetLayerRecursively(marker, LayerMask.NameToLayer("piece"));
            Logger.LogInfo("Advanced Seed Totem uses normal model: " + (seedTotem.m_model ? "renderer found" : "renderer missing"));
        }

        private static Sprite LoadAdvancedIcon(Sprite fallback)
        {
            string path = SeedTotemMod.GetAssetPath("Icons/advanced_seed_totem_icon.png");
            if (path != null)
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                    if (ImageConversion.LoadImage(texture, bytes, true))
                    {
                        texture.name = "advanced_seed_totem_icon";
                        texture.filterMode = FilterMode.Bilinear;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 64f);
                    }
                }
                catch (Exception ex) { Logger.LogWarning("Could not load the Advanced Seed Totem icon: " + ex.Message); }
            }
            return fallback;
        }

        private static GameObject ClonePart(Transform source, Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!source || !parent)
            {
                return null;
            }

            GameObject part = Object.Instantiate(source.gameObject, parent);
            part.name = name;
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            return part;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (!root || layer < 0)
            {
                return;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        internal void UpdatePieceLocation()
        {
            Logger.LogDebug("Moving Seed totem to " + configLocation.Value);
            foreach (PieceLocation location in Enum.GetValues(typeof(PieceLocation)))
            {
                currentPiece = RemovePieceFromPieceTable(location, prefabName);
                if (currentPiece != null)
                {
                    break;
                }
            }
            if (configLocation.Value == PieceLocation.Cultivator)
            {
                GetPieceTable(configLocation.Value).m_pieces.Insert(2, currentPiece);
            }
            else
            {
                GetPieceTable(configLocation.Value).m_pieces.Add(currentPiece);
            }
        }

        private PieceTable GetPieceTable(PieceLocation location)
        {
            string pieceTableName = $"_{location}PieceTable";
            Object[] array = Resources.FindObjectsOfTypeAll(typeof(PieceTable));
            for (int i = 0; i < array.Length; i++)
            {
                PieceTable pieceTable = (PieceTable)array[i];
                string name = pieceTable.gameObject.name;
                if (pieceTableName == name)
                {
                    return pieceTable;
                }
            }
            return null;
        }

        private GameObject RemovePieceFromPieceTable(PieceLocation location, string pieceName)
        {
            Logger.LogDebug("Removing " + pieceName + " from " + location);
            PieceTable pieceTable = GetPieceTable(location);
            int currentPosition = pieceTable.m_pieces.FindIndex(piece => piece.name == pieceName);
            if (currentPosition >= 0)
            {
                Logger.LogDebug("Found Piece " + pieceName + " at position " + currentPosition);
                GameObject @object = pieceTable.m_pieces[currentPosition];
                pieceTable.m_pieces.RemoveAt(currentPosition);
                return @object;
            }

            return null;
        }
    }
}
