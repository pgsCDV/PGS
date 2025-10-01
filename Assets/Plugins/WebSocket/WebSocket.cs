using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Runtime.InteropServices;

public class MainThreadUtil : MonoBehaviour {
    public static MainThreadUtil Instance { get; private set; }
    public static SynchronizationContext Context { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Setup() {
        Instance = new GameObject("MainThreadUtil").AddComponent<MainThreadUtil>();
        Context = SynchronizationContext.Current;
    }

    public static void Run(IEnumerator coroutine) => Context.Post(_ => Instance.StartCoroutine(coroutine), null);

    void Awake() {
        gameObject.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(gameObject);
    }
}

public class WaitForUpdate : CustomYieldInstruction {
    public override bool keepWaiting => false;

    public MainThreadAwaiter GetAwaiter() {
        var awaiter = new MainThreadAwaiter();
        MainThreadUtil.Run(CoroutineWrapper(this, awaiter));
        return awaiter;
    }

    public class MainThreadAwaiter : INotifyCompletion {
        Action continuation;
        public bool IsCompleted { get; private set; }

        public void GetResult() { }

        public void Complete() {
            IsCompleted = true;
            continuation?.Invoke();
        }

        public void OnCompleted(Action continuation) => this.continuation = continuation;
    }

    static IEnumerator CoroutineWrapper(IEnumerator coroutine, MainThreadAwaiter awaiter) {
        yield return coroutine;
        awaiter.Complete();
    }
}

namespace NativeWebSocket {
    public delegate void WebSocketOpenEventHandler();
    public delegate void WebSocketMessageEventHandler(byte[] data);
    public delegate void WebSocketErrorEventHandler(string errorMsg);
    public delegate void WebSocketCloseEventHandler(WebSocketCloseCode closeCode);
    public delegate void WebSocketCloseReasonEventHandler(WebSocketCloseCode closeCode, string reason);

    public enum WebSocketCloseCode {
        NotSet = 0,
        Normal = 1000,
        Away = 1001,
        ProtocolError = 1002,
        UnsupportedData = 1003,
        Undefined = 1004,
        NoStatus = 1005,
        Abnormal = 1006,
        InvalidData = 1007,
        PolicyViolation = 1008,
        TooBig = 1009,
        MandatoryExtension = 1010,
        ServerError = 1011,
        TlsHandshakeFailure = 1015
    }

    public enum WebSocketState {
        Connecting, Open, Closing, Closed
    }

    public interface IWebSocket {
        event WebSocketOpenEventHandler OnOpen;
        event WebSocketMessageEventHandler OnMessage;
        event WebSocketErrorEventHandler OnError;
        event WebSocketCloseEventHandler OnClose;
        event WebSocketCloseReasonEventHandler OnCloseReason;
        WebSocketState State { get; }
    }

    public static class WebSocketHelpers {
        public static WebSocketCloseCode ParseCloseCodeEnum(int closeCode) =>
            Enum.IsDefined(typeof(WebSocketCloseCode), closeCode) ? (WebSocketCloseCode)closeCode : WebSocketCloseCode.Undefined;

        public static WebSocketException GetErrorMessageFromCode(int errorCode, Exception inner) => errorCode switch {
            -1 => new WebSocketUnexpectedException("WebSocket instance not found.", inner),
            -2 => new WebSocketInvalidStateException("WebSocket is already connected or connecting.", inner),
            -3 => new WebSocketInvalidStateException("WebSocket is not connected.", inner),
            -4 => new WebSocketInvalidStateException("WebSocket is already closing.", inner),
            -5 => new WebSocketInvalidStateException("WebSocket is already closed.", inner),
            -6 => new WebSocketInvalidStateException("WebSocket is not open.", inner),
            -7 => new WebSocketInvalidArgumentException("Invalid close code or reason too long.", inner),
            _ => new WebSocketUnexpectedException("Unknown error.", inner)
        };
    }

    public class WebSocketException : Exception {
        public WebSocketException(string message = null, Exception inner = null) : base(message, inner) { }
    }

    public class WebSocketUnexpectedException : WebSocketException {
        public WebSocketUnexpectedException(string message = null, Exception inner = null) : base(message, inner) { }
    }

    public class WebSocketInvalidArgumentException : WebSocketException {
        public WebSocketInvalidArgumentException(string message = null, Exception inner = null) : base(message, inner) { }
    }

    public class WebSocketInvalidStateException : WebSocketException {
        public WebSocketInvalidStateException(string message = null, Exception inner = null) : base(message, inner) { }
    }

