using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BasicNetworkManager : MonoBehaviour {
    string currentRoomId;
    public InputField seed;
    public Dropdown side;
    public GameObject serverEntryPrefab;
    public Transform contentParent;
    public Dictionary<string, ServerData> serverDict = new();

    static readonly Dictionary<WSCmd, string> CmdMap = new() {
        { WSCmd.LeaveRoom, "leave_room" },
        { WSCmd.GetRooms, "get_rooms" },
        { WSCmd.CreateRoom, "create_room" },
        { WSCmd.JoinRoom, "join_room" }
    };

    void Start() => LogInto();

    void LogInto() {
        AuthManager.Instance.Authenticate(
            SystemInfo.deviceUniqueIdentifier.Substring(3, 12) + Application.isEditor + Application.isPlaying,
            SystemInfo.deviceUniqueIdentifier.Substring(0, 10) + Application.isEditor,
            Application.version,
            resp => AuthManager.Instance.ConnectWebSocket(
                onOpen: () => { },
                onMessage: OnMessage,
                onError: err => Debug.LogError("Auth WS Error: " + err),
                onClose: code => {
                    if (!string.IsNullOrEmpty(currentRoomId)) {
                        AuthManager.Instance.SendWSMsg(
                            JsonConvert.SerializeObject(
                                new Dictionary<string, object> {
                                    { "cmd", "leave_room" },
                                    { "data", new Dictionary<string, object> { { "room_id", currentRoomId } } }
                                }
                            )
                        );
                        currentRoomId = null;
                    }
                }
            ),
            err => Debug.LogError("Auth failed: " + err)
        );
    }

    void OnMessage(string msg) {
        var parsed = JsonConvert.DeserializeObject<NetMessage>(msg);
        var status = (parsed.Status ?? "").ToLower();

        if (status == "connected") {
            AuthManager.Instance.SetCurrentUsername(parsed.Username);
            SendCommand(WSCmd.GetRooms);
            return;
        }

        if (status == "ok") {
            switch (parsed.Action) {
                case "create_room":
                    currentRoomId = parsed.RoomId;
                    int joinSide = side != null ? Mathf.Clamp(side.value + 1, 1, 2) : 1;
                    SendCommand(WSCmd.JoinRoom, new { room_id = currentRoomId, side = joinSide });
                    break;

                case "join_room":
                    currentRoomId = parsed.RoomId;
                    Debug.Log("Joined room: " + currentRoomId);

                    int seedVal = 0;
                    List<(string uid, int side)> playersToSpawn = new();

                    if (parsed.Room != null) {
                        var roomObj = parsed.Room as Dictionary<string, object>;
                        seedVal = roomObj != null && roomObj.ContainsKey("seed") ? Convert.ToInt32(roomObj["seed"]) : 0;

                        if (roomObj != null && roomObj.ContainsKey("active_players")) {
                            var act = roomObj["active_players"] as JObject;
                            if (act != null) {
                                var dict = act.ToObject<Dictionary<string, object>>();
                                foreach (var kv in dict) {
                                    var meData = kv.Value as JObject;
                                    if (meData != null && meData.ContainsKey("uid") && meData.ContainsKey("side")) {
                                        string uid = meData["uid"].ToString();
                                        int pSide = meData["side"].ToObject<int>();
                                        if (uid == AuthManager.Instance.GetCurrentUserId()) {
                                            AuthManager.Instance.side = (short)pSide;
                                        }
                                        else {
                                            playersToSpawn.Add((uid, pSide));
                                        }
                                    }
                                }
                            }
                        }
                    }

                    MazeGame.manager = new ServerDataManager {
                        seed = seedVal,
                        serverAddress = currentRoomId
                    };

                    StartCoroutine(LoadRoomAndSpawnPlayers(playersToSpawn));
                    break;


                case "get_rooms":
                    if (parsed.Rooms != null) PopulateServerList(ParseRooms(parsed.Rooms));
                    break;
            }
        }
        else if (parsed.Event == "rooms_updated" && parsed.Rooms != null) {
            PopulateServerList(ParseRooms(parsed.Rooms));
        }
        else if (parsed.Event == "player_joined") {
            Debug.Log($"[RAW SPAWN] player_joined | UID: {parsed.UID} | Side: {parsed.Side}");
            PlayerSpawner.Instance.SpawnRemotePlayer(parsed.UID, parsed.Side);
        }
        else if (parsed.Event == "player_left") {
            Debug.Log($"[RAW DESPAWN] player_left | UID: {parsed.UID}");
            PlayerSpawner.Instance.DespawnPlayer(parsed.UID);
        }

        else if (status == "error") {
            Debug.LogError("Server error: " + parsed.Error);
        }
    }

    IEnumerator LoadRoomAndSpawnPlayers(List<(string uid, int side)> otherPlayers) {
        var asyncLoad = SceneManager.LoadSceneAsync("ROOM");
        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone) yield return null;
        PlayerSpawner.Instance.SpawnLocalPlayer();

        foreach (var p in otherPlayers) {
            PlayerSpawner.Instance.SpawnRemotePlayer(p.uid, p.side);
        }
    }

    ServerData[] ParseRooms(Dictionary<string, object> roomsJson) {
        var list = new List<ServerData>();
        if (roomsJson == null) return list.ToArray();

        foreach (var kv in roomsJson) {
            string roomId = kv.Key;
            Dictionary<string, object> roomObj = null;

            if (kv.Value is JObject jObj) roomObj = jObj.ToObject<Dictionary<string, object>>();
            else if (kv.Value is Dictionary<string, object> dict) roomObj = dict;

            if (roomObj == null) continue;

            int currUsers = roomObj.ContainsKey("curr_users") ? Convert.ToInt32(roomObj["curr_users"]) : 0;
            int maxUsers = roomObj.ContainsKey("max_users") ? Convert.ToInt32(roomObj["max_users"]) : 0;

            list.Add(new ServerData {
                uid = roomId,
                curr_users = currUsers,
                max_users = maxUsers
            });
        }

        return list.ToArray();
    }

    public void SendCommand(WSCmd cmd, object extra = null) {
        if (!CmdMap.TryGetValue(cmd, out var cmdStr)) return;
        var payload = new Dictionary<string, object> { { "cmd", cmdStr } };
        if (extra != null) payload["data"] = extra;
        AuthManager.Instance.SendWSMsg(JsonConvert.SerializeObject(payload));
    }

    public void CreateServerRequest() {
        if (!AuthManager.Instance.IsSocketActive || string.IsNullOrEmpty(seed.text)) return;
        int.TryParse(seed.text, out int parsedSeed);
        int sideVal = side != null ? Mathf.Clamp(side.value + 1, 1, 2) : 1;
        SendCommand(WSCmd.CreateRoom, new { seed = parsedSeed, side = sideVal });
    }

    public void LeaveCurrentRoom() {
        if (!AuthManager.Instance.IsSocketActive || string.IsNullOrEmpty(currentRoomId)) return;
        SendCommand(WSCmd.LeaveRoom, new { room_id = currentRoomId });
        currentRoomId = null;
    }

    public void RefreshRooms() => SendCommand(WSCmd.GetRooms);

    void PopulateServerList(ServerData[] servers) {
        if (currentRoomId != null) return;
        foreach (Transform child in contentParent) Destroy(child.gameObject);
        serverDict.Clear();

        if (servers == null || servers.Length == 0) {
            var e = Instantiate(serverEntryPrefab, contentParent).transform;
            e.GetChild(0).gameObject.SetActive(false);
            e.GetChild(1).GetComponent<Text>().text = "Empty server list!";
            e.GetChild(2).GetComponent<Text>().text = "";
            return;
        }

        foreach (var s in servers) {
            serverDict[s.uid] = s;
            var e = Instantiate(serverEntryPrefab, contentParent).transform;
            e.GetChild(1).GetComponent<Text>().text = s.uid;
            e.GetChild(2).GetComponent<Text>().text = $"{s.curr_users}/{s.max_users}";
            string uid = s.uid;
            e.GetChild(3).GetComponent<Button>().onClick.AddListener(() => ConnectToServer(uid));
        }
    }

    public void ConnectToServer(string serv_uid) {
        if (serverDict.TryGetValue(serv_uid, out var server)) {
            currentRoomId = serv_uid;
            SendCommand(WSCmd.JoinRoom, new { room_id = serv_uid });
        }
    }

    void OnApplicationQuit() {
        if (!string.IsNullOrEmpty(currentRoomId) && AuthManager.Instance.IsSocketActive) {
            SendCommand(WSCmd.LeaveRoom, new { room_id = currentRoomId });
            currentRoomId = null;
        }
    }
    void OnDestroy() {
        if (!string.IsNullOrEmpty(currentRoomId) && AuthManager.Instance.IsSocketActive) {
            SendCommand(WSCmd.LeaveRoom, new { room_id = currentRoomId });
            currentRoomId = null;
        }
    }
}
