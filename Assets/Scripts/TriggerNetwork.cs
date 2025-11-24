using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

public static class TriggerNetwork {
    public static void SendRPC(TriggerController trigger, string cmd, object data) {
        var room = BasicNetworkManager.Instance.CurrentRoomId;
        if (string.IsNullOrEmpty(room)) return;

        var evt = new Dictionary<string, object>
        {
            { "cmd", cmd },
            { "trigger_id", trigger.GetInstanceID() },
            { "data", data }
        };

        var payload = new Dictionary<string, object>
        {
            { "cmd", "room_rpc" },
            { "room_id", room },
            { "event", evt }
        };

        AuthManager.Instance.SendWSMsg(JsonConvert.SerializeObject(payload));
    }

    public static void ReceiveRPC(int triggerId, string cmd, object data) {
        foreach (var t in GameObject.FindObjectsOfType<TriggerController>()) {
            if (t.GetInstanceID() != triggerId) continue;

            if (cmd == "teleport") {
                var pos = (Vector3)data;
                foreach (var uid in new List<string>(PlayerSpawnerInternal.uids))
                    PlayerSpawnerInternal.Teleport(uid, pos);
            }

            else if (cmd == "teleport_specific") {
                var pos = (Vector3)data;
                var uid = AuthManager.Instance.GetCurrentUserId();
                PlayerSpawnerInternal.Teleport(uid, pos);
            }

            else if (cmd == "teleport_both") {
                var pos = (Vector3)data;
                foreach (var uid in new List<string>(PlayerSpawnerInternal.uids))
                    PlayerSpawnerInternal.Teleport(uid, pos);
            }
        }
    }
}