    public class WaitForBackgroundThread {
        public ConfiguredTaskAwaitable.ConfiguredTaskAwaiter GetAwaiter() => Task.Run(() => { }).ConfigureAwait(false).GetAwaiter();
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    public class WebSocket : IWebSocket
    {
        [DllImport("__Internal")] static extern int WebSocketConnect(int instanceId);
        [DllImport("__Internal")] static extern int WebSocketClose(int instanceId, int code, string reason);
        [DllImport("__Internal")] static extern int WebSocketSend(int instanceId, byte[] dataPtr, int dataLength);
        [DllImport("__Internal")] static extern int WebSocketSendText(int instanceId, string message);
        [DllImport("__Internal")] static extern int WebSocketGetState(int instanceId);

        int instanceId;
        public event WebSocketOpenEventHandler OnOpen;
        public event WebSocketMessageEventHandler OnMessage;
        public event WebSocketErrorEventHandler OnError;
        public event WebSocketCloseEventHandler OnClose;
        public event WebSocketCloseReasonEventHandler OnCloseReason;

        public WebSocket(string url, Dictionary<string, string> headers = null) : this(url, (List<string>)null, headers) { }

        public WebSocket(string url, string subprotocol, Dictionary<string, string> headers = null) : this(url, new List<string> { subprotocol }, headers) { }

        public WebSocket(string url, List<string> subprotocols, Dictionary<string, string> headers = null)
        {
            if (!WebSocketFactory.isInitialized) WebSocketFactory.Initialize();
            instanceId = WebSocketFactory.WebSocketAllocate(url);
            WebSocketFactory.instances.Add(instanceId, this);
            if (subprotocols != null)
                foreach (string subprotocol in subprotocols)
                    WebSocketFactory.WebSocketAddSubProtocol(instanceId, subprotocol);
        }

        ~WebSocket() => WebSocketFactory.HandleInstanceDestroy(instanceId);

        public Task Connect()
        {
            int ret = WebSocketConnect(instanceId);
            if (ret < 0) throw WebSocketHelpers.GetErrorMessageFromCode(ret, null);
            return Task.CompletedTask;
        }

        public void CancelConnection()
        {
            if (State == WebSocketState.Open) Close(WebSocketCloseCode.Abnormal);
        }

        public Task Close(WebSocketCloseCode code = WebSocketCloseCode.Normal, string reason = null)
        {
            int ret = WebSocketClose(instanceId, (int)code, reason);
            if (ret < 0) throw WebSocketHelpers.GetErrorMessageFromCode(ret, null);
            return Task.CompletedTask;
        }

        public Task Send(byte[] data)
        {
            int ret = WebSocketSend(instanceId, data, data.Length);
            if (ret < 0) throw WebSocketHelpers.GetErrorMessageFromCode(ret, null);
            return Task.CompletedTask;
        }

        public Task SendText(string message)
        {
            int ret = WebSocketSendText(instanceId, message);
            if (ret < 0) throw WebSocketHelpers.GetErrorMessageFromCode(ret, null);
            return Task.CompletedTask;
        }

        public WebSocketState State
        {
            get
            {
                int state = WebSocketGetState(instanceId);
                if (state < 0) throw WebSocketHelpers.GetErrorMessageFromCode(state, null);
                return state switch
                {
                    0 => WebSocketState.Connecting,
                    1 => WebSocketState.Open,
                    2 => WebSocketState.Closing,
                    3 => WebSocketState.Closed,
                    _ => WebSocketState.Closed
                };
            }
        }

        public void DelegateOnOpenEvent() => OnOpen?.Invoke();
        public void DelegateOnMessageEvent(byte[] data) => OnMessage?.Invoke(data);
        public void DelegateOnErrorEvent(string errorMsg) => OnError?.Invoke(errorMsg);
        public void DelegateOnCloseEvent(int closeCode) => OnClose?.Invoke(WebSocketHelpers.ParseCloseCodeEnum(closeCode));
        public void DelegateOnCloseReasonEvent(int closeCode, string reason) => OnCloseReason?.Invoke(WebSocketHelpers.ParseCloseCodeEnum(closeCode), reason);
    }
#else
    public class WebSocket : IWebSocket {
        public event WebSocketOpenEventHandler OnOpen;
        public event WebSocketMessageEventHandler OnMessage;
        public event WebSocketErrorEventHandler OnError;
        public event WebSocketCloseEventHandler OnClose;
        public event WebSocketCloseReasonEventHandler OnCloseReason;

