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
            GameObject autoFieldSkeleton = assetBundle.LoadAsset<GameObject>(prefabName);
            Sprite advancedIcon = LoadAdvancedIcon(assetBundle.LoadAsset<Sprite>("seed_totem_icon"));

            // Build the model synchronously while the vanilla prefabs are available.
            // Jötunn's kitbash pass is intentionally asynchronous relative to piece
            // registration; registering the unmodified skeleton first leaves Valheim
            // with only its placeholder meshes when the pass fails or runs late.
            ConfigureAutoFieldPrefab(autoFieldSkeleton);

            PieceManager.Instance.AddPiece(new CustomPiece(autoFieldSkeleton, true, new PieceConfig
            {
                PieceTable = "Hammer",
                CraftingStation = "piece_artisanstation",
                Requirements = ParseRequirements(),
                Icon = advancedIcon
            }));
        }

        private static void ConfigureAutoFieldPrefab(GameObject autoFieldPrefab)
        {
            GameObject guardStone = PrefabManager.Instance.GetPrefab("guard_stone");

            if (!guardStone)
            {
                Logger.LogError("Could not prepare the Advanced Seed Totem model: guard_stone or new is missing");
                return;
            }

            // The embedded prefab contains a small placeholder cube. Disable every
            // renderer from the skeleton before attaching the real vanilla meshes so
            // the placeholder cannot survive into the placed piece.
            foreach (Renderer renderer in autoFieldPrefab.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }

            GameObject normalTotem = PrefabManager.Instance.GetPrefab(SeedTotemPrefabConfig.prefabName);
            Transform normalModelRoot = normalTotem ? normalTotem.transform.Find("new") : null;
            normalModelRoot = normalModelRoot ?? guardStone.transform.Find("new");
            GameObject model = ClonePart(normalModelRoot, autoFieldPrefab.transform, "SeedTotemModel", Vector3.zero, Quaternion.identity, Vector3.one);
            if (!model)
            {
                Logger.LogError("Could not prepare the Advanced Seed Totem model: normal Seed Totem new hierarchy is missing");
                return;
            }

            SeedTotem seedTotem = autoFieldPrefab.GetComponent<SeedTotem>() ?? autoFieldPrefab.AddComponent<SeedTotem>();
            seedTotem.m_shape = SeedTotem.FieldShape.Rectangle;
            seedTotem.m_pinkGlow = true;
            Transform wayEffectSource = guardStone.transform.Find("WayEffect");
            if (wayEffectSource)
            {
                GameObject wayEffect = Object.Instantiate(wayEffectSource.gameObject, autoFieldPrefab.transform);
                wayEffect.name = "WayEffect";
                seedTotem.m_enabledEffect = wayEffect;
            }

            seedTotem.m_model = model.GetComponent<MeshRenderer>() ?? model.GetComponentInChildren<MeshRenderer>(true);
            Animator animator = autoFieldPrefab.GetComponent<Animator>();
            if (animator)
            {
                animator.enabled = false;
            }

            Transform areaMarker = autoFieldPrefab.transform.Find("AreaMarker");
            if (areaMarker)
            {
                RectangleProjector rectangleProjector = areaMarker.GetComponent<RectangleProjector>() ?? areaMarker.gameObject.AddComponent<RectangleProjector>();
                seedTotem.m_rectangleProjector = rectangleProjector;
            }

            SetLayerRecursively(autoFieldPrefab, LayerMask.NameToLayer("piece"));
            if (seedTotem.m_model && seedTotem.m_enabledEffect)
            {
                seedTotem.UpdateVisuals();
            }
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
