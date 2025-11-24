using System.Collections.Generic;
using UnityEngine;

public enum TriggerCondition : byte {
    Any = 0,
    LocalOnly = 1,
    RemoteOnly = 2,
    Side1 = 3,
    Side2 = 4,
    BothRequired = 5
}

public enum TriggerAction : byte {
    Teleport = 0,
    SetObjectActive = 1
}

public class TriggerController : MonoBehaviour {
    public TriggerCondition condition;
    public TriggerAction action;

    public Transform teleportSide1;
    public Transform teleportSide2;

    public GameObject targetObject;
    public bool activeStateOnEnter = true;

    private HashSet<GameObject> playersInside = new HashSet<GameObject>();

    void OnTriggerEnter(Collider other) {
        HandleTrigger(other, true);
    }

    void OnTriggerExit(Collider other) {
        HandleTrigger(other, false);
    }

    void HandleTrigger(Collider other, bool isEnter) {
        var pn = other.transform.parent.GetComponent<PlayerNetwork>();
        if (pn == null) return;

        if (isEnter) playersInside.Add(other.gameObject);
        else playersInside.Remove(other.gameObject);

        bool conditionMet = CheckCondition(pn);

        if (conditionMet) {
            if (isEnter) ExecuteAction(pn);
            else RevertAction(pn);
        }
    }

    bool CheckCondition(PlayerNetwork pn) {
        if (condition == TriggerCondition.BothRequired) return playersInside.Count >= 2;

        int localSide = AuthManager.Instance.side;
        int playerSide = pn.IsLocal ? localSide : (localSide == 1 ? 2 : 1);

        switch (condition) {
            case TriggerCondition.Any: return true;
            case TriggerCondition.LocalOnly: return pn.IsLocal;
            case TriggerCondition.RemoteOnly: return !pn.IsLocal;
            case TriggerCondition.Side1: return playerSide == 1;
            case TriggerCondition.Side2: return playerSide == 2;
        }

        return false;
    }

    void ExecuteAction(PlayerNetwork pn) {
        if (action == TriggerAction.SetObjectActive) {
            if (targetObject != null) targetObject.SetActive(activeStateOnEnter);
        }
        else {
            if (condition == TriggerCondition.BothRequired) {
                TeleportLocalPlayerBothRequired();
            }
            else {
                if (pn.IsLocal) TeleportBySide(pn);
            }
        }
    }

    void RevertAction(PlayerNetwork pn) {
        if (action == TriggerAction.SetObjectActive) {
            if (targetObject != null) targetObject.SetActive(!activeStateOnEnter);
        }
    }

    void TeleportBySide(PlayerNetwork pn) {
        int localSide = AuthManager.Instance.side;
        int playerSide = pn.IsLocal ? localSide : (localSide == 1 ? 2 : 1);

        Transform target = playerSide == 1 ? teleportSide1 : teleportSide2;

        pn.transform.position = target.position;
        pn.SetNetworkPosition(target.position);
    }

    void TeleportLocalPlayerBothRequired() {
        foreach (var p in playersInside) {
            var net = p.GetComponent<PlayerNetwork>();
            if (net != null && net.IsLocal) {
                int localSide = AuthManager.Instance.side;
                Transform target = localSide == 1 ? teleportSide1 : teleportSide2;
                p.transform.position = target.position;
                net.SetNetworkPosition(target.position);
                return;
            }
        }
    }
}