        readonly Uri uri;
        readonly Dictionary<string, string> headers;
        readonly List<string> subprotocols;
        ClientWebSocket socket;
        CancellationTokenSource tokenSource;
        readonly object outgoingLock = new();
        readonly object incomingLock = new();
        bool isSending;
        readonly List<ArraySegment<byte>> sendBytesQueue = new();
        readonly List<ArraySegment<byte>> sendTextQueue = new();
        readonly List<byte[]> messageList = new();

        public WebSocket(string url, Dictionary<string, string> headers = null) : this(url, (List<string>)null, headers) { }

        public WebSocket(string url, string subprotocol, Dictionary<string, string> headers = null) : this(url, new List<string> { subprotocol }, headers) { }

        public WebSocket(string url, List<string> subprotocols, Dictionary<string, string> headers = null) {
            uri = new Uri(url);
            this.headers = headers ?? new Dictionary<string, string>();
            this.subprotocols = subprotocols ?? new List<string>();
            if (!uri.Scheme.Equals("ws") && !uri.Scheme.Equals("wss"))
                throw new ArgumentException($"Unsupported protocol: {uri.Scheme}");
        }

        public void CancelConnection() => tokenSource?.Cancel();

        public async Task Connect() {
            try {
                tokenSource = new CancellationTokenSource();
                socket = new ClientWebSocket();
                foreach (var header in headers) socket.Options.SetRequestHeader(header.Key, header.Value);
                foreach (var subprotocol in subprotocols) socket.Options.AddSubProtocol(subprotocol);
                await socket.ConnectAsync(uri, tokenSource.Token);
                OnOpen?.Invoke();
                await Receive();
            } catch (Exception ex) {
                OnError?.Invoke(ex.Message);
                OnClose?.Invoke(WebSocketCloseCode.Abnormal);
                OnCloseReason?.Invoke(WebSocketCloseCode.Abnormal, ex.Message ?? "");
            }
            finally {
                if (socket != null) {
                    tokenSource?.Cancel();
                    socket.Dispose();
                }
            }
        }

        public WebSocketState State => socket.State switch {
            System.Net.WebSockets.WebSocketState.Connecting => WebSocketState.Connecting,
            System.Net.WebSockets.WebSocketState.Open => WebSocketState.Open,
            System.Net.WebSockets.WebSocketState.CloseSent or System.Net.WebSockets.WebSocketState.CloseReceived => WebSocketState.Closing,
            System.Net.WebSockets.WebSocketState.Closed => WebSocketState.Closed,
            _ => WebSocketState.Closed
        };

        public Task Send(byte[] bytes) => SendMessage(sendBytesQueue, WebSocketMessageType.Binary, new ArraySegment<byte>(bytes));

        public Task SendText(string message) => SendMessage(sendTextQueue, WebSocketMessageType.Text, new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)));

        async Task SendMessage(List<ArraySegment<byte>> queue, WebSocketMessageType type, ArraySegment<byte> buffer) {
            if (buffer.Count == 0) return;

            bool sending;
            lock (outgoingLock) {
                sending = isSending;
                if (!isSending) isSending = true;
            }

            if (!sending) {
                if (!Monitor.TryEnter(socket, 1000)) {
                    await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "", tokenSource.Token);
                    return;
                }

                try {
                    await socket.SendAsync(buffer, type, true, tokenSource.Token);
                }
                finally {
                    Monitor.Exit(socket);
                    lock (outgoingLock) isSending = false;
                }

                await HandleQueue(queue, type);
            }
            else {
                lock (outgoingLock) queue.Add(buffer);
            }
        }

        async Task HandleQueue(List<ArraySegment<byte>> queue, WebSocketMessageType type) {
            ArraySegment<byte> buffer;
            lock (outgoingLock) {
                if (queue.Count == 0) return;
                buffer = queue[0];
                queue.RemoveAt(0);
            }
            await SendMessage(queue, type, buffer);
        }

        public void DispatchMessageQueue() {
            if (messageList.Count == 0) return;
            List<byte[]> copy;
            lock (incomingLock) {
                copy = new List<byte[]>(messageList);
                messageList.Clear();
            }
            foreach (var msg in copy) OnMessage?.Invoke(msg);
        }

