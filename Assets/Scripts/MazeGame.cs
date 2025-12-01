using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ServerDataManager {
    public int seed;
    public string serverAddress;
}

public class MazeGame : MonoBehaviour {

    public static MazeGame Instance;
    public static ServerDataManager manager;

    [Header("Maze Settings")]
    public float CellWidth = 5;
    public float CellHeight = 5;
    public bool AddGaps = true;

    public int MinPathLength = 8; 

    [Header("Prefabs")]
    public GameObject Floor, Wall, Pillar, GoalPrefab;

    [Header("Spawn Positions")]
    public Transform pos1;

    private Vector2Int _mazeSize = new Vector2Int(8, 5);

    private MazeCell[,] maze;
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
            for (int r = 0; r < size.x; r++) {
                for (int c = 0; c < size.y; c++) {
                    maze[r, c] = new MazeCell {
                        WallRight = true,
                        WallLeft = true,
                        WallFront = true,
                        WallBack = true,
                        Visited = false
                    };
                }
            }

            maze[startPos.x, startPos.y].WallLeft = false;
            maze[goalPos.x, goalPos.y].WallLeft = false;
            maze[goalPos.x, goalPos.y].IsGoal = true;

            GenerateMazeData(startPos, goalPos, size);

            int pathLength = CalculatePathLength(startPos, goalPos, size);

            if (pathLength >= MinPathLength) {
                validMazeFound = true;
                Debug.Log($"Лабиринт найден! Попытка: {attempts}, Длина пути: {pathLength}");
            }
            else {
                currentSeed++;
            }
        }

        if (!validMazeFound) {
            Debug.LogWarning($"Не удалось найти лабиринт с длиной пути {MinPathLength} за {maxAttempts} попыток. Генерирую последний вариант.");
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
                    bool isDirectConnection = (current == startPos && neighbor == goalPos) ||
                                              (current == goalPos && neighbor == startPos);

                    if (!isDirectConnection) {
                        neighbors.Add((dir, neighbor));
                    }
                }
            }

            if (neighbors.Count > 0) {
                var index = Random.Range(0, neighbors.Count);
                var (chosenDir, next) = neighbors[index];
                RemoveWall(current, next, chosenDir);
                maze[next.x, next.y].Visited = true;
                stack.Push(next);
            }
            else {
                stack.Pop();
            }
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

            // Right
            if (!maze[current.x, current.y].WallRight) {
                Vector2Int next = current + new Vector2Int(0, 1);
                if (IsInside(next, size) && !visitedBFS[next.x, next.y]) {
                    visitedBFS[next.x, next.y] = true;
                    queue.Enqueue((next, dist + 1));
                }
            }
            // Front
            if (!maze[current.x, current.y].WallFront) {
                Vector2Int next = current + new Vector2Int(1, 0);
                if (IsInside(next, size) && !visitedBFS[next.x, next.y]) {
                    visitedBFS[next.x, next.y] = true;
                    queue.Enqueue((next, dist + 1));
                }
            }
            // Left
            if (!maze[current.x, current.y].WallLeft) {
                Vector2Int next = current + new Vector2Int(0, -1);
                if (IsInside(next, size) && !visitedBFS[next.x, next.y]) {
                    visitedBFS[next.x, next.y] = true;
                    queue.Enqueue((next, dist + 1));
                }
            }
            // Back
            if (!maze[current.x, current.y].WallBack) {
                Vector2Int next = current + new Vector2Int(-1, 0);
                if (IsInside(next, size) && !visitedBFS[next.x, next.y]) {
                    visitedBFS[next.x, next.y] = true;
                    queue.Enqueue((next, dist + 1));
                }
            }
        }
        return 0;
    }

    bool IsInside(Vector2Int p, Vector2Int size) {
        return p.x >= 0 && p.x < size.x && p.y >= 0 && p.y < size.y;
    }

    void RemoveWall(Vector2Int a, Vector2Int b, Direction dir) {
        switch (dir) {
            case Direction.Right:
                maze[a.x, a.y].WallRight = false;
                maze[b.x, b.y].WallLeft = false;
                break;
            case Direction.Front:
                maze[a.x, a.y].WallFront = false;
                maze[b.x, b.y].WallBack = false;
                break;
            case Direction.Left:
                maze[a.x, a.y].WallLeft = false;
                maze[b.x, b.y].WallRight = false;
                break;
            case Direction.Back:
                maze[a.x, a.y].WallBack = false;
                maze[b.x, b.y].WallFront = false;
                break;
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

                if (cell.WallRight || c == size.y - 1) {
                    if (cell.WallRight) {
                        GameObject w = Instantiate(Wall, parent);
                        w.transform.localPosition = new Vector3(x + CellWidth / 2f, 0f, z);
                        w.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    }
                }

                if (cell.WallFront || r == size.x - 1) {
                    if (cell.WallFront) {
                        GameObject w = Instantiate(Wall, parent);
                        w.transform.localPosition = new Vector3(x, 0f, z + CellHeight / 2f);
                        w.transform.localRotation = Quaternion.identity;
                    }
                }

                if (c == 0 && cell.WallLeft) {
                    GameObject w = Instantiate(Wall, parent);
                    w.transform.localPosition = new Vector3(x - CellWidth / 2f, 0f, z);
                    w.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                }

                if (r == 0 && cell.WallBack) {
                    GameObject w = Instantiate(Wall, parent);
                    w.transform.localPosition = new Vector3(x, 0f, z - CellHeight / 2f);
                    w.transform.localRotation = Quaternion.identity;
                }

                if (cell.IsGoal && GoalPrefab != null) {
                    GameObject g = Instantiate(GoalPrefab, parent);
                    g.transform.localPosition = new Vector3(x, 0.1f, z);
                    g.transform.localRotation = Quaternion.identity;
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
                }
            }
        }
    }
}