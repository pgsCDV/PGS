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

        if (players.ContainsKey(uid)) DespawnPlayer(uid);

        GameObject go = Instantiate(PlayerPrefab, spawn.position, Quaternion.identity);
        go.name = uid;
        go.GetComponent<PlayerNetwork>().IsLocal = true;

        CameraSingletone.instance.transform.position = go.transform.position;
        CameraSingletone.instance.transform.SetParent(go.transform);

        players[uid] = go;
    }

    public void SpawnRemotePlayer(string uid, int side) {
        if (players.ContainsKey(uid)) DespawnPlayer(uid);

        Transform spawn = side == 1 ? Spawn1 : Spawn2;
        GameObject go = Instantiate(PlayerPrefab, spawn.position, Quaternion.identity);
        go.name = uid;
        go.GetComponent<PlayerNetwork>().IsLocal = false;

        players[uid] = go;
    }

    public void DespawnPlayer(string uid) {
        if (!players.ContainsKey(uid)) return;
        Destroy(players[uid]);
        players.Remove(uid);
    }

    public void DespawnAll() {
        foreach (var kv in new Dictionary<string, GameObject>(players)) {
            DespawnPlayer(kv.Key);
        }
    }
}