        async Task Receive() {
            WebSocketCloseCode closeCode = WebSocketCloseCode.Abnormal;
            string closeReason = "";
            await new WaitForBackgroundThread();
            var buffer = new ArraySegment<byte>(new byte[8192]);

            try {
                while (socket.State == System.Net.WebSockets.WebSocketState.Open) {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do {
                        result = await socket.ReceiveAsync(buffer, tokenSource.Token);
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    }
                    while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);

                    if (result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary) {
                        var data = ms.ToArray();
                        MainThreadUtil.Run(Dispatch(data));
                    }
                    else if (result.MessageType == WebSocketMessageType.Close) {
                        closeCode = WebSocketHelpers.ParseCloseCodeEnum((int)result.CloseStatus);
                        closeReason = result.CloseStatusDescription ?? "";
                        await Close();
                        break;
                    }
                }
            } catch (Exception ex) {
                tokenSource.Cancel();
                MainThreadUtil.Run(DispatchError(ex.Message));
            }
            finally {
                await new WaitForUpdate();
                MainThreadUtil.Run(DispatchClose(closeCode, closeReason));
            }
        }
        IEnumerator Dispatch(byte[] data) {
            OnMessage?.Invoke(data);
            yield break;
        }

        IEnumerator DispatchError(string error) {
            OnError?.Invoke(error);
            yield break;
        }

        IEnumerator DispatchClose(WebSocketCloseCode code, string reason) {
            OnClose?.Invoke(code);
            OnCloseReason?.Invoke(code, reason);
            yield break;
        }
        public async Task Close() {
            if (State == WebSocketState.Open)
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", tokenSource.Token);
        }
    }
#endif

    public static class WebSocketFactory {
#if UNITY_WEBGL && !UNITY_EDITOR
        public static Dictionary<int, WebSocket> instances = new();
        public static bool isInitialized;

        [DllImport("__Internal")] public static extern int WebSocketAllocate(string url);
        [DllImport("__Internal")] public static extern void WebSocketAddSubProtocol(int instanceId, string subprotocol);
        [DllImport("__Internal")] public static extern void WebSocketFree(int instanceId);
        [DllImport("__Internal")] static extern void WebSocketSetOnOpen(OnOpenCallback callback);
        [DllImport("__Internal")] static extern void WebSocketSetOnMessage(OnMessageCallback callback);
        [DllImport("__Internal")] static extern void WebSocketSetOnError(OnErrorCallback callback);
        [DllImport("__Internal")] static extern void WebSocketSetOnClose(OnCloseCallback callback);

        public delegate void OnOpenCallback(int instanceId);
        public delegate void OnMessageCallback(int instanceId, IntPtr msgPtr, int msgSize);
        public delegate void OnErrorCallback(int instanceId, IntPtr errorPtr);
        public delegate void OnCloseCallback(int instanceId, int closeCode);

        public static void Initialize()
        {
            WebSocketSetOnOpen(DelegateOnOpenEvent);
            WebSocketSetOnMessage(DelegateOnMessageEvent);
            WebSocketSetOnError(DelegateOnErrorEvent);
            WebSocketSetOnClose(DelegateOnCloseEvent);
            isInitialized = true;
        }

        public static void HandleInstanceDestroy(int instanceId)
        {
            instances.Remove(instanceId);
            WebSocketFree(instanceId);
        }

        [MonoPInvokeCallback(typeof(OnOpenCallback))]
        static void DelegateOnOpenEvent(int instanceId)
        {
            if (instances.TryGetValue(instanceId, out var instance)) instance.DelegateOnOpenEvent();
        }

        [MonoPInvokeCallback(typeof(OnMessageCallback))]
        static void DelegateOnMessageEvent(int instanceId, IntPtr msgPtr, int msgSize)
        {
            if (instances.TryGetValue(instanceId, out var instance))
            {
                var msg = new byte[msgSize];
                Marshal.Copy(msgPtr, msg, 0, msgSize);
                instance.DelegateOnMessageEvent(msg);
            }
        }

        [MonoPInvokeCallback(typeof(OnErrorCallback))]
        static void DelegateOnErrorEvent(int instanceId, IntPtr errorPtr)
        {
            if (instances.TryGetValue(instanceId, out var instance))
                instance.DelegateOnErrorEvent(Marshal.PtrToStringAuto(errorPtr));
        }

        [MonoPInvokeCallback(typeof(OnCloseCallback))]
        static void DelegateOnCloseEvent(int instanceId, int closeCode)
        {
            if (instances.TryGetValue(instanceId, out var instance))
                instance.DelegateOnCloseEvent(closeCode);
            if (instances.TryGetValue(instanceId, out var instance2))
                instance2.DelegateOnCloseReasonEvent(closeCode, "");
        }
#endif

        public static WebSocket CreateInstance(string url) => new WebSocket(url);
    }
}
