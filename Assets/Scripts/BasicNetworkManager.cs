using Newtonsoft.Json;
using UnityEngine;

public class BasicNetworkManager : MonoBehaviour
{
    void Start() {
        LogInto();
    }
    async void LogInto() {
        print(SystemInfo.deviceUniqueIdentifier.Substring(0, 7));
        AuthManager.Instance.Authenticate(
            SystemInfo.deviceUniqueIdentifier.Substring(0, 7), SystemInfo.deviceUniqueIdentifier.Substring(0, 7), SystemInfo.deviceUniqueIdentifier, Application.version,
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
                            AuthManager.Instance.SendWebSocket(JsonConvert.SerializeObject(new { cmd = "get_sessions" }));
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
