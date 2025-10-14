using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

[Serializable]
public class ServerData {
	public string name;
	public string uid;
	public int curr_users;
	public int max_users;
	public int seed;
	public bool is_favorite;
	public int mapSizeX, mapSizeY;
}

[Serializable]
public struct NetMessage {
	[JsonProperty("status")] public string Status;
	[JsonProperty("cmd")] public string Cmd;
	[JsonProperty("room_id")] public string RoomId;
	[JsonProperty("username")] public string Username;
	[JsonProperty("user_id")] public string UserId;
	[JsonProperty("data")] public Dictionary<string, object> Data;
	[JsonProperty("rooms")] public Dictionary<string, object> Rooms;
	[JsonProperty("conf")] public Dictionary<string, object> Conf;
	[JsonProperty("error")] public string Error;
}

public enum NetCmd { CreateRoom, JoinRoom, LeaveRoom, GetRooms }

public class BasicNetworkManager : MonoBehaviour {
	string currentRoomId;
	public InputField server_seed;
	public GameObject serverEntryPrefab;
	public Transform contentParent;
	public Dictionary<string, ServerData> serverDict = new();

	static readonly Dictionary<NetCmd, string> CmdMap = new() {
		{ NetCmd.LeaveRoom, "leave_room" },
		{ NetCmd.GetRooms, "get_rooms" },
		{ NetCmd.CreateRoom, "create_room" },
		{ NetCmd.JoinRoom, "join_room" }
	};

	void Start() => LogInto();

	void LogInto() {
		AuthManager.Instance.Authenticate(
			SystemInfo.deviceUniqueIdentifier.Substring(3, 12),
			SystemInfo.deviceUniqueIdentifier.Substring(0, 10),
			SystemInfo.deviceUniqueIdentifier,
			Application.version,
			resp => {
				AuthManager.Instance.ConnectWebSocket(
					onOpen: () => { },
					onMessage: OnMessage,
					onError: err => Debug.LogError("Auth WS Error: " + err),
					onClose: code => Debug.Log("WS closed: " + code)
				);
			},
			err => Debug.LogError("Auth failed: " + err)
		);
	}

	void OnMessage(string msg) {
		var parsed = JsonConvert.DeserializeObject<NetMessage>(msg);
		var status = (parsed.Status ?? "").ToLower();

		switch (status) {
			case "connected":
				SendCommand(NetCmd.GetRooms);
				break;

			case "success":
				if (parsed.Cmd == "create_room") {
					currentRoomId = parsed.RoomId;
					Debug.Log("Room created: " + currentRoomId);
					SendCommand(NetCmd.JoinRoom, new { room_id = currentRoomId });
				}
				else if (parsed.Cmd == "join_room") {
					currentRoomId = parsed.RoomId;
					Debug.Log("Joined room: " + currentRoomId);

					if (parsed.Conf != null) {
						int seed = parsed.Conf.ContainsKey("seed") ? Convert.ToInt32(parsed.Conf["seed"]) : 0;
						int max = parsed.Conf.ContainsKey("max_players") ? Convert.ToInt32(parsed.Conf["max_players"]) : 10;
						MazeGame.manager = new ServerDataManager {
							seed = seed,
							mapSizeX = max * 5,
							mapSizeY = max * 5,
							serverAddress = currentRoomId
						};
						UnityEngine.SceneManagement.SceneManager.LoadScene("MazeScene");
					}
				}
				else if (parsed.Cmd == "get_rooms" && parsed.Rooms != null) {
					var serverList = new List<ServerData>();
					foreach (var kv in parsed.Rooms) {
						var roomId = kv.Key;
						var roomJson = kv.Value.ToString();
						var roomDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(roomJson);
						var confJson = roomDict.ContainsKey("match_conf") ? roomDict["match_conf"].ToString() : "{}";
						var conf = JsonConvert.DeserializeObject<Dictionary<string, object>>(confJson);
						var playersJson = roomDict.ContainsKey("active_players") ? roomDict["active_players"].ToString() : "{}";
						var players = JsonConvert.DeserializeObject<Dictionary<string, object>>(playersJson);
						serverList.Add(new ServerData {
							name = roomId,
							uid = roomId,
							curr_users = players.Count,
							max_users = conf.ContainsKey("max_players") ? Convert.ToInt32(conf["max_players"]) : 0,
							seed = conf.ContainsKey("seed") ? Convert.ToInt32(conf["seed"]) : 0,
							mapSizeX = 10,
							mapSizeY = 10
						});
					}
					PopulateServerList(serverList.ToArray());
				}
				break;

			case "room_deleted":
				Debug.Log("Room deleted: " + parsed.RoomId);
				if (parsed.RoomId == currentRoomId) currentRoomId = null;
				SendCommand(NetCmd.GetRooms);
				break;

			case "error":
				Debug.LogError("Server error: " + parsed.Error);
				break;
		}
	}

	public void SendCommand(NetCmd cmd, object extra = null) {
		if (!CmdMap.ContainsKey(cmd)) return;
		var payload = new Dictionary<string, object> { { "cmd", CmdMap[cmd] } };
		if (extra != null) payload["data"] = extra;
		AuthManager.Instance.SendWSMsg(JsonConvert.SerializeObject(payload));
	}

	public void CreateServerRequest() {
		if (!AuthManager.Instance.IsSocketActive) return;
		if (server_seed.text.Length <= 0) return;
		int.TryParse(server_seed.text, out int parsedSeed);
		var payload = new {
			seed = parsedSeed
		};
		SendCommand(NetCmd.CreateRoom, payload);
	}

	public void LeaveCurrentRoom() {
		if (!AuthManager.Instance.IsSocketActive) return;
		if (string.IsNullOrEmpty(currentRoomId)) return;
		SendCommand(NetCmd.LeaveRoom, new { room_id = currentRoomId });
		currentRoomId = null;
	}

	public void RefreshRooms() => SendCommand(NetCmd.GetRooms);

	void PopulateServerList(ServerData[] servers) {
		foreach (Transform child in contentParent) Destroy(child.gameObject);
		if (servers != null && servers.Length > 0) {
			serverDict.Clear();
			foreach (var s in servers) {
				serverDict[s.uid] = s;
				var e = Instantiate(serverEntryPrefab, contentParent).transform;
				e.GetChild(1).GetComponent<Text>().text = s.name;
				e.GetChild(2).GetComponent<Text>().text = $"{s.curr_users}/{s.max_users}";
				string uid = s.uid;
				e.GetChild(3).GetComponent<Button>().onClick.AddListener(() => ConnectToServer(uid));
			}
		}
		else {
			var e = Instantiate(serverEntryPrefab, contentParent).transform;
			e.GetChild(0).gameObject.SetActive(false);
			e.GetChild(1).GetComponent<Text>().text = "Empty server list!";
			e.GetChild(2).GetComponent<Text>().text = "";
		}
	}

	public void ConnectToServer(string serv_uid) {
		if (serverDict.TryGetValue(serv_uid, out var server)) {
			currentRoomId = serv_uid;
			SendCommand(NetCmd.JoinRoom, new { room_id = serv_uid });
		}
	}
}
