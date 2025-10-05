using Newtonsoft.Json;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class BasicNetworkManager : MonoBehaviour {
    string currentRoomId;
    private InputSystem_Actions inputActions;

    [System.Serializable]
    public class WsMessage<T> {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("cmd")]
        public string Cmd { get; set; }

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

    void Awake() {
        LogInto();
        inputActions = new InputSystem_Actions();
    }

    private void OnEnable() {
        inputActions.Player.Sprint.performed += OnSprintPerf;
        inputActions.Player.Enable();
    }

    private void OnDisable() {
        inputActions.Player.Sprint.performed -= OnSprintPerf;
        inputActions.Player.Disable();
    }

    void OnSprintPerf(InputAction.CallbackContext context) {
        var leave = new WsMessage<object> {
            Cmd = "leave_room",
            RoomId = currentRoomId
        };
        AuthManager.Instance.SendWebSocket(JsonConvert.SerializeObject(leave));
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
                        Debug.Log("WS opened, wait for confirm...");
                    },
                    onMessage: msg => {
                        Debug.Log($"new msg: [{Time.frameCount}] {Time.time} " + msg);

                        var parsed = JsonConvert.DeserializeObject<WsMessage<Dictionary<string, object>>>(msg);

                        if (parsed.Status == "connected") {
                            AuthManager.Instance.SendWebSocket(JsonConvert.SerializeObject(new WsMessage<object> { Cmd = "get_me" }));
                            AuthManager.Instance.SendWebSocket(JsonConvert.SerializeObject(new WsMessage<object> { Cmd = "create_room" }));
                        }
                        else if (parsed.Status == "success" && !string.IsNullOrEmpty(parsed.RoomId)) {
                            currentRoomId = parsed.RoomId;
                            Debug.Log("Room created, id = " + currentRoomId);
                            AuthManager.Instance.SendWebSocket(JsonConvert.SerializeObject(new WsMessage<object> { Cmd = "get_rooms" }));
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
}
