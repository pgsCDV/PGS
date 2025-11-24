using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour {
	public static PlayerSpawner Instance;
	public GameObject PlayerPrefab;
	public Transform Spawn1;
	public Transform Spawn2;
	public Transform map1Spawn1;
	public Transform map1Spawn2;

	Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();

	void Awake() {
		Instance = this;
	}

	Transform GetSpawn(int side) {
		int loc = BasicNetworkManager.Instance.CurrentStartLocation;
		if (loc == 0) return side == 1 ? Spawn1 : Spawn2;
		if (loc == 1) return side == 1 ? map1Spawn1 : map1Spawn2;
		return side == 1 ? Spawn1 : Spawn2;
	}

	public void SpawnLocalPlayer() {
		string uid = AuthManager.Instance.GetCurrentUserId();
		int side = AuthManager.Instance.side;
		Transform spawn = GetSpawn(side) ?? transform;

		if (players.ContainsKey(uid)) DespawnPlayer(uid);

		GameObject go = Instantiate(PlayerPrefab, spawn.position, Quaternion.identity);
		go.name = uid;
		go.GetComponent<PlayerNetwork>().IsLocal = true;

		CameraSingletone.instance.transform.position = go.transform.position;
		CameraSingletone.instance.transform.SetParent(go.transform);
		PlayerSpawnerInternal.Register(uid);

		players[uid] = go;
	}

	public void SpawnRemotePlayer(string uid, int side) {
		if (players.ContainsKey(uid)) DespawnPlayer(uid);

		Transform spawn = GetSpawn(side) ?? transform;
		GameObject go = Instantiate(PlayerPrefab, spawn.position, Quaternion.identity);
		go.name = uid;
		go.GetComponent<PlayerNetwork>().IsLocal = false;
		PlayerSpawnerInternal.Register(uid);

		players[uid] = go;
	}

	public void DespawnPlayer(string uid) {
		if (!players.ContainsKey(uid)) return;
		Destroy(players[uid]);
		PlayerSpawnerInternal.Unregister(uid);

		players.Remove(uid);
	}

	public void DespawnAll() {
		foreach (var kv in new Dictionary<string, GameObject>(players)) {
			DespawnPlayer(kv.Key);
		}
	}

	public bool TrySetPlayerPosition(string uid, Vector3 pos) {
		if (string.IsNullOrEmpty(uid)) return false;
		if (!players.ContainsKey(uid)) return false;
		var go = players[uid];
		if (go == null) return false;

		var net = go.GetComponent<PlayerNetwork>();

		if (net != null && !net.IsLocal) {
			net.SetNetworkPosition(pos);
			return true;
		}

		if (net == null) {
			go.transform.position = pos;
			return true;
		}

		return false;
	}
}
