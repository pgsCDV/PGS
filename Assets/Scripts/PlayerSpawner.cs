using System.Collections.Generic;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour {
    public static PlayerSpawner Instance;
    public GameObject PlayerPrefab;
    public Transform Spawn1;
    public Transform Spawn2;

    Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();

    void Awake() {
        Instance = this;
    }

    public void SpawnLocalPlayer() {
        string uid = AuthManager.Instance.GetCurrentUserId();
        int side = AuthManager.Instance.side;
        Transform spawn = side == 1 ? Spawn1 : Spawn2;

        GameObject go = Instantiate(PlayerPrefab);
        go.name = uid;
        go.transform.position = spawn.position;

        go.GetComponent<PlayerNetwork>().IsLocal = true;

        CameraSingletone.instance.transform.position = go.transform.position;
        CameraSingletone.instance.transform.SetParent(go.transform);

        players[uid] = go;
    }

    public void SpawnRemotePlayer(string uid, int side) {
        if (players.ContainsKey(uid)) return;

        Transform spawn = side == 1 ? Spawn1 : Spawn2;

        GameObject go = Instantiate(PlayerPrefab);

        go.name = uid;
        go.transform.position = spawn.position;

        go.GetComponent<PlayerNetwork>().IsLocal = false;
        go.GetComponent<PlayerNetwork>().spawnTime = Time.time;

        players[uid] = go;
    }

    public void DespawnPlayer(string uid) {
        if (!players.ContainsKey(uid)) return;
        Destroy(players[uid]);
        players.Remove(uid);
    }
}
