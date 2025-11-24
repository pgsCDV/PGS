using UnityEngine;

public enum TriggerType : byte { OnEnter = 2, OnStay = 4, OnExit = 8 }
public enum TriggerActivator : byte { Local = 1, Remote = 2, Both = 3 }
public enum TriggerAction : byte { Teleport = 1, TeleportIfBoth = 2, TeleportSpecific = 3 }

public class TriggerController : MonoBehaviour {
    public TriggerType type;
    public TriggerActivator activator;
    public TriggerAction action;
    public Transform teleportTarget;
    public string specificUid;

    int insideCount;

    void OnTriggerEnter(Collider other) {
        if (type != TriggerType.OnEnter) return;
        TryActivate(other);
    }

    void OnTriggerStay(Collider other) {
        if (type != TriggerType.OnStay) return;
        TryActivate(other);
    }

    void OnTriggerExit(Collider other) {
        if (type != TriggerType.OnExit) return;
        TryActivate(other);
    }

    void TryActivate(Collider other) {
        var pn = other.GetComponent<PlayerNetwork>();
        if (pn == null) return;

        if (activator == TriggerActivator.Local && !pn.IsLocal) return;
        if (activator == TriggerActivator.Remote && pn.IsLocal) return;

        if (action == TriggerAction.Teleport) {
            TriggerNetwork.SendRPC(this, "teleport", teleportTarget.position);
            return;
        }

        if (action == TriggerAction.TeleportSpecific) {
            if (pn.IsLocal && AuthManager.Instance.GetCurrentUserId() == specificUid)
                TriggerNetwork.SendRPC(this, "teleport_specific", teleportTarget.position);
            return;
        }

        if (action == TriggerAction.TeleportIfBoth) {
            if (pn.IsLocal) insideCount++;
            if (insideCount >= 2) {
                TriggerNetwork.SendRPC(this, "teleport_both", teleportTarget.position);
                insideCount = 0;
            }
        }
    }
}
