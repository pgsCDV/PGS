using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ServerDataManager {
    public int seed;
    public string roomID;
}

public class MazeGame : MonoBehaviour {

    public static MazeGame Instance;
    public static ServerDataManager manager;

    public bool enableFogOfWar = false;
    public Transform playerTransform;
    public float viewRadius = 15f;
    public LayerMask obstructionLayer;

    public GameObject Floor, Wall, Pillar, GoalPrefab;
    public Transform pos1;

    [Header("Maze Settings")]
    public float CellWidth = 5;
    public float CellHeight = 5;
    public bool AddGaps = true;
    public int MinPathLength = 8;
    private Vector2Int _mazeSize = new Vector2Int(8, 5);
    private MazeCell[,] maze;

    private class CellVisuals {
        public GameObject FloorObj;
        public List<GameObject> WallObjs = new List<GameObject>();
        public List<GameObject> ContentObjs = new List<GameObject>();

        public void SetVisible(bool state) {
            SetMesh(FloorObj, state);
            foreach (var w in WallObjs) SetMesh(w, state);
            foreach (var c in ContentObjs) SetMesh(c, state);
        }

        void SetMesh(GameObject go, bool state) {
            if (!go) return;
            var r = go.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < r.Length; i++) r[i].enabled = state;
        }
    }

    private CellVisuals[,] _cellVisualsGrid;
    private enum Direction { Right, Front, Left, Back }

    private struct MazeCell {
        public bool Visited;
        public bool WallRight, WallFront, WallLeft, WallBack;
        public bool IsGoal;
    }

    void Awake() {
        Instance = this;
        if (manager == null) manager = new ServerDataManager();
    }

    private void Start() {
        GenerateAndSpawnAll();
        if (enableFogOfWar && _cellVisualsGrid != null) HideAllMaze();
    }

    private void Update() {
        if (playerTransform == null) {
            if (PlayerMovement.instance != null) playerTransform = PlayerMovement.instance.transform;
            else return;
        }

        if (enableFogOfWar && _cellVisualsGrid != null) UpdateVisibility();
        else if (!enableFogOfWar && _cellVisualsGrid != null) {
            if (Time.frameCount % 10 == 0) ShowAllMaze();
        }
    }

    void HideAllMaze() {
        for (int r = 0; r < _mazeSize.x; r++)
            for (int c = 0; c < _mazeSize.y; c++)
                _cellVisualsGrid[r, c].SetVisible(false);
    }

    void ShowAllMaze() {
        for (int r = 0; r < _mazeSize.x; r++)
            for (int c = 0; c < _mazeSize.y; c++)
                _cellVisualsGrid[r, c].SetVisible(true);
    }

    void UpdateVisibility() {
        HideAllMaze();

        Vector3 playerLocPos = pos1.InverseTransformPoint(playerTransform.position);
        float gap = AddGaps ? 0.2f : 0f;
        float stepX = CellWidth + gap;
        float stepZ = CellHeight + gap;

        int playerGridC = Mathf.RoundToInt(playerLocPos.x / stepX);
        int playerGridR = Mathf.RoundToInt(playerLocPos.z / stepZ);

        Vector3 origin = playerTransform.position + Vector3.up * 1;
        int rad = Mathf.CeilToInt(viewRadius);

        int rMin = Mathf.Max(0, playerGridR - rad);
        int rMax = Mathf.Min(_mazeSize.x - 1, playerGridR + rad);
        int cMin = Mathf.Max(0, playerGridC - rad);
        int cMax = Mathf.Min(_mazeSize.y - 1, playerGridC + rad);

        for (int r = rMin; r <= rMax; r++) {
            for (int c = cMin; c <= cMax; c++) {
                float dist = Vector2.Distance(new Vector2(r, c), new Vector2(playerGridR, playerGridC));
                if (dist > viewRadius) continue;

                float targetX = c * stepX;
                float targetZ = r * stepZ;
                Vector3 targetLocal = new Vector3(targetX, 0f, targetZ);
                Vector3 targetWorld = pos1.TransformPoint(targetLocal) + Vector3.up * 1;

                Vector3 dir = targetWorld - origin;
                float rayDist = dir.magnitude;

                if (!Physics.Raycast(origin, dir.normalized, rayDist - 0.5f, obstructionLayer))
                    _cellVisualsGrid[r, c].SetVisible(true);
            }
        }
    }

    void GenerateAndSpawnAll() {
        GenerateAndSpawnMaze(manager.seed, pos1, _mazeSize);
    }

    void GenerateAndSpawnMaze(int baseSeed, Transform parent, Vector2Int size) {
        int currentSeed = baseSeed;
        int attempts = 0;
        int maxAttempts = 100;
        bool validMazeFound = false;

        Vector2Int startPos = new Vector2Int(3, 0);
        Vector2Int goalPos = new Vector2Int(4, 0);

        while (!validMazeFound && attempts < maxAttempts) {
            attempts++;
            Random.InitState(currentSeed);

            maze = new MazeCell[size.x, size.y];
            _cellVisualsGrid = new CellVisuals[size.x, size.y];

            for (int r = 0; r < size.x; r++) {
                for (int c = 0; c < size.y; c++) {
                    maze[r, c] = new MazeCell {
                        WallRight = true,
                        WallLeft = true,
                        WallFront = true,
                        WallBack = true,
                        Visited = false
                    };
                    _cellVisualsGrid[r, c] = new CellVisuals();
                }
            }

            maze[startPos.x, startPos.y].WallLeft = false;
            maze[goalPos.x, goalPos.y].WallLeft = false;
            maze[goalPos.x, goalPos.y].IsGoal = true;

            GenerateMazeData(startPos, goalPos, size);
            int pathLength = CalculatePathLength(startPos, goalPos, size);

            if (pathLength >= MinPathLength) validMazeFound = true;
            else currentSeed++;
        }

        SpawnMaze(parent, size);
    }

    void GenerateMazeData(Vector2Int startPos, Vector2Int goalPos, Vector2Int size) {
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        maze[startPos.x, startPos.y].Visited = true;
        stack.Push(startPos);

        while (stack.Count > 0) {
            Vector2Int current = stack.Peek();
            List<(Direction, Vector2Int)> neighbors = new();

            foreach (var (dir, offset) in new[]{
                (Direction.Right, new Vector2Int(0, 1)),
                (Direction.Front, new Vector2Int(1, 0)),
                (Direction.Left, new Vector2Int(0, -1)),
                (Direction.Back, new Vector2Int(-1, 0))
            }) {
                Vector2Int neighbor = current + offset;
                if (IsInside(neighbor, size) && !maze[neighbor.x, neighbor.y].Visited) {
                    bool isDirectConnection = (current == startPos && neighbor == goalPos) || (current == goalPos && neighbor == startPos);
                    if (!isDirectConnection) neighbors.Add((dir, neighbor));
                }
            }

            if (neighbors.Count > 0) {
                var index = Random.Range(0, neighbors.Count);
                var (chosenDir, next) = neighbors[index];
                RemoveWall(current, next, chosenDir);
                maze[next.x, next.y].Visited = true;
                stack.Push(next);
            }
            else stack.Pop();
        }
    }

    int CalculatePathLength(Vector2Int start, Vector2Int end, Vector2Int size) {
        bool[,] visitedBFS = new bool[size.x, size.y];
        Queue<(Vector2Int pos, int dist)> queue = new Queue<(Vector2Int, int)>();

        queue.Enqueue((start, 0));
        visitedBFS[start.x, start.y] = true;

        while (queue.Count > 0) {
            var (current, dist) = queue.Dequeue();
            if (current == end) return dist;

            if (!maze[current.x, current.y].WallRight) CheckNeighbor(current + new Vector2Int(0, 1), dist, size, visitedBFS, queue);
            if (!maze[current.x, current.y].WallFront) CheckNeighbor(current + new Vector2Int(1, 0), dist, size, visitedBFS, queue);
            if (!maze[current.x, current.y].WallLeft) CheckNeighbor(current + new Vector2Int(0, -1), dist, size, visitedBFS, queue);
            if (!maze[current.x, current.y].WallBack) CheckNeighbor(current + new Vector2Int(-1, 0), dist, size, visitedBFS, queue);
        }
        return 0;
    }

    void CheckNeighbor(Vector2Int next, int dist, Vector2Int size, bool[,] visited, Queue<(Vector2Int, int)> queue) {
        if (IsInside(next, size) && !visited[next.x, next.y]) {
            visited[next.x, next.y] = true;
            queue.Enqueue((next, dist + 1));
        }
    }

    bool IsInside(Vector2Int p, Vector2Int size) {
        return p.x >= 0 && p.x < size.x && p.y >= 0 && p.y < size.y;
    }

    void RemoveWall(Vector2Int a, Vector2Int b, Direction dir) {
        switch (dir) {
            case Direction.Right: maze[a.x, a.y].WallRight = false; maze[b.x, b.y].WallLeft = false; break;
            case Direction.Front: maze[a.x, a.y].WallFront = false; maze[b.x, b.y].WallBack = false; break;
            case Direction.Left: maze[a.x, a.y].WallLeft = false; maze[b.x, b.y].WallRight = false; break;
            case Direction.Back: maze[a.x, a.y].WallBack = false; maze[b.x, b.y].WallFront = false; break;
        }
    }

    void SpawnMaze(Transform parent, Vector2Int size) {
        foreach (Transform child in parent) Destroy(child.gameObject);

        float gap = AddGaps ? 0.2f : 0f;

        GameObject floorPlane = Instantiate(Floor, parent);
        floorPlane.transform.localPosition = new Vector3((size.y - 1) * (CellWidth + gap) / 2f, 0f, (size.x - 1) * (CellHeight + gap) / 2f);
        floorPlane.transform.localScale = new Vector3((size.y * (CellWidth + gap)) / 10f, 1f, (size.x * (CellHeight + gap)) / 10f);

        for (int r = 0; r < size.x; r++) {
            for (int c = 0; c < size.y; c++) {
                float x = c * (CellWidth + gap);
                float z = r * (CellHeight + gap);
                MazeCell cell = maze[r, c];
                CellVisuals visuals = _cellVisualsGrid[r, c];

                if (cell.WallRight && c != size.y - 1) {
                    GameObject w = Instantiate(Wall, parent);
                    w.transform.localPosition = new Vector3(x + CellWidth / 2f, 0f, z);
                    w.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    visuals.WallObjs.Add(w);
                }

                if (cell.WallFront && r != size.x - 1) {
                    GameObject w = Instantiate(Wall, parent);
                    w.transform.localPosition = new Vector3(x, 0f, z + CellHeight / 2f);
                    w.transform.localRotation = Quaternion.identity;
                    visuals.WallObjs.Add(w);
                }

                if (c == 0 && cell.WallLeft) {
                    GameObject w = Instantiate(Wall, parent);
                    w.transform.localPosition = new Vector3(x - CellWidth / 2f, 0f, z);
                    w.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    visuals.WallObjs.Add(w);
                }

                if (r == 0 && cell.WallBack) {
                    GameObject w = Instantiate(Wall, parent);
                    w.transform.localPosition = new Vector3(x, 0f, z - CellHeight / 2f);
                    w.transform.localRotation = Quaternion.identity;
                    visuals.WallObjs.Add(w);
                }

                if (cell.IsGoal && GoalPrefab != null) {
                    GameObject g = Instantiate(GoalPrefab, parent);
                    g.transform.localPosition = new Vector3(x, 0.1f, z);
                    g.transform.localRotation = Quaternion.identity;
                    visuals.ContentObjs.Add(g);
                }
            }
        }

        if (Pillar != null) {
            for (int r = 0; r <= size.x; r++) {
                for (int c = 0; c <= size.y; c++) {
                    float x = c * (CellWidth + gap) - CellWidth / 2f;
                    float z = r * (CellHeight + gap) - CellHeight / 2f;
                    GameObject p = Instantiate(Pillar, parent);
                    p.transform.localPosition = new Vector3(x, 0f, z);
                    p.transform.localRotation = Quaternion.identity;

                    int bindR = Mathf.Clamp(r, 0, size.x - 1);
                    int bindC = Mathf.Clamp(c, 0, size.y - 1);
                    _cellVisualsGrid[bindR, bindC].ContentObjs.Add(p);
                }
            }
        }
    }
}
