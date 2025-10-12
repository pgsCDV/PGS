using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System;
using System.Linq;
using Sirenix.Serialization;

[System.Serializable]
public class ServerData {
    public string name;
    public string uid;
    public int curr_users;
    public int max_users;
    public int seed;
    public bool is_favorite;
    public int mapSizeX, mapSizeY;
}

[System.Serializable]
public struct WsMessage<T> {
	[JsonProperty("status")]
	public string StatusString { get; set; }

	[JsonIgnore]
	public WSType Status {
		get {
			if (Enum.TryParse(StatusString, true, out WSType result))
				return result;
			return WSType.Unknown;
		}
	}

	[JsonIgnore]
	public WSCmd Cmd { get; set; }

	[JsonProperty("room_id")]
	public string RoomId { get; set; }

	[JsonProperty("username")]
	public string Username { get; set; }

	[JsonProperty("user_id")]
	public string UserId { get; set; }

	[JsonProperty("data")]
	public T Data { get; set; }

	[JsonProperty("rooms")]
	public Dictionary<string, object> Rooms { get; set; }

	[JsonProperty("conf")]
	public Dictionary<string, object> Conf { get; set; }
}

public class BasicNetworkManager : MonoBehaviour {
	string currentRoomId;
	private InputSystem_Actions inputActions;

	static readonly Dictionary<WSCmd, string> CmdMap = new Dictionary<WSCmd, string> {
		{ WSCmd.LeaveRoom, "leave_room" },
		{ WSCmd.GetMe, "get_me" },
		{ WSCmd.CreateRoom, "create_room" },
		{ WSCmd.GetRooms, "get_rooms" }
	};
	public InputField server_name;
	public Slider server_slots;
	public InputField server_password;
	public Dropdown server_map_type;
	public InputField server_seed;


    public GameObject serverEntryPrefab;
    public Transform contentParent;

    public ServerData[] allServers;
    [OdinSerialize] public Dictionary<string, ServerData> serverDict;

    private void Awake() {        
		inputActions = new InputSystem_Actions();
	}
	void Start() => LogInto();

	private void OnEnable() {
		inputActions.Player.Sprint.performed += OnSprintPerf;
		inputActions.Player.Enable();
	}

	private void OnDisable() {
		inputActions.Player.Sprint.performed -= OnSprintPerf;
		inputActions.Player.Disable();
	}

	void SendCommand(WSCmd cmd, object extraFields = null) {
		if (!CmdMap.ContainsKey(cmd)) return;
		var baseMsg = new Dictionary<string, object> { { "cmd", CmdMap[cmd] } };
		if (extraFields != null) {
			foreach (var field in extraFields.GetType().GetFields()) {
				baseMsg[field.Name] = field.GetValue(extraFields);
			}
			foreach (var prop in extraFields.GetType().GetProperties()) {
				if (prop.CanRead)
					baseMsg[prop.Name] = prop.GetValue(extraFields);
			}
		}
		AuthManager.Instance.SendWSMsg(JsonConvert.SerializeObject(baseMsg));
	}

	void OnSprintPerf(InputAction.CallbackContext context) {
		SendCommand(WSCmd.LeaveRoom, new { RoomId = currentRoomId });
	}

	async void LogInto() {
		AuthManager.Instance.Authenticate(
			SystemInfo.deviceUniqueIdentifier.Substring(3, 12),
			SystemInfo.deviceUniqueIdentifier.Substring(0, 10),
			SystemInfo.deviceUniqueIdentifier,
			Application.version,
			resp => {
				Debug.Log($"Logged in! user_id={resp.data.user_id}, token={resp.data.token}");

				AuthManager.Instance.ConnectWebSocket(
					onOpen: () => {
						//Debug.Log("WS opened");
					},
					onMessage: msg => {
						//Debug.Log($"new msg: [{Time.frameCount}] {Time.time} " + msg);

						var parsed = JsonConvert.DeserializeObject<WsMessage<Dictionary<string, object>>>(msg);
						switch (parsed.Status) {
							case WSType.Connected:
								SendCommand(WSCmd.GetMe);
								SendCommand(WSCmd.CreateRoom);
								break;
							case WSType.Success:
								if (!string.IsNullOrEmpty(parsed.RoomId)) {
									currentRoomId = parsed.RoomId;
									Debug.Log("Room created, id = " + currentRoomId);
									SendCommand(WSCmd.GetRooms);
								}
								break;
							case WSType.ConnectCallback:
								break;
							default:
								break;
						}
					},
					onError: err => {
						Debug.LogError("Error WS: " + err);
					},
					onClose: code => {
						Debug.Log("WS closed: " + code);
					}
				);
			},
			err => {
				Debug.LogError("Auth failed: " + err);
			}
		);
	}
	public void CreateServerRequest() {
		if (!AuthManager.Instance.IsSocketActive) return;
		if (server_name.text.Length <= 0) return;
		if (server_seed.text.Length <= 0) return;

		var payload = new {
			cmd = CmdMap[WSCmd.CreateRoom],
			name = server_name.text,
			max_users = Mathf.RoundToInt(server_slots.value),
			password = server_password.text,
			seed = int.TryParse(server_seed.text, out int parsedSeed) ? parsedSeed : 0
		};

		string json = JsonConvert.SerializeObject(payload);
		server_name.text = "";
		//server_seed.text = "";
		AuthManager.Instance.SendWSMsg(json);
    }
    public void ConnectToServer(string serv_uid) {
        if (serverDict != null && serverDict.TryGetValue(serv_uid, out var server)) {
            MazeGame.manager.seed = server.seed;
            MazeGame.manager.mapSizeX = server.mapSizeX;
            MazeGame.manager.mapSizeY = server.mapSizeY;
            //SendWSRequest($"connect_{serv_uid}");
        }
    }

    void PopulateServerList(ServerData[] servers) {

        foreach (Transform child in contentParent) Destroy(child.gameObject);

        if (servers?.Length > 0) {
            serverDict = servers.ToDictionary(s => s.uid, s => s);
            foreach (var s in serverDict.Values) {
                var e = Instantiate(serverEntryPrefab, contentParent).transform;
                //e.GetChild(0).GetComponent<Image>().sprite = s.pass ? passwordOnSprite : passwordOffSprite;
                e.GetChild(1).GetComponent<Text>().text = s.name;
                e.GetChild(2).GetComponent<Text>().text = $"{s.curr_users}/{s.max_users}";
                e.GetChild(3).GetComponent<Button>().onClick.AddListener(() => ConnectToServer(s.uid));
            }
        }
        else {
            var e = Instantiate(serverEntryPrefab, contentParent).transform;
            e.GetChild(0).GetComponent<Image>().gameObject.SetActive(false);
            e.GetChild(1).GetComponent<Text>().text = "Empty server list!";
            e.GetChild(2).GetComponent<Text>().text = "";
            e.GetChild(2).GetChild(0).GetComponent<Text>().text = "";
        }
    }

}
