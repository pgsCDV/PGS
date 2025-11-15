using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

public class BasicNetworkManager : MonoBehaviour {
    string currentRoomId;
    public InputField seed;
    public Dropdown order;
    public GameObject serverEntryPrefab;
    public Transform contentParent;
    public Dictionary<string, ServerData> serverDict = new();

    static readonly Dictionary<WSCmd, string> CmdMap = new()
    {
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
                onClose: code => Debug.Log("WS closed: " + code)
            ),
            err => Debug.LogError("Auth failed: " + err)
        );
    }

    void OnMessage(string msg) {
        var parsed = JsonConvert.DeserializeObject<NetMessage>(msg);
        var status = (parsed.Status ?? "").ToLower();

        if (status == "connected") {
            SendCommand(WSCmd.GetRooms);
            return;
        }

        if (status == "ok") {
            switch (parsed.Cmd) {
                case "create_room":
                    currentRoomId = parsed.RoomId;
                    Debug.Log("Room created: " + currentRoomId);
                    // ВНИМАНИЕ: Убрано автоподключение (авто-join). Теперь создание комнаты и подключение — отдельные действия.
                    // Если нужно — можно автоматически обновлять список или открыть UI для управления созданной комнатой.
                    break;

                case "join_room":
                    currentRoomId = parsed.RoomId;
                    Debug.Log("Joined room: " + currentRoomId);
                    if (parsed.Room?.ContainsKey("conf") == true) {
                        var conf = parsed.Room["conf"] as Dictionary<string, object>;
                        int seedVal = conf?.ContainsKey("seed") == true ? Convert.ToInt32(conf["seed"]) : 0;
                        int max = conf?.ContainsKey("max_players") == true ? Convert.ToInt32(conf["max_players"]) : 2;
                        MazeGame.manager = new ServerDataManager { seed = seedVal, serverAddress = currentRoomId };
                        UnityEngine.SceneManagement.SceneManager.LoadScene("ROOM");
                    }
                    break;

                case "get_rooms":
                    if (parsed.Rooms != null) PopulateServerList(ParseRooms(parsed.Rooms));
                    break;
            }
        }
        else if (parsed.Event == "rooms_updated" && parsed.Rooms != null) {
            PopulateServerList(ParseRooms(parsed.Rooms));
        }
        else if (parsed.Event == "player_left" || parsed.Event == "player_joined") {
            SendCommand(WSCmd.GetRooms); // refresh list
        }
        else if (status == "error") {
            Debug.LogError("Server error: " + parsed.Error);
        }
    }

    ServerData[] ParseRooms(Dictionary<string, object> roomsJson) {
        var list = new List<ServerData>();
        foreach (var kv in roomsJson) {
            var roomId = kv.Key;
            var roomObj = kv.Value as Dictionary<string, object>;
            var conf = roomObj?["conf"] as Dictionary<string, object> ?? new();
            var players = roomObj?["active_players"] as Dictionary<string, object> ?? new();
            list.Add(new ServerData {
                name = roomId,
                uid = roomId,
                curr_users = players.Count,
                max_users = conf.ContainsKey("max_players") ? Convert.ToInt32(conf["max_players"]) : 2,
                seed = conf.ContainsKey("seed") ? Convert.ToInt32(conf["seed"]) : 0
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
        SendCommand(WSCmd.CreateRoom, new { seed = parsedSeed, order = order.value });
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
            e.GetChild(1).GetComponent<Text>().text = s.name;
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
}
