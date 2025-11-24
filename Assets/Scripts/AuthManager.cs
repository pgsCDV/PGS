using System;
using UnityEngine;
using Proyecto26;
using NativeWebSocket;
using Newtonsoft.Json;
using System.Text;
using System.Collections;
using System.Collections.Generic;

[Serializable]
public enum WSType : byte {
	Unknown,
	Connected,
	Success,
	ConnectCallback
}

[Serializable]
public enum WSCmd : byte {
	LeaveRoom,
	GetMe,
	CreateRoom,
	JoinRoom,
	GetRooms
}

[Serializable]
public struct LoginResponse {
	public string status;
	public LoginData data;
	public string error;
}

[Serializable]
public struct LoginData {
	public string user_id;
}

[Serializable]
public struct UserIdPayload {
	public string user_id;
}

[Serializable]
public class ServerData {
	public string uid;
	public int curr_users;
	public int max_users;
	public int seed;
	public int start_location;
}

[Serializable]
public class NetMessage {
	[JsonProperty("status")] public string Status;
	[JsonProperty("action")] public string Action;
	[JsonProperty("room_id")] public string RoomId;
	[JsonProperty("username")] public string Username;
	[JsonProperty("room")] public Dictionary<string, object> Room;
	[JsonProperty("rooms")] public Dictionary<string, object> Rooms;
	[JsonProperty("event")] public string Event;
	[JsonProperty("error")] public string Error;
	public float PositionX;
	public float PositionY;
	public float PositionZ;
	[JsonProperty("uid")] public string UID;
	[JsonProperty("side")] public int Side;
}

public class AuthManager : MonoBehaviour {
	public static AuthManager Instance { get; private set; }

	const string ServerUrl = "https://pgs.wk19.lol";
	const string WsUrl = "wss://pgs.wk19.lol/ws";
	public int side;
	string currentUserId;
	public string username;
	WebSocket ws;
	bool isDestroyed;
	bool autoReconnectEnabled = true;
	float reconnectDelay = 4f;
	Coroutine reconnectRoutine;
	bool lastCloseWasNormal;

	public string GetCurrentUserId() => currentUserId;
	public string SetCurrentUsername(string uid) => username = uid;
	public bool IsSocketActive => ws != null && ws.State == WebSocketState.Open;

	void Awake() {
		if (Instance == null) Instance = this;
		else Destroy(gameObject);
	}

	IEnumerator Start() {
		while (true) {
			if (IsSocketActive && !string.IsNullOrEmpty(BasicNetworkManager.Instance.CurrentRoomId)) {
				SendWSMsg("{\"cmd\":\"ping\"}");
			}
			yield return new WaitForSeconds(5f);
		}
	}

	[Serializable]
	private class Credentials {
		public string username;
		public string password;
		public string version;
	}

	public void Authenticate(string username, string password, string version,
		Action<LoginResponse> onSuccess, Action<string> onFailure) {

		var creds = new Credentials { username = username, password = password, version = version };

		RestClient.Post<LoginResponse>(ServerUrl + "/login", creds).Then(resp => {
			if (resp.status == "success") {
				currentUserId = resp.data.user_id;
				Debug.Log($"[AUTH] Login success, user_id={currentUserId}");
				onSuccess?.Invoke(resp);
			}
			else {
				onFailure?.Invoke(resp.error ?? "Auth failed");
			}
		}).Catch(err => {
			onFailure?.Invoke(err.Message);
		});
	}

	async void OnDestroy() {
		isDestroyed = true;
		autoReconnectEnabled = false;
		if (ws != null) {
			try {
				await ws.Close();
			} catch { }
			ws = null;
		}
	}

