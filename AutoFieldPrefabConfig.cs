using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
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
            Sprite autoFieldIcon = assetBundle.LoadAsset<Sprite>("auto_field_icon");

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
                Icon = autoFieldIcon
            }));
        }

        private static void ConfigureAutoFieldPrefab(GameObject autoFieldPrefab)
        {
            Transform modelRoot = autoFieldPrefab.transform.Find("new");
            GameObject guardStone = PrefabManager.Instance.GetPrefab("guard_stone");

            if (!modelRoot || !guardStone)
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

            Transform guardStoneModel = FindSourceTransform(guardStone, "new/default", "default");
            GameObject model = ClonePart(guardStoneModel, modelRoot, "default", Vector3.zero, Quaternion.identity, Vector3.one * 0.6f);
            if (!model)
            {
                Logger.LogError("Could not prepare the Advanced Seed Totem model: guard_stone/new/default is missing");
                return;
            }

            // These parts are optional decorations. Exact paths cover the current
            // Valheim 1.0 prefabs; name fallbacks keep the model usable across small
            // vanilla hierarchy changes instead of aborting the whole model.
            GameObject spinningWheel = PrefabManager.Instance.GetPrefab("piece_spinning_wheel")
                ?? PrefabManager.Instance.GetPrefab("piece_spinningwheel");
            Transform hopperSource = FindSourceTransform(
                spinningWheel,
                "SpinningWheel_Destruction/SpinningWheel_Destruction.002_SpinningWheel_Broken.018",
                "SpinningWheel_Broken");
            ClonePart(hopperSource, modelRoot, "hopper", new Vector3(0.29f, 1.12f, 1.26f),
                Quaternion.Euler(177.7f, -258.918f, -89.55298f), Vector3.one);

            GameObject artisanStation = PrefabManager.Instance.GetPrefab("piece_artisanstation");
            Transform leftGearSource = FindSourceTransform(
                artisanStation,
                "ArtisanTable_Destruction/ArtisanTable_Destruction.007_ArtisanTable.019",
                "ArtisanTable.007_ArtisanTable.019");
            Transform rightGearSource = FindSourceTransform(
                artisanStation,
                "ArtisanTable_Destruction/ArtisanTable_Destruction.006_ArtisanTable.018",
                "ArtisanTable.006_ArtisanTable.018");

            GameObject leftGear = ClonePart(leftGearSource, modelRoot.Find("pivot_left"), "gear_left",
                new Vector3(-0.383f, 0.8181f, -0.8028001f),
                Quaternion.Euler(0f, -90.00001f, -90.91601f), Vector3.one * 0.68285f);
            GameObject rightGear = ClonePart(rightGearSource, modelRoot.Find("pivot_right"), "gear_right",
                new Vector3(-0.47695f, 0.5057697f, -0.7557001f),
                Quaternion.Euler(0f, -90.00001f, -90.91601f), Vector3.one * 0.68285f);

            SeedTotem seedTotem = autoFieldPrefab.GetComponent<SeedTotem>() ?? autoFieldPrefab.AddComponent<SeedTotem>();
            seedTotem.m_shape = SeedTotem.FieldShape.Rectangle;
            Transform wayEffectSource = guardStone.transform.Find("WayEffect");
            if (wayEffectSource)
            {
                GameObject wayEffect = Object.Instantiate(wayEffectSource.gameObject, autoFieldPrefab.transform);
                wayEffect.name = "WayEffect";
                seedTotem.m_enabledEffect = wayEffect;
            }

            seedTotem.m_model = model.GetComponent<MeshRenderer>() ?? model.GetComponentInChildren<MeshRenderer>(true);
            seedTotem.m_gearLeft = leftGear?.GetComponentInChildren<MeshRenderer>(true);
            seedTotem.m_gearRight = rightGear?.GetComponentInChildren<MeshRenderer>(true);

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

        private static Transform FindSourceTransform(GameObject prefab, string preferredPath, params string[] fallbackNames)
        {
            if (!prefab)
            {
                return null;
            }

            Transform result = prefab.transform.Find(preferredPath);
            if (result)
            {
                return result;
            }

            Transform[] transforms = prefab.GetComponentsInChildren<Transform>(true);
            foreach (string fallbackName in fallbackNames)
            {
                foreach (Transform candidate in transforms)
                {
                    if (candidate.name.IndexOf(fallbackName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return candidate;
                    }
                }
            }

            Logger.LogWarning("Advanced Seed Totem decoration was not found: " + preferredPath);
            return null;
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
