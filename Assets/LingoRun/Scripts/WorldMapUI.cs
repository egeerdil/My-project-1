using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LingoRun
{
    /// <summary>
    /// Overcooked-style 3D overworld: a low-poly landscape with a winding
    /// stepping-stone trail, one pad per map level (colored by game mode, a
    /// little building behind it), and 3D Lino standing on the current level.
    /// Tap an unlocked pad → Lino hops along the trail, walks to the door and
    /// goes inside → the level launches (same StartFromMap flow as the 2D map).
    ///
    /// Everything is runtime-built like the rest of the game. Scenery uses the
    /// low-poly prefabs bundled in Resources/Prefabs (plus anything dropped in
    /// Resources/Prefabs/MapEnv by the "LingoRun → Copy Map Env Prefabs" menu
    /// once an environment pack is imported). The 3D scene lives far below the
    /// runner's world (y = -1500) with its own camera and light.
    /// </summary>
    public class WorldMapUI : MonoBehaviour
    {
        public LingoRunBootstrap bootstrap;

        const float SceneY = -1500f;
        const float NodeStepZ = 5.5f;
        const float UnitGapZ = 7f;

        GameObject _sceneRoot;
        Camera _cam;
        Transform _lino;
        Transform _linoBody;
        int _builtCount;

        class Node
        {
            public int level;
            public Vector3 pos;
            public Vector3 doorPos;
            public Renderer pad;
            public Color color;
            public Transform padTf;
            public GameObject star;
        }
        readonly List<Node> _nodes = new List<Node>();
        readonly Dictionary<Collider, Node> _tapTargets = new Dictionary<Collider, Node>();

        // ----- walk state -----
        enum LinoState { Idle, Walking, Entering }
        LinoState _state = LinoState.Idle;
        readonly Queue<Vector3> _waypoints = new Queue<Vector3>();
        Node _walkTarget;
        float _enterT;
        float _walkPhase;

        // ----- camera / input -----
        float _camZ;
        float _maxZ;
        bool _pressed;
        bool _dragging;
        Vector2 _pressPos;
        int _shakeNode = -1;
        float _shakeT;

        // ----- overlay UI -----
        Text _title;
        Text _progress;
        bool _uiBuilt;

        static Material _ground, _stone, _white, _pink, _dark, _padLocked;

        void OnEnable()
        {
            // AddComponent fires OnEnable before the bootstrap field is set;
            // the real build happens on the first ShowWorldMap activation.
            if (bootstrap == null) return;
            BuildOverlayUI();
            RefreshScene();
            if (_sceneRoot != null) _sceneRoot.SetActive(true);
        }

        void OnDisable()
        {
            if (_sceneRoot != null) _sceneRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (_sceneRoot != null) Destroy(_sceneRoot);
        }

        // ================= materials =================

        static Material Mat(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(sh);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            return m;
        }

        static void EnsureMats()
        {
            if (_ground != null) return;
            _ground = Mat(Theme.Hex("7CC26F"));
            _stone = Mat(Theme.Hex("D9C9A3"));
            _white = Mat(Color.white);
            _pink = Mat(Theme.Hex("FF8FC7"));
            _dark = Mat(Theme.Hex("2E2A3B"));
            _padLocked = Mat(Theme.Hex("B4B2A9"));
        }

        // ================= scene build =================

        static int LevelCount()
        {
            int reached = Mathf.Max(1, ProgressManager.MapLevelReached);
            int desired = Mathf.Max(GameConfig.MapLevelCount, reached + 3);
            return Mathf.CeilToInt(desired / (float)GameConfig.LevelsPerUnit)
                * GameConfig.LevelsPerUnit;
        }

        static Vector3 NodeLocalPos(int level)
        {
            int u = (level - 1) / GameConfig.LevelsPerUnit;
            int li = (level - 1) % GameConfig.LevelsPerUnit;
            float[] xs = { -2.4f, 2.2f, -2.1f, 2.5f, 0f };
            float z = (level - 1) * NodeStepZ + u * UnitGapZ;
            float x = xs[li] + Mathf.Sin(level * 2.37f) * 0.5f;
            return new Vector3(x, 0f, z);
        }

        void RefreshScene()
        {
            int count = LevelCount();
            if (_sceneRoot == null || count != _builtCount) BuildScene(count);
            UpdateNodeStates();
            PlaceLinoAtCurrent();
            int reached = Mathf.Clamp(ProgressManager.MapLevelReached, 1, count);
            _camZ = NodeLocalPos(reached).z;
            if (_progress != null) _progress.text = Loc.F("map.progress", reached);
            if (_title != null) _title.text = Loc.T("map.title");
        }

        void BuildScene(int count)
        {
            if (_sceneRoot != null) Destroy(_sceneRoot);
            _nodes.Clear();
            _tapTargets.Clear();
            _builtCount = count;
            EnsureMats();

            _sceneRoot = new GameObject("WorldMap3D");
            _sceneRoot.transform.position = new Vector3(0f, SceneY, 0f);
            var t = _sceneRoot.transform;
            _maxZ = NodeLocalPos(count).z + 6f;

            // Camera + light.
            var camGo = new GameObject("WorldMapCam", typeof(Camera));
            camGo.transform.SetParent(t, false);
            _cam = camGo.GetComponent<Camera>();
            _cam.depth = 10f;
            _cam.fieldOfView = 50f;
            _cam.nearClipPlane = 0.5f;
            _cam.farClipPlane = 120f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Theme.Hex("AEE3F5");
            camGo.transform.localRotation = Quaternion.Euler(42f, 0f, 0f);

            var lightGo = new GameObject("Sun", typeof(Light));
            lightGo.transform.SetParent(t, false);
            var sun = lightGo.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = Theme.Hex("FFF4E0");
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            // Ground.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(t, false);
            ground.transform.localScale = new Vector3(70f, 1f, _maxZ + 60f);
            ground.transform.localPosition = new Vector3(0f, -0.5f, _maxZ * 0.5f);
            ground.GetComponent<Renderer>().sharedMaterial = _ground;
            Destroy(ground.GetComponent<Collider>());

            // Nodes + trail.
            var rng = new System.Random(1234);
            for (int lvl = 1; lvl <= count; lvl++)
            {
                var node = BuildNode(t, lvl);
                _nodes.Add(node);
                if (lvl > 1) BuildTrail(t, _nodes[lvl - 2].pos, node.pos);
            }

            // A gate at the start of every unit (temple/torii if present).
            for (int u = 0; u * GameConfig.LevelsPerUnit < count; u++)
            {
                int firstLevel = u * GameConfig.LevelsPerUnit + 1;
                var gatePos = NodeLocalPos(firstLevel) + new Vector3(0f, 0f, -2.6f);
                var gate = Spawn(t, u % 2 == 0 ? "torii_gate" : "japanese_temple",
                    gatePos, 2.6f, rng);
                if (gate != null)
                {
                    var banner = MakeText(t, Loc.Topic(GameConfig.UnitTopicForLevel(firstLevel)),
                        gatePos + new Vector3(0f, 3.4f, 0f), 0.55f, Theme.Hex("5B3FC9"));
                    banner.anchor = TextAnchor.MiddleCenter;
                }
            }

            ScatterEnvironment(t, count, rng);
            BuildLino(t);
        }

        Node BuildNode(Transform t, int lvl)
        {
            var def = GameConfig.GetMapLevel(lvl);
            var node = new Node { level = lvl, pos = NodeLocalPos(lvl), color = PadColor(def.mode) };

            var group = new GameObject("Node" + lvl);
            group.transform.SetParent(t, false);
            group.transform.localPosition = node.pos;
            node.padTf = group.transform;

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.transform.SetParent(group.transform, false);
            ring.transform.localScale = new Vector3(2.7f, 0.06f, 2.7f);
            ring.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            ring.GetComponent<Renderer>().sharedMaterial = _white;
            Destroy(ring.GetComponent<Collider>());

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.transform.SetParent(group.transform, false);
            pad.transform.localScale = new Vector3(2.2f, 0.14f, 2.2f);
            pad.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            node.pad = pad.GetComponent<Renderer>();
            node.pad.material = Mat(node.color);
            Destroy(pad.GetComponent<Collider>());

            // One big tap target for the whole plot (pad + building).
            var tap = group.AddComponent<SphereCollider>();
            tap.radius = 2.1f;
            tap.center = new Vector3(0f, 0.6f, 0f);
            _tapTargets[tap] = node;

            var num = MakeText(group.transform, lvl.ToString(),
                new Vector3(0f, 1.05f, -0.4f), 0.5f, Theme.Hex("2E2A3B"));
            num.anchor = TextAnchor.MiddleCenter;

            // Completed marker: the icon-pack star as a billboard sprite
            // (the display font has no ★ glyph, so no TextMesh here).
            node.star = new GameObject("Star");
            node.star.transform.SetParent(group.transform, false);
            node.star.transform.localPosition = new Vector3(0f, 2f, 0.3f);
            node.star.transform.localRotation = Quaternion.Euler(42f, 0f, 0f);
            node.star.transform.localScale = Vector3.one * 0.35f;
            var starSr = node.star.AddComponent<SpriteRenderer>();
            starSr.sprite = UIFactory.IconSprite("star");
            node.star.SetActive(false);

            // Little building — Lino walks in here. Style rotates per unit.
            string[] houses = { "camping_tent_red", "building", "wooden_winter_house",
                "camping_tent_blue", "building_3", "railway_station",
                "camping_tent_yellow", "building_2", "wooden_winter_house_2", "building_4" };
            int u = (lvl - 1) / GameConfig.LevelsPerUnit;
            float side = node.pos.x >= 0f ? 1f : -1f;
            var housePos = node.pos + new Vector3(side * 3.6f, 0f, 1.2f);
            var rng = new System.Random(lvl * 977);
            var house = Spawn(t, houses[(u * 3 + (lvl % 3)) % houses.Length], housePos, 2.4f, rng);
            if (house != null)
                house.transform.localRotation = Quaternion.Euler(0f, side > 0f ? -105f : 105f, 0f);
            node.doorPos = node.pos + new Vector3(side * 2.4f, 0f, 0.8f);
            return node;
        }

        void BuildTrail(Transform t, Vector3 a, Vector3 b)
        {
            const int stones = 6;
            Vector3 dir = (b - a).normalized;
            Vector3 perp = Vector3.Cross(dir, Vector3.up);
            for (int k = 1; k <= stones; k++)
            {
                float f = k / (float)(stones + 1);
                Vector3 p = Vector3.Lerp(a, b, f) + perp * Mathf.Sin(f * Mathf.PI) * 0.7f;
                var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                s.transform.SetParent(t, false);
                s.transform.localScale = new Vector3(0.85f, 0.05f, 0.85f);
                s.transform.localPosition = p + new Vector3(0f, 0.05f, 0f);
                s.GetComponent<Renderer>().sharedMaterial = _stone;
                Destroy(s.GetComponent<Collider>());
            }
        }

        void ScatterEnvironment(Transform t, int count, System.Random rng)
        {
            // Prefer a dedicated environment pack if one was copied in
            // (LingoRun → Copy Map Env Prefabs), else the bundled low-poly set.
            var env = Resources.LoadAll<GameObject>("Prefabs/MapEnv");
            string[] fallback = { "autumn_tree", "autumn_tree_2", "autumn_tree_3",
                "fir_tree_medium", "sakura_tree", "palm_tree_large", "christmas_tree",
                "wooden_post", "mesh_fence", "hitching_post" };
            int props = Mathf.Min(24 + count * 2, 110);
            for (int i = 0; i < props; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                float x = side * (5.2f + (float)rng.NextDouble() * 9f);
                float z = -6f + (float)rng.NextDouble() * (_maxZ + 10f);
                GameObject inst = null;
                if (env != null && env.Length > 0)
                {
                    var pf = env[rng.Next(env.Length)];
                    inst = SpawnPrefab(t, pf, new Vector3(x, 0f, z),
                        1.6f + (float)rng.NextDouble() * 1.6f);
                }
                else
                {
                    inst = Spawn(t, fallback[rng.Next(fallback.Length)],
                        new Vector3(x, 0f, z), 1.6f + (float)rng.NextDouble() * 1.6f, rng);
                }
                if (inst != null)
                    inst.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);
            }
        }

        static GameObject Spawn(Transform t, string prefabName, Vector3 pos,
            float targetHeight, System.Random rng)
        {
            var pf = Resources.Load<GameObject>("Prefabs/" + prefabName);
            return pf == null ? null : SpawnPrefab(t, pf, pos, targetHeight);
        }

        static GameObject SpawnPrefab(Transform t, GameObject pf, Vector3 pos, float targetHeight)
        {
            var inst = Instantiate(pf, t);
            inst.transform.localPosition = pos;
            foreach (var c in inst.GetComponentsInChildren<Collider>()) Destroy(c);
            // Normalize wildly different pack scales to a target height.
            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float h = Mathf.Max(0.01f, b.size.y);
                inst.transform.localScale *= targetHeight / h;
                // Re-ground after scaling: bottom of the bounds sits on y=0
                // of the scene root (the ground plane's top).
                b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                inst.transform.position += new Vector3(0f, t.position.y - b.min.y, 0f);
            }
            return inst;
        }

        static TextMesh MakeText(Transform parent, string text, Vector3 pos,
            float size, Color color)
        {
            var go = new GameObject("Txt");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(42f, 0f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = UIFactory.DisplayFont;
            tm.fontSize = 64;
            tm.characterSize = size * 0.14f;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = tm.font.material;
            return tm;
        }

        static Color PadColor(GameMode m)
        {
            switch (m)
            {
                case GameMode.Sentences: return Theme.Teal;
                case GameMode.Match: return Theme.Purple;
                case GameMode.Bubbles: return Theme.Hex("FF8FC7");
                default: return Theme.Hex("6FBBE8");
            }
        }

        void UpdateNodeStates()
        {
            int reached = Mathf.Max(1, ProgressManager.MapLevelReached);
            foreach (var n in _nodes)
            {
                bool locked = n.level > reached;
                n.pad.sharedMaterial = locked ? _padLocked : Mat(n.color);
                n.star.SetActive(n.level < reached);
            }
        }

        // ================= Lino =================

        void BuildLino(Transform t)
        {
            var root = new GameObject("Lino3D");
            root.transform.SetParent(t, false);
            _lino = root.transform;

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            _linoBody = body.transform;

            AddPart(body.transform, PrimitiveType.Sphere, new Vector3(0f, 0.55f, 0f),
                new Vector3(0.72f, 0.8f, 0.62f), _white);                        // body
            AddPart(body.transform, PrimitiveType.Sphere, new Vector3(0f, 1.22f, 0.02f),
                new Vector3(0.56f, 0.54f, 0.54f), _white);                       // head
            AddPart(body.transform, PrimitiveType.Capsule, new Vector3(-0.14f, 1.72f, -0.02f),
                new Vector3(0.15f, 0.3f, 0.15f), _white)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 8f);         // ear L
            AddPart(body.transform, PrimitiveType.Capsule, new Vector3(0.14f, 1.72f, -0.02f),
                new Vector3(0.15f, 0.3f, 0.15f), _white)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -8f);        // ear R
            AddPart(body.transform, PrimitiveType.Sphere, new Vector3(-0.1f, 1.28f, 0.24f),
                new Vector3(0.09f, 0.11f, 0.06f), _dark);                        // eye L
            AddPart(body.transform, PrimitiveType.Sphere, new Vector3(0.1f, 1.28f, 0.24f),
                new Vector3(0.09f, 0.11f, 0.06f), _dark);                        // eye R
            AddPart(body.transform, PrimitiveType.Sphere, new Vector3(0f, 1.16f, 0.27f),
                new Vector3(0.09f, 0.07f, 0.07f), _pink);                        // nose
            AddPart(body.transform, PrimitiveType.Sphere, new Vector3(-0.16f, 0.12f, 0.1f),
                new Vector3(0.24f, 0.14f, 0.34f), _white);                       // foot L
            AddPart(body.transform, PrimitiveType.Sphere, new Vector3(0.16f, 0.12f, 0.1f),
                new Vector3(0.24f, 0.14f, 0.34f), _white);                       // foot R
            AddPart(body.transform, PrimitiveType.Sphere, new Vector3(0f, 0.5f, -0.34f),
                new Vector3(0.2f, 0.2f, 0.2f), _white);                          // tail
        }

        static GameObject AddPart(Transform parent, PrimitiveType type, Vector3 pos,
            Vector3 scale, Material m)
        {
            var p = GameObject.CreatePrimitive(type);
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localScale = scale;
            p.GetComponent<Renderer>().sharedMaterial = m;
            Destroy(p.GetComponent<Collider>());
            return p;
        }

        void PlaceLinoAtCurrent()
        {
            if (_lino == null) return;
            _state = LinoState.Idle;
            _waypoints.Clear();
            _lino.localScale = Vector3.one;
            int reached = Mathf.Clamp(Mathf.Max(1, ProgressManager.MapLevelReached),
                1, _builtCount);
            _lino.localPosition = NodeLocalPos(reached) + new Vector3(0f, 0.2f, 0f);
            _lino.localRotation = Quaternion.identity;
        }

        // ================= interaction / update =================

        void Update()
        {
            if (_cam == null) return;
            HandleInput();
            AnimateLino();
            AnimatePads();

            // Camera follows Lino while walking, else the drag target.
            if (_state != LinoState.Idle) _camZ = _lino.localPosition.z;
            _camZ = Mathf.Clamp(_camZ, 0f, _maxZ);
            var want = new Vector3(0f, 9.6f, _camZ - 8.6f);
            var ct = _cam.transform;
            ct.localPosition = Vector3.Lerp(ct.localPosition, want,
                1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        void HandleInput()
        {
            var ptr = Pointer.current;
            if (ptr == null || _state == LinoState.Entering) return;
            Vector2 pos = ptr.position.ReadValue();

            if (ptr.press.wasPressedThisFrame)
            {
                _pressed = true;
                _dragging = false;
                _pressPos = pos;
            }
            else if (_pressed && ptr.press.isPressed)
            {
                if (!_dragging && (pos - _pressPos).magnitude > 18f) _dragging = true;
                if (_dragging)
                {
                    float perPixel = 22f / Screen.height; // world units per screen px
                    _camZ += (_pressPos.y - pos.y) * perPixel * -1f;
                    _pressPos = pos;
                }
            }
            else if (_pressed && ptr.press.wasReleasedThisFrame)
            {
                _pressed = false;
                if (!_dragging && pos.y < Screen.height * 0.88f) TryTap(pos);
            }
        }

        void TryTap(Vector2 screenPos)
        {
            RaycastHit hit;
            if (!Physics.Raycast(_cam.ScreenPointToRay(screenPos), out hit, 200f)) return;
            Node node;
            if (!_tapTargets.TryGetValue(hit.collider, out node)) return;

            int reached = Mathf.Max(1, ProgressManager.MapLevelReached);
            if (node.level > reached)
            {
                ProceduralAudio.PlayWrong();
                _shakeNode = _nodes.IndexOf(node);
                _shakeT = 0.4f;
                return;
            }
            if (HeartsManager.Hearts <= 0)
            {
                // No hearts: skip the walk, let StartFromMap surface the popup.
                bootstrap.StartFromMap(node.level);
                return;
            }
            ProceduralAudio.PlayClick();
            StartWalk(node);
        }

        void StartWalk(Node target)
        {
            _waypoints.Clear();
            _walkTarget = target;

            // Route through every pad between Lino's nearest node and the target.
            int from = NearestLevel(_lino.localPosition);
            int step = target.level >= from ? 1 : -1;
            for (int l = from + step; l != target.level + step; l += step)
                _waypoints.Enqueue(NodeLocalPos(l) + new Vector3(0f, 0.2f, 0f));
            _waypoints.Enqueue(target.doorPos + new Vector3(0f, 0.2f, 0f));
            _state = LinoState.Walking;
        }

        int NearestLevel(Vector3 p)
        {
            int best = 1;
            float bestD = float.MaxValue;
            foreach (var n in _nodes)
            {
                float d = (n.pos - new Vector3(p.x, 0f, p.z)).sqrMagnitude;
                if (d < bestD) { bestD = d; best = n.level; }
            }
            return best;
        }

        void AnimateLino()
        {
            if (_lino == null) return;

            if (_state == LinoState.Walking)
            {
                if (_waypoints.Count == 0)
                {
                    _state = LinoState.Entering;
                    _enterT = 0f;
                    return;
                }
                Vector3 wp = _waypoints.Peek();
                Vector3 cur = _lino.localPosition;
                Vector3 delta = wp - cur;
                float speed = 5.2f;
                if (delta.magnitude < speed * Time.deltaTime) _waypoints.Dequeue();
                _lino.localPosition = Vector3.MoveTowards(cur, wp, speed * Time.deltaTime);
                if (delta.sqrMagnitude > 0.001f)
                    _lino.localRotation = Quaternion.Slerp(_lino.localRotation,
                        Quaternion.LookRotation(new Vector3(delta.x, 0f, delta.z)),
                        10f * Time.deltaTime);
                // Hop.
                _walkPhase += Time.deltaTime * 11f;
                _linoBody.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.22f, 0f);
            }
            else if (_state == LinoState.Entering)
            {
                _enterT += Time.deltaTime;
                float f = Mathf.Clamp01(_enterT / 0.55f);
                _lino.localScale = Vector3.one * (1f - f);
                _lino.localRotation *= Quaternion.Euler(0f, 540f * Time.deltaTime, 0f);
                if (f >= 1f)
                {
                    var lvl = _walkTarget.level;
                    _state = LinoState.Idle;
                    bootstrap.StartFromMap(lvl);
                }
            }
            else
            {
                // Idle bob + ear vibe.
                _walkPhase += Time.deltaTime * 2.2f;
                _linoBody.localPosition = new Vector3(0f, Mathf.Sin(_walkPhase) * 0.04f, 0f);
            }
        }

        void AnimatePads()
        {
            int reached = Mathf.Max(1, ProgressManager.MapLevelReached);
            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                if (n.level == reached && _state == LinoState.Idle)
                {
                    float s = 1f + Mathf.Sin(Time.time * 3f) * 0.045f;
                    n.padTf.localScale = new Vector3(s, 1f, s);
                }
                else if (i == _shakeNode && _shakeT > 0f)
                {
                    _shakeT -= Time.deltaTime;
                    n.padTf.localPosition = n.pos + new Vector3(
                        Mathf.Sin(_shakeT * 50f) * 0.12f * (_shakeT / 0.4f), 0f, 0f);
                    if (_shakeT <= 0f) n.padTf.localPosition = n.pos;
                }
                else n.padTf.localScale = Vector3.one;
            }
        }

        // ================= overlay UI =================

        void BuildOverlayUI()
        {
            if (_uiBuilt) return;
            _uiBuilt = true;
            var t = transform;

            var back = UIFactory.CreateButton(t, "Back", Loc.T("common.back"), 13,
                Theme.CreamPanel, Theme.Ink,
                delegate { ProceduralAudio.PlayClick(); bootstrap.ShowMap(); });
            UIFactory.PlaceTop(back.GetComponent<RectTransform>(), 70f, 34f, 100f, 36f);

            var flat = UIFactory.CreateButton(t, "To2D", "2D", 13,
                Theme.CreamPanel, Theme.Ink,
                delegate { ProceduralAudio.PlayClick(); bootstrap.ShowMap(); });
            UIFactory.PlaceTop(flat.GetComponent<RectTransform>(), 372f, 34f, 96f, 36f);

            _title = UIFactory.CreateText(t, "Title", "", 26, Color.white);
            _title.font = UIFactory.DisplayFont;
            var to = _title.gameObject.AddComponent<Outline>();
            to.effectColor = new Color(0.18f, 0.1f, 0.3f, 0.65f);
            to.effectDistance = new Vector2(1.5f, -1.5f);
            UIFactory.BandTop(_title.rectTransform, 62f, 40f);

            _progress = UIFactory.CreateText(t, "Progress", "", 14, Color.white);
            var po = _progress.gameObject.AddComponent<Outline>();
            po.effectColor = new Color(0.18f, 0.1f, 0.3f, 0.65f);
            po.effectDistance = new Vector2(1f, -1f);
            UIFactory.BandTop(_progress.rectTransform, 104f, 20f);
        }
    }
}
