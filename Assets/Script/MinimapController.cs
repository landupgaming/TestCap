using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class MinimapController : MonoBehaviour
{
    [Header("Scene Refs")]
    [SerializeField] Transform player;
    [SerializeField] Canvas mainCanvas;               // optional; auto-find if null

    [Header("UI Roots")]
    [SerializeField] RectTransform miniMapRoot;       // small map container on UI
    [SerializeField] RectTransform fullMapRoot;       // fullscreen overlay container
    [SerializeField] KeyCode toggleFullMapKey = KeyCode.Tab;

    [Header("Sizing")]
    [SerializeField] float cellSize = 24f;            // pixel size of a room cell
    [SerializeField] float cellGap = 2f;             // spacing between cells (px)
    [SerializeField] float doorThickness = 6f;        // length of door stub (px)
    [SerializeField] float doorInset = 2f;        // inset from cell edge (px)
    [SerializeField] bool invertY = true;      // +Z shows upward on map

    [Header("Colors")]
    [SerializeField] Color cellHidden = new(0.2f, 0.2f, 0.2f, 0.35f);
    [SerializeField] Color cellShown = new(0.95f, 0.95f, 0.95f, 0.95f);
    [SerializeField] Color doorOpen = new(0.2f, 1f, 0.2f, 1f);
    [SerializeField] Color doorClosed = new(1f, 0.25f, 0.25f, 1f);
    [SerializeField] Color playerColor = new(0.2f, 1f, 0.2f, 1f);
    [SerializeField] float playerDotSize = 6f;

    // ---------- internals ----------
    enum Dir { North, East, South, West }

    class RoomInfo
    {
        public Transform root;     // scene root
        public Bounds bounds;    // world-space bounds (XZ used)
        public Vector2Int grid;    // stable 2D index for UI layout
        public readonly Dictionary<Dir, bool> doorOpen = new(); // true=open
        public RectTransform cellUI;
        public Image bg;
        public readonly Dictionary<Dir, Image> doorUI = new();
        public bool revealed;
    }

    readonly List<RoomInfo> _rooms = new();
    readonly Dictionary<Vector2Int, RoomInfo> _gridToRoom = new();
    Image _playerDot;
    bool _fullOn;

    // auto-rescan
    float _rescanTimer;
    int _lastSceneRoomCount;

    void Awake()
    {
        if (!mainCanvas) mainCanvas = FindFirstObjectByType<Canvas>();

        if (!miniMapRoot)
        {
            var go = new GameObject("MiniMap", typeof(RectTransform));
            miniMapRoot = go.GetComponent<RectTransform>();
            miniMapRoot.SetParent(mainCanvas.transform, false);
            miniMapRoot.anchorMin = new Vector2(0, 1);
            miniMapRoot.anchorMax = new Vector2(0, 1);
            miniMapRoot.pivot = new Vector2(0, 1);
            miniMapRoot.anchoredPosition = new Vector2(16, -16);
            miniMapRoot.sizeDelta = new Vector2(400, 300);
        }

        if (!fullMapRoot)
        {
            var go = new GameObject("FullMap", typeof(RectTransform));
            fullMapRoot = go.GetComponent<RectTransform>();
            fullMapRoot.SetParent(mainCanvas.transform, false);
            fullMapRoot.anchorMin = Vector2.zero;
            fullMapRoot.anchorMax = Vector2.one;
            fullMapRoot.pivot = new Vector2(0.5f, 0.5f);
            fullMapRoot.offsetMin = fullMapRoot.offsetMax = Vector2.zero;
            fullMapRoot.gameObject.SetActive(false);
        }

        // build minimal UI; rooms will be scanned shortly
        _playerDot = CreateDot(miniMapRoot, playerDotSize, playerColor);

        ForceRescan();  // first pass (handles static scenes)
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleFullMapKey))
        {
            _fullOn = !_fullOn;
            fullMapRoot.gameObject.SetActive(_fullOn);
        }

        // Periodically rescan after generation or if room count changes.
        _rescanTimer -= Time.deltaTime;
        if (_rescanTimer <= 0f)
        {
            _rescanTimer = 0.75f;

            int sceneCount = CountSceneRooms();
            if (sceneCount != _lastSceneRoomCount)
            {
                _lastSceneRoomCount = sceneCount;
                ForceRescan();
            }
        }

        TrackPlayerAndReveal();
        UpdatePlayerDot();
    }

    // -------- room discovery & caching --------
    int CountSceneRooms()
    {
        // Prefer objects tagged "Room" (your generator tags them), else look for Room component/name.
        int tagged = GameObject.FindGameObjectsWithTag("Room").Length;
        if (tagged > 0) return tagged;

        return FindObjectsOfType<Transform>(true)
            .Count(t => t.GetComponent("Room") != null || t.name.ToLower().Contains("room"));
    }

    void CacheRoomsFromScene()
    {
        _rooms.Clear();
        _gridToRoom.Clear();

        // Prefer tagged rooms first (generator uses tag "Room").
        var tagged = GameObject.FindGameObjectsWithTag("Room");
        IEnumerable<Transform> roomRoots = tagged.Length > 0
            ? tagged.Select(go => go.transform)
            : FindObjectsOfType<Transform>(true)
              .Where(t => t.GetComponent("Room") != null || t.name.ToLower().Contains("room"));

        foreach (var tr in roomRoots)
        {
            var info = new RoomInfo { root = tr, bounds = ComputeWorldBoundsXZ(tr) };
            if (info.bounds.size == Vector3.zero) continue;

            var center = info.bounds.center;
            float step = Mathf.Max(1f, Mathf.Round(Mathf.Max(info.bounds.size.x, info.bounds.size.z)));
            int gx = Mathf.RoundToInt(center.x / step);
            int gy = Mathf.RoundToInt(center.z / step);
            info.grid = new Vector2Int(gx, gy);

            info.doorOpen[Dir.North] = false;
            info.doorOpen[Dir.East] = false;
            info.doorOpen[Dir.South] = false;
            info.doorOpen[Dir.West] = false;

            InferDoorways(info);

            _rooms.Add(info);
            if (!_gridToRoom.ContainsKey(info.grid))
                _gridToRoom.Add(info.grid, info);
        }
    }

    static Bounds ComputeWorldBoundsXZ(Transform root)
    {
        bool has = false;
        Bounds b = new Bounds(root.position, Vector3.zero);

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        foreach (var c in root.GetComponentsInChildren<Collider>(true))
        {
            if (!has) { b = c.bounds; has = true; }
            else b.Encapsulate(c.bounds);
        }

        if (!has) return new Bounds(root.position, Vector3.zero);
        b.center = new Vector3(b.center.x, 0f, b.center.z);
        b.extents = new Vector3(b.extents.x, 0.1f, b.extents.z);
        return b;
    }

    void InferDoorways(RoomInfo info)
    {
        var doorTs = info.root.GetComponentsInChildren<Transform>(true)
            .Where(t => t != info.root &&
                        (t.name.ToLower().Contains("doorway") ||
                         t.GetComponent("Doorway") != null))
            .ToList();

        Vector3 c = info.bounds.center;

        foreach (var dt in doorTs)
        {
            Vector3 d = dt.position - c; d.y = 0f;
            Dir dir = Mathf.Abs(d.x) > Mathf.Abs(d.z)
                ? (d.x > 0 ? Dir.East : Dir.West)
                : (d.z > 0 ? Dir.North : Dir.South);

            bool hasCap = dt.GetComponentsInChildren<Transform>(true)
                           .Any(t => t != dt && (t.name.ToLower().Contains("doorwaycap") ||
                                                 t.GetComponent("DoorwayCap") != null) &&
                                     t.gameObject.activeInHierarchy);

            info.doorOpen[dir] = !hasCap;
        }
    }

    // -------- UI build --------
    void BuildInitialUI()
    {
        foreach (Transform t in miniMapRoot) Destroy(t.gameObject);
        foreach (Transform t in fullMapRoot) if (t != miniMapRoot) Destroy(t.gameObject);

        // Player dot first so it exists even before rooms appear
        _playerDot = CreateDot(_fullOn ? fullMapRoot : miniMapRoot, playerDotSize, playerColor);

        foreach (var r in _rooms)
        {
            r.cellUI = CreateCell(miniMapRoot, r.grid);
            r.bg = CreateCellBG(r.cellUI, cellHidden);

            r.doorUI[Dir.North] = CreateDoorStub(r.cellUI, Dir.North);
            r.doorUI[Dir.East] = CreateDoorStub(r.cellUI, Dir.East);
            r.doorUI[Dir.South] = CreateDoorStub(r.cellUI, Dir.South);
            r.doorUI[Dir.West] = CreateDoorStub(r.cellUI, Dir.West);

            r.cellUI.gameObject.SetActive(false);
            UpdateDoorColors(r);
        }
    }

    RectTransform CreateCell(RectTransform parent, Vector2Int g)
    {
        float step = cellSize + cellGap;
        float x = (g.x + 0.5f) * step;
        float y = (g.y + 0.5f) * step;
        if (invertY) y = -(g.y + 0.5f) * step;

        var rt = new GameObject($"Cell_{g.x}_{g.y}", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.sizeDelta = new Vector2(cellSize, cellSize);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        return rt;
    }

    Image CreateCellBG(RectTransform cell, Color col)
    {
        var img = new GameObject("BG", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(cell, false);
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
        img.color = col;
        return img;
    }

    Image CreateDoorStub(RectTransform cell, Dir dir)
    {
        var img = new GameObject(dir.ToString(), typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rt = img.rectTransform;
        rt.SetParent(cell, false);

        float half = cellSize * 0.5f;
        float len = Mathf.Clamp(doorThickness, 2f, cellSize);
        float inset = Mathf.Clamp(doorInset, 0f, cellSize / 2f - 1f);

        switch (dir)
        {
            case Dir.North:
                rt.sizeDelta = new Vector2(len, 2f);
                rt.anchoredPosition = new Vector2(0f, half - inset);
                break;
            case Dir.South:
                rt.sizeDelta = new Vector2(len, 2f);
                rt.anchoredPosition = new Vector2(0f, -half + inset);
                break;
            case Dir.East:
                rt.sizeDelta = new Vector2(2f, len);
                rt.anchoredPosition = new Vector2(half - inset, 0f);
                break;
            case Dir.West:
                rt.sizeDelta = new Vector2(2f, len);
                rt.anchoredPosition = new Vector2(-half + inset, 0f);
                break;
        }
        img.color = doorClosed;
        return img;
    }

    Image CreateDot(RectTransform parent, float size, Color col)
    {
        var img = new GameObject("PlayerDot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.rectTransform.SetParent(parent, false);
        img.rectTransform.sizeDelta = new Vector2(size, size);
        img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.color = col;
        return img;
    }

    void UpdateDoorColors(RoomInfo r)
    {
        r.doorUI[Dir.North].color = r.doorOpen[Dir.North] ? doorOpen : doorClosed;
        r.doorUI[Dir.East].color = r.doorOpen[Dir.East] ? doorOpen : doorClosed;
        r.doorUI[Dir.South].color = r.doorOpen[Dir.South] ? doorOpen : doorClosed;
        r.doorUI[Dir.West].color = r.doorOpen[Dir.West] ? doorOpen : doorClosed;
    }

    // -------- runtime tracking --------
    void TrackPlayerAndReveal()
    {
        if (!player || _rooms.Count == 0) return;

        RoomInfo current = null;
        Vector3 p = player.position;
        foreach (var r in _rooms)
        {
            var b = r.bounds;
            if (p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z)
            {
                current = r; break;
            }
        }
        if (current == null) return;

        if (!current.revealed)
        {
            current.revealed = true;
            EnsureCellUI(current);
            current.bg.color = cellShown;
            current.cellUI.gameObject.SetActive(true);
            UpdateDoorColors(current);
            RevealNeighbors(current);
        }
    }

    void RevealNeighbors(RoomInfo r)
    {
        foreach (var kv in r.doorOpen)
        {
            if (!kv.Value) continue;
            var nGrid = r.grid + DirToDelta(kv.Key);
            if (_gridToRoom.TryGetValue(nGrid, out var n) && !n.revealed)
            {
                n.revealed = true;
                EnsureCellUI(n);
                n.bg.color = cellShown;
                n.cellUI.gameObject.SetActive(true);
                UpdateDoorColors(n);
            }
        }
    }

    void UpdatePlayerDot()
    {
        if (!player || _gridToRoom.Count == 0 || _playerDot == null) return;

        Vector2Int g = WorldToGridPublic(player.position);
        float step = cellSize + cellGap;
        float x = (g.x + 0.5f) * step;
        float y = (g.y + 0.5f) * step;
        if (invertY) y = -(g.y + 0.5f) * step;

        var parent = _fullOn ? fullMapRoot : miniMapRoot;
        if (_playerDot.rectTransform.parent != parent)
            _playerDot.rectTransform.SetParent(parent, false);

        _playerDot.rectTransform.anchoredPosition = new Vector2(x, y);
    }

    Vector2Int NearestGridTo(Vector3 worldPos)
    {
        float best = float.MaxValue;
        Vector2Int bestG = default;
        foreach (var r in _rooms)
        {
            float d = Vector2.SqrMagnitude(new Vector2(worldPos.x, worldPos.z) -
                                           new Vector2(r.bounds.center.x, r.bounds.center.z));
            if (d < best) { best = d; bestG = r.grid; }
        }
        return bestG;
    }

    static Vector2Int DirToDelta(Dir d) => d switch
    {
        Dir.North => new Vector2Int(0, 1),
        Dir.East => new Vector2Int(1, 0),
        Dir.South => new Vector2Int(0, -1),
        Dir.West => new Vector2Int(-1, 0),
        _ => Vector2Int.zero
    };

    // ------------ Public helpers / compatibility API ------------
    public Vector2Int WorldToGridPublic(Vector3 worldPos) => NearestGridTo(worldPos);

    public void RegisterRoom(Vector2Int grid, bool revealNow = false)
    {
        if (_gridToRoom.TryGetValue(grid, out var r))
        {
            EnsureCellUI(r);
            if (revealNow && !r.revealed)
            {
                r.revealed = true;
                r.bg.color = cellShown;
                r.cellUI.gameObject.SetActive(true);
                UpdateDoorColors(r);
            }
            return;
        }

        // Placeholder if someone registers a grid we don't know yet.
        var placeholder = new GameObject($"Cell_{grid.x}_{grid.y}", typeof(RectTransform)).GetComponent<RectTransform>();
        placeholder.SetParent(miniMapRoot, false);
        placeholder.sizeDelta = new Vector2(cellSize, cellSize);
        placeholder.anchorMin = placeholder.anchorMax = placeholder.pivot = new Vector2(0.5f, 0.5f);
        float step = cellSize + cellGap;
        float x = (grid.x + 0.5f) * step;
        float y = (grid.y + 0.5f) * step;
        if (invertY) y = -(grid.y + 0.5f) * step;
        placeholder.anchoredPosition = new Vector2(x, y);

        var bg = new GameObject("BG", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        bg.rectTransform.SetParent(placeholder, false);
        bg.rectTransform.anchorMin = Vector2.zero;
        bg.rectTransform.anchorMax = Vector2.one;
        bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
        bg.color = revealNow ? cellShown : cellHidden;

        placeholder.gameObject.SetActive(revealNow);
    }

    public void RevealRoom(Vector2Int grid)
    {
        if (_gridToRoom.TryGetValue(grid, out var r))
        {
            if (!r.revealed)
            {
                r.revealed = true;
                EnsureCellUI(r);
                r.bg.color = cellShown;
                r.cellUI.gameObject.SetActive(true);
                UpdateDoorColors(r);
                RevealNeighbors(r);
            }
        }
        else
        {
            RegisterRoom(grid, true);
        }
    }

    public void RevealAtWorldPosition(Vector3 worldPos) => RevealRoom(WorldToGridPublic(worldPos));

    // ------------ Maintenance ------------
    public void ForceRescan()
    {
        CacheRoomsFromScene();
        BuildInitialUI();
        _lastSceneRoomCount = CountSceneRooms();
    }

    public void SetPlayer(Transform t) => player = t;
    public void SetCellSize(float px) { cellSize = Mathf.Max(4f, px); ForceRescan(); }
    public void SetGap(float px) { cellGap = Mathf.Max(0f, px); ForceRescan(); }

    // ensure UI exists for a room (used when revealing a room discovered after initial build)
    void EnsureCellUI(RoomInfo r)
    {
        if (r.cellUI != null) return;

        r.cellUI = CreateCell(miniMapRoot, r.grid);
        r.bg = CreateCellBG(r.cellUI, cellHidden);
        r.doorUI[Dir.North] = CreateDoorStub(r.cellUI, Dir.North);
        r.doorUI[Dir.East] = CreateDoorStub(r.cellUI, Dir.East);
        r.doorUI[Dir.South] = CreateDoorStub(r.cellUI, Dir.South);
        r.doorUI[Dir.West] = CreateDoorStub(r.cellUI, Dir.West);
        UpdateDoorColors(r);
        r.cellUI.gameObject.SetActive(false);
    }
}
