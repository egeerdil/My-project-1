#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LingoRun.EditorTools
{
    /// <summary>
    /// One-time art pipeline: renders real 3D prefabs from the "Low Poly
    /// Atmospheric Locations Pack" (Assets/Palmov Island/...) to flat PNG
    /// sprites, angled and lit consistently, with a transparent background.
    /// MapScreenUI loads the results from Resources/Sprites/Map at runtime
    /// instead of drawing procedural shapes — so the world map uses the
    /// actual purchased art, at zero runtime 3D cost and with no risk of
    /// the huge source meshes/textures bloating the build (only the small
    /// baked PNGs ship).
    ///
    /// Run once via the menu below whenever you add/change which prefabs
    /// this bakes, then re-enter Play mode to see the result on the map.
    /// </summary>
    public static class IslandThumbnailBaker
    {
        const string OutputFolder = "Assets/LingoRun/Resources/Sprites/Map";
        const string BgOutputFolder = "Assets/LingoRun/Resources/Sprites/Backgrounds";
        const int Size = 1024;
        const string PackRoot = "Assets/Palmov Island/Low Poly Atmospheric Locations Pack/Prefabs/";

        struct Target
        {
            public string prefabPath;
            public string outputName;
            public Target(string p, string o) { prefabPath = PackRoot + p; outputName = o; }
        }

        // Add/replace entries here to change what the map uses — MapScreenUI
        // picks islands/props by these output names (see BakedSprite calls).
        static readonly Target[] Targets =
        {
            new Target("Islands/japanes island.prefab", "island_japanese"),
            new Target("Islands/treasure Island.prefab", "island_treasure"),
            new Target("Trees/Palm trees/palm tree small.prefab", "prop_palm_small"),
            new Target("Trees/Palm trees/palm tree large.prefab", "prop_palm_large"),
            new Target("Trees/Palm trees/palm tree bent.prefab", "prop_palm_bent"),
            new Target("Stones/Stones small/stone small.prefab", "prop_stone_small"),
        };

        // The pack's 10 fully-decorated "location with environment" scenes —
        // one per Shop background theme (see GameConfig.Theme.Backgrounds'
        // backdropSprite field, which must match these output names exactly).
        static readonly Target[] BackgroundTargets =
        {
            new Target("Location with environment/autumn atmosphere forest with environment.prefab", "bg_autumn_forest"),
            new Target("Location with environment/camping forest with environment.prefab", "bg_camping_forest"),
            new Target("Location with environment/christmas village with environment.prefab", "bg_christmas_village"),
            new Target("Location with environment/desert pyramids with environment.prefab", "bg_desert_pyramids"),
            new Target("Location with environment/downtown with environment.prefab", "bg_downtown"),
            new Target("Location with environment/japanes island with environment.prefab", "bg_japanese_island"),
            new Target("Location with environment/soccer field with environment.prefab", "bg_soccer_field"),
            new Target("Location with environment/space shuttle launch with environment.prefab", "bg_space_shuttle"),
            new Target("Location with environment/treasure Island with environment.prefab", "bg_treasure_island"),
            new Target("Location with environment/wild west railway station with environment.prefab", "bg_wild_west_station"),
        };

        const string RuntimePrefabFolder = "Assets/LingoRun/Resources/Prefabs";

        struct RuntimeTarget
        {
            public string sourcePath;
            public string outputName;
            public RuntimeTarget(string p, string o) { sourcePath = PackRoot + p; outputName = o; }
        }

        // Prefabs Instantiate'd directly at runtime (as real 3D scenery, not
        // baked to an image) — RunningGame3D.BuildThemedPropScenery loads
        // these by outputName via Resources.Load, one set per background
        // theme that lists them in GameConfig.Theme.BackgroundTheme's
        // sceneryPropPrefabs (must match these output names exactly). Unlike
        // the baked art above, these need to physically live under a
        // Resources folder, because the AssetDatabase API used for baking is
        // Editor-only and unavailable in an actual shipped build.
        static readonly RuntimeTarget[] RuntimeTargets =
        {
            // Rocket Launch
            new RuntimeTarget("Vehicles/space shuttle.prefab", "space_shuttle"),

            // Autumn Forest
            new RuntimeTarget("Trees/Autumn trees/autumn tree.prefab", "autumn_tree"),
            new RuntimeTarget("Trees/Autumn trees/autumn tree 2.prefab", "autumn_tree_2"),
            new RuntimeTarget("Trees/Autumn trees/autumn tree 3.prefab", "autumn_tree_3"),

            // Camping Forest
            new RuntimeTarget("Environment/Camping environment/camping tent blue.prefab", "camping_tent_blue"),
            new RuntimeTarget("Environment/Camping environment/camping tent red.prefab", "camping_tent_red"),
            new RuntimeTarget("Environment/Camping environment/camping tent yellow.prefab", "camping_tent_yellow"),
            new RuntimeTarget("Trees/Fir trees/fir tree medium.prefab", "fir_tree_medium"),

            // Christmas Village
            new RuntimeTarget("Trees/christmas tree.prefab", "christmas_tree"),
            new RuntimeTarget("Houses/Wooden winter houses/wooden winter house.prefab", "wooden_winter_house"),
            new RuntimeTarget("Houses/Wooden winter houses/wooden winter house 2.prefab", "wooden_winter_house_2"),

            // Desert Pyramids
            new RuntimeTarget("Environment/Pyramids/pyramid.prefab", "pyramid"),
            new RuntimeTarget("Environment/Pyramids/pyramid 2.prefab", "pyramid_2"),
            new RuntimeTarget("Plants/Cacti/cactus.prefab", "cactus"),
            new RuntimeTarget("Plants/Cacti/cactus 2.prefab", "cactus_2"),

            // Downtown
            new RuntimeTarget("Houses/Buildings/building.prefab", "building"),
            new RuntimeTarget("Houses/Buildings/building 2.prefab", "building_2"),
            new RuntimeTarget("Houses/Buildings/building 3.prefab", "building_3"),
            new RuntimeTarget("Houses/Buildings/building 4.prefab", "building_4"),

            // Japanese Island
            new RuntimeTarget("Trees/sakura tree.prefab", "sakura_tree"),
            new RuntimeTarget("Houses/japanes temple.prefab", "japanese_temple"),
            new RuntimeTarget("Environment/torii gate.prefab", "torii_gate"),

            // Soccer Field
            new RuntimeTarget("Environment/Sports environment/soccer goal.prefab", "soccer_goal"),
            new RuntimeTarget("Environment/Sports environment/soccer grandstand.prefab", "soccer_grandstand"),
            new RuntimeTarget("Environment/Sports environment/mesh fence.prefab", "mesh_fence"),

            // Treasure Cove
            new RuntimeTarget("Trees/Palm trees/palm tree large.prefab", "palm_tree_large"),
            new RuntimeTarget("Trees/Palm trees/palm tree bent.prefab", "palm_tree_bent"),
            new RuntimeTarget("Environment/wooden pier.prefab", "wooden_pier"),
            new RuntimeTarget("Environment/chest.prefab", "chest"),

            // Wild West
            new RuntimeTarget("Houses/railway station.prefab", "railway_station"),
            new RuntimeTarget("Environment/hitching post.prefab", "hitching_post"),
            new RuntimeTarget("Environment/wooden post.prefab", "wooden_post"),

            // Player "ball" vehicle shapes (RunningGame3D.BuildVehicleShapes,
            // Ball Shop) — the same real pack models, reused as selectable
            // player shapes instead of a plain sphere/cube/etc. space_shuttle
            // is already copied above (Rocket Launch scenery) and reused here.
            new RuntimeTarget("Vehicles/sedan car brown.prefab", "vehicle_sedan"),
            new RuntimeTarget("Vehicles/car SUV large yellow.prefab", "vehicle_suv_yellow"),
            new RuntimeTarget("Vehicles/car SUVs pickups lights red.prefab", "vehicle_suv_red"),
            new RuntimeTarget("Vehicles/cart.prefab", "vehicle_cart"),
            new RuntimeTarget("Vehicles/boat.prefab", "vehicle_boat"),
            new RuntimeTarget("Vehicles/japanese boat.prefab", "vehicle_japanese_boat"),
            new RuntimeTarget("Vehicles/destroyed ship.prefab", "vehicle_destroyed_ship"),
            new RuntimeTarget("Vehicles/freight wagon black.prefab", "vehicle_freight_wagon"),
            new RuntimeTarget("Vehicles/passenger wagon.prefab", "vehicle_passenger_wagon"),
            new RuntimeTarget("Vehicles/train.prefab", "vehicle_train"),
        };

        [MenuItem("LingoRun/Bake Map Island Thumbnails")]
        public static void BakeAll()
        {
            // The pack's own material is authored for the Built-in Render
            // Pipeline; this project runs URP, so it renders flat magenta
            // until converted — do that first so the bake actually captures
            // real textures instead of pink placeholders.
            int converted = ConvertStandardMaterialsToUrp();
            if (converted > 0) AssetDatabase.SaveAssets();

            int done = BakeTargets(Targets, OutputFolder, 0.78f);

            AssetDatabase.Refresh();
            Debug.Log("IslandThumbnailBaker: baked " + done + "/" + Targets.Length +
                " images into " + OutputFolder + ". Enter Play mode and open the Map to see them.");
        }

        /// <summary>
        /// Bakes the 10 "location with environment" scenes used by the Shop's
        /// background themes (GameConfig.Theme.Backgrounds, indices 3-12).
        /// Run this once (or again if you change BackgroundTargets), then
        /// open the Shop's BG tab or start a run to see the results.
        /// </summary>
        [MenuItem("LingoRun/Bake Shop Backgrounds")]
        public static void BakeBackgrounds()
        {
            int converted = ConvertStandardMaterialsToUrp();
            if (converted > 0) AssetDatabase.SaveAssets();

            // A bit more headroom than the small island icons — these are
            // meant to read as a whole scene, not a tight close-up.
            int done = BakeTargets(BackgroundTargets, BgOutputFolder, 0.9f);

            AssetDatabase.Refresh();
            Debug.Log("IslandThumbnailBaker: baked " + done + "/" + BackgroundTargets.Length +
                " backgrounds into " + BgOutputFolder + ". Open the Shop's BG tab (or start a run) to see them.");
        }

        /// <summary>
        /// Copies RuntimeTargets into a Resources folder so they can be
        /// Instantiate'd at runtime — real space shuttles, buildings,
        /// pyramids, tents, etc. for each background theme's near-track
        /// scenery, instead of a hand-built/recolored primitive stand-in.
        /// Safe to run more than once — already-copied prefabs are skipped.
        /// Unity assigns each copy a fresh GUID automatically, so this never
        /// collides with the pack's original asset.
        /// </summary>
        [MenuItem("LingoRun/Copy Runtime Scenery Prefabs")]
        public static void CopyRuntimePrefabs()
        {
            int converted = ConvertStandardMaterialsToUrp();
            if (converted > 0) AssetDatabase.SaveAssets();

            Directory.CreateDirectory(RuntimePrefabFolder);

            int done = 0;
            foreach (var target in RuntimeTargets)
            {
                string destPath = RuntimePrefabFolder + "/" + target.outputName + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(destPath) != null)
                {
                    done++; // already copied from a previous run
                    continue;
                }
                if (AssetDatabase.LoadAssetAtPath<GameObject>(target.sourcePath) == null)
                {
                    Debug.LogWarning("IslandThumbnailBaker: prefab not found at " + target.sourcePath +
                        " — skipping " + target.outputName + ". Did the asset's folder names change?");
                    continue;
                }
                if (AssetDatabase.CopyAsset(target.sourcePath, destPath)) done++;
                else Debug.LogWarning("IslandThumbnailBaker: failed to copy " + target.sourcePath + " to " + destPath);
            }

            AssetDatabase.Refresh();
            Debug.Log("IslandThumbnailBaker: " + done + "/" + RuntimeTargets.Length +
                " runtime prefab(s) ready in " + RuntimePrefabFolder + ". Enter Play mode to see them.");
        }

        static int BakeTargets(Target[] targets, string outputFolder, float marginScale)
        {
            Directory.CreateDirectory(outputFolder);

            int done = 0;
            foreach (var target in targets)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(target.prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning("IslandThumbnailBaker: prefab not found at " + target.prefabPath +
                        " — skipping " + target.outputName + ". Did the asset's folder names change?");
                    continue;
                }
                Bake(prefab, target.outputName, outputFolder, marginScale);
                done++;
            }
            return done;
        }

        /// <summary>
        /// Standalone version of the material fix, in case you want to drop
        /// this pack's prefabs into an actual 3D scene later — this fixes
        /// the pink material everywhere it's used, not just for baking.
        /// </summary>
        [MenuItem("LingoRun/Fix Island Pack Materials (URP)")]
        public static void FixMaterialsForUrp()
        {
            int converted = ConvertStandardMaterialsToUrp();
            if (converted > 0) AssetDatabase.SaveAssets();
            Debug.Log("IslandThumbnailBaker: converted " + converted +
                " material(s) from Built-in Standard to Universal Render Pipeline/Lit.");
        }

        /// <summary>
        /// The pack ships one shared material ("mat main") using Unity's
        /// Built-in Standard shader. Under URP that shader isn't supported
        /// and every prefab using it renders flat magenta — exactly the pink
        /// islands you saw. This finds every material like that anywhere
        /// under the pack and switches it to Universal Render Pipeline/Lit,
        /// carrying the main texture and tint color across (_MainTex/_Color
        /// -> _BaseMap/_BaseColor, the renamed equivalents in URP/Lit).
        /// Uses Shader.Find by name rather than a hardcoded shader GUID, so
        /// it keeps working across URP package version bumps.
        /// </summary>
        static int ConvertStandardMaterialsToUrp()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogWarning("IslandThumbnailBaker: couldn't find the Universal Render Pipeline/Lit " +
                    "shader — is the URP package installed on this project?");
                return 0;
            }

            int count = 0;
            var guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Palmov Island" });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;
                if (mat.shader.name == urpLit.name) continue; // already converted

                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Color mainColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

                mat.shader = urpLit;
                if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                mat.SetColor("_BaseColor", mainColor);
                EditorUtility.SetDirty(mat);
                count++;
            }
            return count;
        }

        static void Bake(GameObject prefab, string outputName, string outputFolder, float marginScale)
        {
            // Everything below happens in a throwaway root parked far outside
            // normal world space so nothing already open in the editor scene
            // (or the prefab's own absolute position) ends up in frame.
            var root = new GameObject("__IslandBakeRoot_" + outputName);
            root.transform.position = new Vector3(9000f, 9000f, 9000f);

            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                instance.transform.localPosition = Vector3.zero;

                Bounds bounds = ComputeBounds(instance);
                Vector3 center = bounds.center;
                float radius = Mathf.Max(0.4f, bounds.extents.magnitude);

                var lightGo = new GameObject("__BakeLight");
                lightGo.transform.SetParent(root.transform, false);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.color = new Color(1f, 0.98f, 0.92f);
                lightGo.transform.rotation = Quaternion.Euler(46f, -35f, 0f);

                var fillGo = new GameObject("__BakeFill");
                fillGo.transform.SetParent(root.transform, false);
                var fill = fillGo.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0.35f;
                fill.color = new Color(0.75f, 0.85f, 1f);
                fillGo.transform.rotation = Quaternion.Euler(-30f, 140f, 0f);

                var camGo = new GameObject("__BakeCamera");
                camGo.transform.SetParent(root.transform, false);
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent PNG
                cam.orthographic = true;
                cam.orthographicSize = radius * marginScale; // fit with a little margin
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = radius * 6f + 10f;
                cam.allowHDR = false;
                cam.allowMSAA = false; // avoid platform-dependent MSAA sample-count mismatches

                // A gentle angled look-down (not straight top-down) so the
                // shape still reads as "3D" the way the rest of the map does.
                Vector3 dir = (Quaternion.Euler(34f, -35f, 0f) * Vector3.forward).normalized;
                camGo.transform.position = center - dir * (radius * 3f + 5f);
                camGo.transform.LookAt(center);

                // No MSAA on the render target either — Size is already high
                // enough (1024) that edges look clean once scaled down for UI,
                // and this sidesteps "N samples requested but M supported"
                // warnings/blank captures on some GPUs.
                var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
                rt.antiAliasing = 1;
                cam.targetTexture = rt;

                var prevActive = RenderTexture.active;
                RenderTexture.active = rt;
                cam.Render();

                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();

                RenderTexture.active = prevActive;
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);

                var bytes = tex.EncodeToPNG();
                string path = outputFolder + "/" + outputName + ".png";
                File.WriteAllBytes(path, bytes);
                Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static Bounds ComputeBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
#endif
