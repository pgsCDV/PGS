using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

public class PlayerNetwork : MonoBehaviour {
    public bool IsLocal;
    Vector3 lastSentPos;
    float sendThreshold = 0.00018f;

    void Start() {
        if (!IsLocal) {
            var m1 = GetComponent<Movement1>();
            if (m1) Destroy(m1);
            if (TryGetComponent<PlayerMovement>(out var pm)) Destroy(pm);
        }
        else {
            lastSentPos = transform.position;
        }
    }

    void Update() {
        if (!IsLocal) return;

        var pos = transform.position;
        if ((pos - lastSentPos).sqrMagnitude > sendThreshold * sendThreshold) {
            lastSentPos = pos;

            var roomId = BasicNetworkManager.Instance != null ? BasicNetworkManager.Instance.CurrentRoomId : null;
            if (string.IsNullOrEmpty(roomId)) return;

            var evt = new Dictionary<string, object> {
                { "cmd", "sync_position" },
                { "position_x", pos.x },
                { "position_y", pos.y },
                { "position_z", pos.z }
            };

            var payload = new Dictionary<string, object> {
                { "cmd", "room_rpc" },
                { "room_id", roomId },
                { "event", evt }
            };

            var json = JsonConvert.SerializeObject(payload);
            AuthManager.Instance.SendWSMsg(json);
        }
    }
}
