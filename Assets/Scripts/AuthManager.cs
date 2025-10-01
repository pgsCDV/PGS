using System;
using UnityEngine;
using Proyecto26;
using NativeWebSocket;
using Newtonsoft.Json;
using System.Text;
using UnityEngine.UI;

[Serializable]
public class LoginResponse {
	public string status;
	public LoginData data;
	public string error;
}

[Serializable]
public class MeResponse {
	public string status;
	public object data;
	public string error;
}

[Serializable]
public class LoginData {
	public string user_id;
	public string token;
	public string expires;
}

[Serializable]
public class TokenPayload {
	public string token;
}

public class AuthManager : MonoBehaviour {
	public static AuthManager Instance { get; private set; }
	private const string ServerUrl = "https://wk19.lol:7007";
	private const string WsUrl = "wss://wk19.lol:7007/ws";
	private string currentToken;
	private string currentUserId;
	private WebSocket ws;
	private bool isDestroyed;
	public Text field1, field2, field3;
	public GameObject RegistrationWindow;
	public string GetCurrentToken() => currentToken;
	public string GetCurrentUserId() => currentUserId;

	void Awake() {
		if (Instance == null) {
			Instance = this;
		}
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

	// --- WebSocket ---
	async void OnDestroy() {
		isDestroyed = true;
		if (ws != null && ws.State == WebSocketState.Open) await ws.Close();
	}

	public async void ConnectWebSocket(Action onOpen = null, Action<string> onMessage = null,
		Action<string> onError = null, Action<WebSocketCloseCode> onClose = null) {

		if (string.IsNullOrEmpty(currentToken)) {
			Debug.LogError("❌ No token available for WS");
			return;
		}

		ws = new WebSocket(WsUrl);

		ws.OnOpen += () => {
			if (isDestroyed) return;
			Debug.Log("🔌 WS connected");
			onOpen?.Invoke();
			var payload = new TokenPayload { token = GetCurrentToken() };
			ws.SendText(JsonConvert.SerializeObject(payload));
		};

		ws.OnMessage += (bytes) => {
			if (isDestroyed) return;
			string msg = Encoding.UTF8.GetString(bytes);
			onMessage?.Invoke(msg);
		};

		ws.OnError += (err) => {
			if (!isDestroyed) Debug.LogError("⚠️ WS error: " + err);
			onError?.Invoke(err);
		};

		ws.OnCloseReason += (code, reason) => {
			if (isDestroyed) return;
			Debug.Log($"🔒 WS Closed: {code} : {reason}");
			onClose?.Invoke(code);
		};

		try {
			await ws.Connect();
		} catch {
			Debug.LogError("🚨 WS connection failed");
		}
	}

	public async void SendWebSocket(string message) {
		if (ws != null && ws.State == WebSocketState.Open) {
			await ws.SendText(message);
		}
		else {
			Debug.LogWarning("⚠️ WS not connected");
		}
	}

	public async void CloseWebSocket() {
		if (ws != null) {
			await ws.Close();
			ws = null;
		}
	}

	private void OnDisable() {
		CloseWebSocket();
	}
	public void RegisterField1Controller(InputField field) {
		if (field.text.Length >= 14) field.text = field.text.Substring(0, 13);
		if (field.text.Contains('!')) field.text = field.text.Substring(0, field.text.Length - 1);
		field1.color = (field.text.Length <= 3 ? Color.red : Color.green);
		field1.text = field.text.Length.ToString() + "/13";
	}
	public void RegisterField2Controller(InputField field) {
		if (field.text.Length >= 10) field.text = field.text.Substring(0, 9);
		if (field.text.Contains('!')) field.text = field.text.Substring(0, field.text.Length - 1);
		field2.color = (field.text.Length <= 4 ? Color.red : Color.green);
		field2.text = field.text.Length.ToString() + "/9";
	}
	public void RegisterField3Controller(InputField field) {
		if (field.text.Length >= 12) field.text = field.text.Substring(0, 11);
		if (field.text.Contains('!')) field.text = field.text.Substring(0, field.text.Length - 1);
		field3.color = (field.text.Length <= 1 ? Color.red : Color.green);
		field3.text = field.text.Length.ToString() + "/11";
	}
	public void ShowPassword(InputField inputField) {
		inputField.contentType = (inputField.contentType == InputField.ContentType.Password ? InputField.ContentType.Standard : InputField.ContentType.Password);
		string text = inputField.text;
		inputField.text += "1";
		inputField.text = text;
	}
}
