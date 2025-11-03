using System;
using UnityEngine;
using Proyecto26;
using NativeWebSocket;
using Newtonsoft.Json;
using System.Text;
using System.Collections;

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
	GetRooms,
	Ping
}

[Serializable]
public struct LoginResponse {
	public string status;
	public LoginData data;
	public string error;
}

[Serializable]
public struct MeResponse {
	public string status;
	public object data;
	public string error;
}

[Serializable]
public struct LoginData {
	public string user_id, token, expires;
}

[Serializable]
public struct TokenPayload {
	public string token;
}

public class AuthManager : MonoBehaviour {
	public static AuthManager Instance { get; private set; }

	const string ServerUrl = "https://pgs.wk19.lol";
	const string WsUrl = "wss://pgs.wk19.lol/ws";
	string currentToken, currentUserId;
	WebSocket ws;
	bool isDestroyed, autoReconnectEnabled = true;
	float reconnectDelay = 4f;
	Coroutine reconnectRoutine;

	public int playerSpawnID;

	public string GetCurrentToken() => currentToken;
	public string GetCurrentUserId() => currentUserId;
	public bool IsSocketActive => ws != null && ws.State == WebSocketState.Open;

	void Awake() {
		if (Instance == null) Instance = this;
		else Destroy(gameObject);
	}

	[Serializable]
	private class Credentials {
		public string username;
		public string password;
		public string device_id;
		public string version;
	}

	public void Authenticate(string username, string password, string deviceId, string version,
		Action<LoginResponse> onSuccess, Action<string> onFailure) {

		var creds = new Credentials { username = username, password = password, device_id = deviceId, version = version };

		RestClient.Post<LoginResponse>(ServerUrl + "/login", creds).Then(resp => {
			if (resp.status == "success") {
				currentToken = resp.data.token;
				currentUserId = resp.data.user_id;
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
		if (ws != null && ws.State == WebSocketState.Open) await ws.Close();
	}

	public async void ConnectWebSocket(Action onOpen = null, Action<string> onMessage = null,
		Action<string> onError = null, Action<WebSocketCloseCode> onClose = null) {

		if (string.IsNullOrEmpty(currentToken)) {
			Debug.LogError($"[WS] [{Time.time}|{Time.frameCount}] - No token available for WS");
			return;
		}

		if (IsSocketActive) {
			Debug.Log($"[WS] [{Time.time}|{Time.frameCount}] - Already connected");
			return;
		}

		ws = new WebSocket(WsUrl);

		ws.OnOpen += () => {
			if (isDestroyed) return;
			Debug.Log($"[WS] [{Time.time}|{Time.frameCount}] - Connected");
			onOpen?.Invoke();

			var payload = new TokenPayload { token = GetCurrentToken() };
			ws.SendText(JsonConvert.SerializeObject(payload));

			if (reconnectRoutine != null) {
				StopCoroutine(reconnectRoutine);
				reconnectRoutine = null;
			}
		};

		ws.OnMessage += (bytes) => {
			if (isDestroyed) return;
			string msg = Encoding.UTF8.GetString(bytes);
			Debug.Log($"[WS] [{Time.time}|{Time.frameCount}] - Received: {msg}");
			onMessage?.Invoke(msg);
		};

		ws.OnError += (err) => {
			if (isDestroyed) return;
			Debug.LogError($"[WS] [{Time.time}|{Time.frameCount}] - Error: {err}");
			onError?.Invoke(err);
			if (autoReconnectEnabled && Application.isPlaying) TryReconnect(onOpen, onMessage, onError, onClose);
		};

		ws.OnClose += (code) => {
			if (isDestroyed) return;
			Debug.Log($"[WS] [{Time.time}|{Time.frameCount}] - Closed: {code}");
			onClose?.Invoke(code);
			if (autoReconnectEnabled && Application.isPlaying) TryReconnect(onOpen, onMessage, onError, onClose);
		};

		try {
			Debug.Log($"[WS] [{Time.time}|{Time.frameCount}] - Connecting...");
			await ws.Connect();
		} catch (Exception e) {
			Debug.LogError($"[WS] [{Time.time}|{Time.frameCount}] - Connection failed: {e.Message}");
			if (autoReconnectEnabled && Application.isPlaying) TryReconnect(onOpen, onMessage, onError, onClose);
		}
	}

	void TryReconnect(Action onOpen, Action<string> onMessage, Action<string> onError, Action<WebSocketCloseCode> onClose) {
		if (reconnectRoutine == null) reconnectRoutine = StartCoroutine(ReconnectCoroutine(onOpen, onMessage, onError, onClose));
	}

	IEnumerator ReconnectCoroutine(Action onOpen, Action<string> onMessage, Action<string> onError, Action<WebSocketCloseCode> onClose) {
		Debug.Log($"[WS] [{Time.time}|{Time.frameCount}] - Attempting reconnect in {reconnectDelay}s...");
		yield return new WaitForSeconds(reconnectDelay);
		if (!isDestroyed && Application.isPlaying) ConnectWebSocket(onOpen, onMessage, onError, onClose);
	}

	public async void SendWSMsg(string message) {
		if (ws != null && ws.State == WebSocketState.Open) {
			Debug.Log($"[WS] [{Time.time}|{Time.frameCount}] - Sent: {message}");
			await ws.SendText(message);
		}
		else {
			Debug.LogWarning($"[WS] [{Time.time}|{Time.frameCount}] - Not connected, message not sent");
		}
	}

	public async void CloseWS() {
		if (ws != null) {
			Debug.Log($"[WS] [{Time.time}|{Time.frameCount}] - Closing connection...");
			await ws.Close();
			ws = null;
		}
	}

	private void OnDisable() {
		if (Application.isPlaying) CloseWS();
	}
}