	public async void ConnectWebSocket(Action onOpen = null, Action<string> onMessage = null,
		Action<string> onError = null, Action<WebSocketCloseCode> onClose = null) {

		if (string.IsNullOrEmpty(currentUserId)) {
			Debug.LogError("[WS] No user_id set — login first");
			return;
		}

		if (ws != null && (ws.State == WebSocketState.Open || ws.State == WebSocketState.Connecting)) {
			Debug.Log("[WS] Already connected or connecting");
			return;
		}

		lastCloseWasNormal = false;

		ws = new WebSocket(WsUrl);

		ws.OnOpen += () => {
			if (isDestroyed) return;
			Debug.Log("[WS] Connected");
			onOpen?.Invoke();

			var payload = new UserIdPayload { user_id = GetCurrentUserId() };
			try {
				ws.SendText(JsonConvert.SerializeObject(payload));
			} catch (Exception e) {
				Debug.LogError($"[WS] Send after open failed: {e.Message}");
			}

			if (reconnectRoutine != null) {
				try {
					StopCoroutine(reconnectRoutine);
				} catch { }
				reconnectRoutine = null;
			}
		};

		ws.OnMessage += (bytes) => {
			if (isDestroyed) return;
			string msg = Encoding.UTF8.GetString(bytes);
			Debug.Log($"[WS] {Time.time} {Time.frameCount} | Received: {msg}");
			onMessage?.Invoke(msg);
		};

		ws.OnError += (err) => {
			if (isDestroyed) return;
			Debug.LogError($"[WS] Error: {err}");
			onError?.Invoke(err);
			if (autoReconnectEnabled && Application.isPlaying && !lastCloseWasNormal) {
				TryReconnect(onOpen, onMessage, onError, onClose);
			}
		};

		ws.OnClose += (code) => {
			if (isDestroyed) return;
			Debug.Log($"[WS] Closed: {code}");
			onClose?.Invoke(code);

			if (code == WebSocketCloseCode.Normal) {
				lastCloseWasNormal = true;
				try {
					if (reconnectRoutine != null) {
						StopCoroutine(reconnectRoutine);
						reconnectRoutine = null;
					}
				} catch { }
				ws = null;
				return;
			}

			ws = null;

			if (autoReconnectEnabled && Application.isPlaying && !lastCloseWasNormal) {
				TryReconnect(onOpen, onMessage, onError, onClose);
			}
		};

		try {
			Debug.Log("[WS] Connecting...");
			await ws.Connect();
		} catch (Exception e) {
			Debug.LogError($"[WS] Connection failed: {e.Message}");
			if (autoReconnectEnabled && Application.isPlaying && !lastCloseWasNormal) {
				TryReconnect(onOpen, onMessage, onError, onClose);
			}
		}
	}

	void TryReconnect(Action onOpen, Action<string> onMessage, Action<string> onError, Action<WebSocketCloseCode> onClose) {
		if (lastCloseWasNormal) return;
		if (reconnectRoutine == null) reconnectRoutine = StartCoroutine(ReconnectCoroutine(onOpen, onMessage, onError, onClose));
	}

	IEnumerator ReconnectCoroutine(Action onOpen, Action<string> onMessage, Action<string> onError, Action<WebSocketCloseCode> onClose) {
		Debug.Log($"[WS] Attempting reconnect in {reconnectDelay}s...");
		yield return new WaitForSeconds(reconnectDelay);
		if (!isDestroyed && Application.isPlaying && !lastCloseWasNormal) {
			ConnectWebSocket(onOpen, onMessage, onError, onClose);
		}
		reconnectRoutine = null;
	}

	public async void SendWSMsg(string message) {
		if (ws != null && ws.State == WebSocketState.Open) {
			Debug.Log($"[WS] {Time.time} {Time.frameCount} | Sent: {message}");
			try {
				await ws.SendText(message);
			} catch (Exception e) {
				Debug.LogError($"[WS] Send failed: {e.Message}");
			}
		}
		else {
			Debug.LogWarning("[WS] Not connected — message not sent");
		}
	}

	public async void CloseWS() {
		if (ws != null) {
			lastCloseWasNormal = true;
			Debug.Log("[WS] Closing connection...");
			try {
				await ws.Close();
			} catch (Exception e) {
				Debug.LogError($"[WS] Close failed: {e.Message}");
			}
			ws = null;
		}
	}

	public void CloseGame() {
		CloseWS();
		Application.Quit();
		SendWSMsg("{\"cmd\":\"ping\"}");
	}

	private void OnDisable() {
		if (Application.isPlaying) CloseWS();
	}
}