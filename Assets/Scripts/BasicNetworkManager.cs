using Newtonsoft.Json;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class BasicNetworkManager : MonoBehaviour {
    string currentRoomId;

    private InputSystem_Actions inputActions;
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
        AuthManager.Instance.SendWebSocket(JsonConvert.SerializeObject(new { cmd = "leave_room", room_id = currentRoomId }));

    }
    async void LogInto() {
        AuthManager.Instance.Authenticate(
            SystemInfo.deviceUniqueIdentifier.Substring(3, 13),
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

                        if (msg.Contains("\"status\": \"connected\"")) {
                            AuthManager.Instance.SendWebSocket(JsonConvert.SerializeObject(new { cmd = "get_me" }));
                            AuthManager.Instance.SendWebSocket(JsonConvert.SerializeObject(new { cmd = "create_room" }));
                        }
                        else if (msg.Contains("\"room_id\"") && msg.Contains("\"status\": \"success\"")) {
                            var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(msg);
                            if (parsed.ContainsKey("room_id")) {
                                currentRoomId = parsed["room_id"].ToString();
                                Debug.Log("Room created, id = " + currentRoomId);

                                AuthManager.Instance.SendWebSocket(JsonConvert.SerializeObject(new { cmd = "get_rooms" }));

                                //AuthManager.Instance.SendWebSocket(JsonConvert.SerializeObject(new { cmd = "leave_room", room_id = currentRoomId }));
                            }
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
