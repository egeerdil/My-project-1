using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LingoRun
{
    /// <summary>
    /// Real 3D shop preview: a hidden rig far below the world (camera +
    /// light + one model) rendering into a RenderTexture that the Shop's
    /// podium shows through a RawImage — so a "space shuttle" preview is the
    /// actual space shuttle model slowly turning, not an abstract 2D emblem.
    /// Also bakes one static 3/4-view thumbnail sprite per vehicle for the
    /// catalogue cards. Primitive shapes are rebuilt here with the exact
    /// same proportions as RunningGame3D's player shapes.
    /// </summary>
    public static class ShapePreview3D
    {
        const float RigY = -800f;
        const int RtWidth = 512, RtHeight = 512;
        const int ThumbSize = 128;

        static GameObject _rigRoot;
        static Camera _cam;
        static RenderTexture _rt;
        static Transform _modelAnchor;
        static GameObject _model;
        static GameObject _ground;
        static GameObject _backdrop;
        static GameObject _propsRoot;      // the theme's scenery prefabs on stage
        static string _propsTheme = null;  // which theme's props are built
        static Material _skinMat, _whiteMat, _groundMat, _backdropMat;
        static readonly Dictionary<string, Sprite> _backdropCache = new Dictionary<string, Sprite>();
        static int _builtShape = -1;
        static readonly Dictionary<int, Sprite> _vehicleThumbs = new Dictionary<int, Sprite>();

        class Spinner : MonoBehaviour
        {
            void Update() { transform.Rotate(0f, 42f * Time.deltaTime, 0f, Space.World); }
        }

        public static Texture LiveTexture { get { EnsureRig(); return _rt; } }

        static void EnsureRig()
        {
            if (_rigRoot != null) return;

            _rigRoot = new GameObject("ShopPreviewRig");
            Object.DontDestroyOnLoad(_rigRoot);
            _rigRoot.transform.position = new Vector3(0f, RigY, 0f);

            _rt = new RenderTexture(RtWidth, RtHeight, 16, RenderTextureFormat.ARGB32);

            var camGo = new GameObject("PreviewCam");
            camGo.transform.SetParent(_rigRoot.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.25f, -3.5f);
            camGo.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
            _cam = camGo.AddComponent<Camera>();
            _cam.fieldOfView = 38f; // wide enough that stage props read as scenery
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 20f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _cam.targetTexture = _rt;

            // The pack vehicles use lit materials — give them a light.
            var lightGo = new GameObject("PreviewLight");
            lightGo.transform.SetParent(_rigRoot.transform, false);
            lightGo.transform.localRotation = Quaternion.Euler(42f, -28f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;

            var anchorGo = new GameObject("ModelAnchor");
            anchorGo.transform.SetParent(_rigRoot.transform, false);
            anchorGo.transform.localPosition = Vector3.zero;
            _modelAnchor = anchorGo.transform;
            anchorGo.AddComponent<Spinner>();

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            _skinMat = new Material(unlit);
            _whiteMat = new Material(unlit);
            _whiteMat.SetColor("_BaseColor", Color.white);
            _whiteMat.color = Color.white;
            _groundMat = new Material(unlit);

            // Little stage floor: the previewed background theme's ground
            // color, with the camera clearing to its sky color — an opaque
            // mini-scene instead of relying on RT transparency (URP doesn't
            // preserve alpha reliably, which showed as an ugly black box).
            _ground = Prim(PrimitiveType.Cube, _rigRoot.transform,
                new Vector3(0f, -0.05f, 2f), new Vector3(16f, 0.1f, 16f),
                Quaternion.identity, _groundMat);

            // Backdrop quad far behind the model — shows the theme's baked
            // scenery image (Japanese island, space shuttle, …), same asset
            // the run itself uses (Resources/Sprites/Backgrounds).
            _backdropMat = new Material(unlit);
            _backdropMat.SetColor("_BaseColor", Color.white);
            _backdropMat.color = Color.white;
            if (_backdropMat.HasProperty("_Cull")) _backdropMat.SetFloat("_Cull", 0f);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(quad.GetComponent<Collider>());
            quad.name = "Backdrop";
            quad.transform.SetParent(_rigRoot.transform, false);
            quad.transform.localPosition = new Vector3(0f, 2.6f, 9f);
            quad.transform.localScale = new Vector3(15f, 10f, 1f);
            var qr = quad.GetComponent<Renderer>();
            qr.sharedMaterial = _backdropMat;
            qr.shadowCastingMode = ShadowCastingMode.Off;
            _backdrop = quad;

            _rigRoot.SetActive(false);
        }

        /// <summary>Show the given shape+color live on the podium texture,
        /// staged against the given background theme's sky + ground colors.
        /// Returns false when the model isn't available (missing prefab) —
        /// caller falls back to the 2D emblem.</summary>
        public static bool ShowLive(int shapeIdx, Color skin, Theme.BackgroundTheme stage)
        {
            EnsureRig();
            _cam.backgroundColor = stage.skyTint;
            _groundMat.SetColor("_BaseColor", stage.groundColor);
            _groundMat.color = stage.groundColor;
            _ground.SetActive(true);

            // Theme's baked backdrop image behind the stage (when it has one).
            Sprite bd = LoadBackdrop(stage.backdropSprite);
            _backdrop.SetActive(bd != null);
            if (bd != null) _backdropMat.SetTexture("_BaseMap", bd.texture);

            // The theme's real scenery props ON the stage — this is what makes
            // "Japanese Island" actually show its pagoda/palms in the popup,
            // exactly the prefabs the run places along the track.
            BuildStageProps(stage);
            if (_builtShape != shapeIdx)
            {
                if (_model != null) Object.Destroy(_model);
                _model = BuildModel(shapeIdx);
                _builtShape = shapeIdx;
                if (_model != null)
                {
                    _model.transform.SetParent(_modelAnchor, false);
                    _modelAnchor.localRotation = Quaternion.Euler(0f, 25f, 0f);
                }
            }
            bool ok = _model != null;
            _skinMat.SetColor("_BaseColor", skin);
            _skinMat.color = skin;
            _rigRoot.SetActive(ok);
            return ok;
        }

        static void BuildStageProps(Theme.BackgroundTheme stage)
        {
            string key = stage.name ?? "";
            if (_propsTheme == key) { if (_propsRoot != null) _propsRoot.SetActive(true); return; }
            _propsTheme = key;
            if (_propsRoot != null) Object.Destroy(_propsRoot);
            _propsRoot = new GameObject("StageProps");
            _propsRoot.transform.SetParent(_rigRoot.transform, false);

            var names = stage.sceneryPropPrefabs;
            if (names == null || names.Length == 0) return;

            // Up to three GENEROUSLY sized props: two flanking the model up
            // close, one big landmark centered behind it — the theme should
            // dominate the frame, the ball just sits in it.
            Vector3[] spots =
            {
                new Vector3(-2.5f, 0f, 2.8f),
                new Vector3(2.7f, 0f, 3.8f),
                new Vector3(0.4f, 0f, 7.2f),
            };
            float[] yaws = { 25f, -20f, 8f };
            float[] footprints = { 5.0f, 5.0f, 7.5f };
            int placed = 0;
            for (int n = 0; placed < 3; n++)
            {
                // Cycle the theme's prefab list so 3 slots fill even when the
                // theme only ships 1-2 distinct props.
                var prefab = Resources.Load<GameObject>("Prefabs/" + names[n % names.Length]);
                if (prefab == null) { if (n >= names.Length) break; continue; }
                // Build unparented so FitToBall's world-space grounding math
                // is valid, then adopt into the rig (worldPositionStays:false
                // keeps the small centering offset as localPosition).
                var prop = Object.Instantiate(prefab);
                foreach (var col in prop.GetComponentsInChildren<Collider>())
                    Object.Destroy(col);
                var renderers = prop.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers) r.shadowCastingMode = ShadowCastingMode.Off;
                RunningGame3D.FitToBall(prop, renderers, footprints[placed], yaws[placed]);
                prop.transform.SetParent(_propsRoot.transform, false);
                prop.transform.localPosition += spots[placed];
                placed++;
                if (n >= names.Length * 3) break; // safety
            }
        }

        static Sprite LoadBackdrop(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Sprite s;
            if (_backdropCache.TryGetValue(name, out s)) return s;
            s = Resources.Load<Sprite>("Sprites/Backgrounds/" + name);
            _backdropCache[name] = s; // caches null misses too
            return s;
        }

        public static void SetActive(bool active)
        {
            if (_rigRoot != null) _rigRoot.SetActive(active && _model != null);
        }

        /// <summary>Baked 3/4-view thumbnail for a vehicle shape's catalogue
        /// card (null when the prefab isn't in Resources).</summary>
        public static Sprite VehicleThumbnail(int shapeIdx)
        {
            Sprite cached;
            if (_vehicleThumbs.TryGetValue(shapeIdx, out cached)) return cached;

            EnsureRig();
            bool wasActive = _rigRoot.activeSelf;
            int liveShape = _builtShape;
            Color liveSky = _cam.backgroundColor;

            var model = BuildModel(shapeIdx);
            Sprite sprite = null;
            if (model != null)
            {
                // Thumbnails bake against a neutral light backdrop (no
                // ground, no scenery image) so they sit cleanly on any card.
                bool backdropWas = _backdrop.activeSelf;
                _cam.backgroundColor = new Color(0.955f, 0.945f, 0.985f, 1f);
                _ground.SetActive(false);
                _backdrop.SetActive(false);
                if (_propsRoot != null) _propsRoot.SetActive(false);
                if (_model != null) _model.SetActive(false);
                model.transform.SetParent(_rigRoot.transform, false);
                model.transform.localRotation = Quaternion.Euler(0f, 35f, 0f) * model.transform.localRotation;
                _rigRoot.SetActive(true);

                var prev = RenderTexture.active;
                _cam.Render();
                RenderTexture.active = _rt;
                var tex = new Texture2D(RtWidth, RtHeight, TextureFormat.ARGB32, false);
                tex.ReadPixels(new Rect(0, 0, RtWidth, RtHeight), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                sprite = Sprite.Create(tex, new Rect(0, 0, RtWidth, RtHeight),
                    new Vector2(0.5f, 0.5f), 100f);
                Object.Destroy(model);
                if (_model != null) _model.SetActive(true);
                _cam.backgroundColor = liveSky;
                _ground.SetActive(true);
                _backdrop.SetActive(backdropWas);
                if (_propsRoot != null) _propsRoot.SetActive(true);
            }
            _rigRoot.SetActive(wasActive && liveShape >= 0);
            _vehicleThumbs[shapeIdx] = sprite; // caches null misses too
            return sprite;
        }

        // ---------------------------------------------------------- builders

        static GameObject BuildModel(int shapeIdx)
        {
            if (shapeIdx >= GameConfig.PrimitiveShapeCount)
            {
                int v = shapeIdx - GameConfig.PrimitiveShapeCount;
                if (v >= RunningGame3D.VehicleShapeCount) return null;
                var prefab = Resources.Load<GameObject>("Prefabs/" + RunningGame3D.VehicleShapeResource(v));
                if (prefab == null) return null;
                var instance = Object.Instantiate(prefab);
                foreach (var col in instance.GetComponentsInChildren<Collider>())
                    Object.Destroy(col);
                var renderers = instance.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers) r.shadowCastingMode = ShadowCastingMode.Off;
                RunningGame3D.FitToBall(instance, renderers, 1.35f,
                    RunningGame3D.VehicleShapeYaw(v));
                // FitToBall grounds/centers it at the world origin; the small
                // centering offset survives as localPosition when the caller
                // parents it under the rig (SetParent worldPositionStays:false).
                return instance;
            }
            return BuildPrimitive(shapeIdx);
        }

        static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale,
            Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = rot;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Same proportions as RunningGame3D's player shapes.</summary>
        static GameObject BuildPrimitive(int idx)
        {
            var root = new GameObject("PreviewShape" + idx);
            root.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            var t = root.transform;
            var q = Quaternion.identity;

            switch (idx)
            {
                case 1: Prim(PrimitiveType.Cube, t, Vector3.zero, Vector3.one * 0.85f, q, _skinMat); break;
                case 2: Prim(PrimitiveType.Capsule, t, Vector3.zero, new Vector3(0.44f, 0.52f, 0.44f),
                    Quaternion.Euler(0f, 0f, 90f), _skinMat); break;
                case 3: Prim(PrimitiveType.Cube, t, Vector3.zero, Vector3.one * 0.68f,
                    Quaternion.Euler(45f, 0f, 45f), _skinMat); break;
                case 4: Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(0.8f, 1.15f, 0.8f), q, _skinMat); break;
                case 5: Prim(PrimitiveType.Cylinder, t, Vector3.zero, new Vector3(0.9f, 0.14f, 0.9f),
                    Quaternion.Euler(0f, 0f, 90f), _skinMat); break;
                case 6: Prim(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.55f, 0.95f, 0.55f),
                    Quaternion.Euler(0f, 45f, 0f), _skinMat); break;
                case 7: // UFO: saucer + white dome
                    Prim(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(1.15f, 0.35f, 1.15f), q, _skinMat);
                    Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.2f, 0f), Vector3.one * 0.45f, q, _whiteMat);
                    break;
                default: // sphere + white cap dot
                    Prim(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one, q, _skinMat);
                    Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.18f, -0.28f), Vector3.one * 0.42f, q, _whiteMat);
                    break;
            }
            return root;
        }
    }
}
