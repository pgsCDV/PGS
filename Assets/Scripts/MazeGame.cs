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

	private MazeCell[,] maze;
	int Rows = manager.mapSizeX;
	int Columns = manager.mapSizeY;

	private enum Direction { Right, Front, Left, Back }
	private struct MazeCell {
		public bool Visited;
		public bool WallRight, WallFront, WallLeft, WallBack;
		public bool IsGoal;
	}

	void Awake() {
		Instance = this;
		print($"{Rows} {Columns}");
	}
	private void Start() {
		GenerateMaze();
		SpawnMaze();
	}

	void GenerateMaze() {
		Random.InitState(manager.seed);
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

	void SpawnMaze() {
		float gap = AddGaps ? 0.2f : 0;

		GameObject floorPlane = Instantiate(Floor);
		floorPlane.transform.position = new Vector3((Columns - 1) * (CellWidth + gap) / 2, 0, (Rows - 1) * (CellHeight + gap) / 2);
		floorPlane.transform.localScale = new Vector3((Columns * (CellWidth + gap)) / 10f, 1, (Rows * (CellHeight + gap)) / 10f);

		for (int row = 0; row < Rows; row++) {
			for (int col = 0; col < Columns; col++) {
				float x = col * (CellWidth + gap);
				float z = row * (CellHeight + gap);
				MazeCell cell = maze[row, col];

				if (cell.WallRight)
					Instantiate(Wall, new Vector3(x + CellWidth / 2, 0, z), Quaternion.Euler(0, 90, 0), transform);

				if (cell.WallFront)
					Instantiate(Wall, new Vector3(x, 0, z + CellHeight / 2), Quaternion.identity, transform);

				if (cell.WallLeft && col == 0)
					Instantiate(Wall, new Vector3(x - CellWidth / 2, 0, z), Quaternion.Euler(0, 270, 0), transform);

				if (cell.WallBack && row == 0)
					Instantiate(Wall, new Vector3(x, 0, z - CellHeight / 2), Quaternion.Euler(0, 180, 0), transform);

				if (cell.IsGoal && GoalPrefab != null)
					Instantiate(GoalPrefab, new Vector3(x, 0.1f, z), Quaternion.identity, transform);
			}
		}

		if (Pillar != null) {
			for (int row = 0; row <= Rows; row++) {
				for (int col = 0; col <= Columns; col++) {
					float x = col * (CellWidth + gap) - CellWidth / 2;
					float z = row * (CellHeight + gap) - CellHeight / 2;
					Instantiate(Pillar, new Vector3(x, 0, z), Quaternion.identity, transform);
				}
			}
		}
	}

	public static Vector3 GetRandomEmptyCellWorldPositionStatic() {
		if (Instance == null || Instance.maze == null) return Vector3.zero;
		while (true) {
			int row = Random.Range(0, Instance.Rows);
			int col = Random.Range(0, Instance.Columns);
			if (Instance.maze[row, col].Visited) {
				float gap = Instance.AddGaps ? 0.2f : 0f;
				float x = col * (Instance.CellWidth + gap);
				float z = row * (Instance.CellHeight + gap);
				return new Vector3(x, 0.3f, z);
			}
		}
	}
}
