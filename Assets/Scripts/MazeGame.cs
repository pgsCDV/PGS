using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ServerDataManager {
	public int seed;
	public int mapSizeX, mapSizeY;
	public string serverAddress;
}

public class MazeGame : MonoBehaviour {

	public static MazeGame Instance;
	public static ServerDataManager manager;
	[Header("Maze Settings")]
	public float CellWidth = 5;
	public float CellHeight = 5;
	public bool AddGaps = true;

	[Header("Prefabs")]
	public GameObject Floor, Wall, Pillar, GoalPrefab;

	[Header("Spawn Positions")]
	public Transform pos1;
	public Transform pos2;
	public Transform pos3;

	private MazeCell[,] maze;
	int Rows;
	int Columns;

	private enum Direction { Right, Front, Left, Back }
	private struct MazeCell {
		public bool Visited;
		public bool WallRight, WallFront, WallLeft, WallBack;
		public bool IsGoal;
	}

	void Awake() {
		Instance = this;
		Rows = manager.mapSizeX;
		Columns = manager.mapSizeY;
	}

	private void Start() {
		GenerateAndSpawnAll();
	}

	void GenerateAndSpawnAll() {
		GenerateAndSpawnMaze(manager.seed, pos1);
		GenerateAndSpawnMaze(manager.seed + 1, pos2);
		GenerateAndSpawnMaze(manager.seed + 2, pos3);
	}

	void GenerateAndSpawnMaze(int seed, Transform parent) {
		Random.InitState(seed);
		maze = new MazeCell[Rows, Columns];

		for (int r = 0; r < Rows; r++) {
			for (int c = 0; c < Columns; c++) {
				maze[r, c] = new MazeCell {
					WallRight = true,
					WallLeft = true,
					WallFront = true,
					WallBack = true,
					Visited = false
				};
			}
		}

		Stack<Vector2Int> stack = new Stack<Vector2Int>();
		Vector2Int start = new Vector2Int(Random.Range(0, Rows), Random.Range(0, Columns));
		maze[start.x, start.y].Visited = true;
		stack.Push(start);

		while (stack.Count > 0) {
			Vector2Int current = stack.Peek();
			List<(Direction, Vector2Int)> unvisitedNeighbors = new();

			foreach (var (dir, offset) in new[]{
				(Direction.Right, new Vector2Int(0, 1)),
				(Direction.Front, new Vector2Int(1, 0)),
				(Direction.Left, new Vector2Int(0, -1)),
				(Direction.Back, new Vector2Int(-1, 0))
			}) {
				Vector2Int neighbor = current + offset;
				if (neighbor.x >= 0 && neighbor.x < Rows && neighbor.y >= 0 && neighbor.y < Columns &&
					!maze[neighbor.x, neighbor.y].Visited) {
					unvisitedNeighbors.Add((dir, neighbor));
				}
			}

			if (unvisitedNeighbors.Count > 0) {
				var (chosenDir, next) = unvisitedNeighbors[Random.Range(0, unvisitedNeighbors.Count)];
				RemoveWall(current, next, chosenDir);
				maze[next.x, next.y].Visited = true;
				stack.Push(next);
			}
			else {
				stack.Pop();
			}
		}

		var goal = new Vector2Int(Random.Range(0, Rows), Random.Range(0, Columns));
		maze[goal.x, goal.y].IsGoal = true;
		Random.InitState((int)System.DateTime.Now.Ticks);
		SpawnMaze(parent);
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

	void SpawnMaze(Transform parent) {
		float gap = AddGaps ? 0.2f : 0f;

		GameObject floorPlane = Instantiate(Floor, parent);
		floorPlane.transform.localPosition = new Vector3((Columns - 1) * (CellWidth + gap) / 2f, 0f, (Rows - 1) * (CellHeight + gap) / 2f);
		floorPlane.transform.localScale = new Vector3((Columns * (CellWidth + gap)) / 10f, 1f, (Rows * (CellHeight + gap)) / 10f);

		for (int row = 0; row < Rows; row++) {
			for (int col = 0; col < Columns; col++) {
				float x = col * (CellWidth + gap);
				float z = row * (CellHeight + gap);
				MazeCell cell = maze[row, col];

				if (cell.WallRight) {
					GameObject w = Instantiate(Wall, parent);
					w.transform.localPosition = new Vector3(x + CellWidth / 2f, 0f, z);
					w.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
				}

				if (cell.WallFront) {
					GameObject w = Instantiate(Wall, parent);
					w.transform.localPosition = new Vector3(x, 0f, z + CellHeight / 2f);
					w.transform.localRotation = Quaternion.identity;
				}

				if (cell.WallLeft && col == 0) {
					GameObject w = Instantiate(Wall, parent);
					w.transform.localPosition = new Vector3(x - CellWidth / 2f, 0f, z);
					w.transform.localRotation = Quaternion.Euler(0f, 270f, 0f);
				}

				if (cell.WallBack && row == 0) {
					GameObject w = Instantiate(Wall, parent);
					w.transform.localPosition = new Vector3(x, 0f, z - CellHeight / 2f);
					w.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
				}

				if (cell.IsGoal && GoalPrefab != null) {
					GameObject g = Instantiate(GoalPrefab, parent);
					g.transform.localPosition = new Vector3(x, 0.1f, z);
					g.transform.localRotation = Quaternion.identity;
				}
			}
		}

		if (Pillar != null) {
			for (int row = 0; row <= Rows; row++) {
				for (int col = 0; col <= Columns; col++) {
					float x = col * (CellWidth + gap) - CellWidth / 2f;
					float z = row * (CellHeight + gap) - CellHeight / 2f;
					GameObject p = Instantiate(Pillar, parent);
					p.transform.localPosition = new Vector3(x, 0f, z);
					p.transform.localRotation = Quaternion.identity;
				}
			}
		}
	}
}
