using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TriggerCondition : byte {
	Any = 0,
	LocalOnly = 1,
	RemoteOnly = 2,
	Side1 = 3,
	Side2 = 4,
	Both = 5
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

	public float syncDelay = 0.5f;

	private HashSet<GameObject> playersInside = new HashSet<GameObject>();
	private Coroutine waitingCoroutine;

	void OnTriggerEnter(Collider other) {
		PlayerNetwork pn = other.transform.parent.GetComponent<PlayerNetwork>();
		if (pn == null) return;

		playersInside.Add(other.gameObject);
		playersInside.RemoveWhere(go => go == null);

		CheckAndStartTriggerLogic(pn);
	}

	private void OnTriggerStay(Collider other) {
		PlayerNetwork pn = other.transform.parent.GetComponent<PlayerNetwork>();
		if (pn == null) return;

		playersInside.Add(other.gameObject);
		playersInside.RemoveWhere(go => go == null);


		CheckAndStartTriggerLogic(pn);
	}

	void CheckAndStartTriggerLogic(PlayerNetwork pn) {
		if (condition == TriggerCondition.Both) {
			if (playersInside.Count >= 2) {
				if (waitingCoroutine == null) {
					waitingCoroutine = StartCoroutine(WaitAndExecuteBothCondition());
				}
			}
		}
		else {
			HandleTrigger(pn, true);
		}
	}

	void OnTriggerExit(Collider other) {
		PlayerNetwork pn = other.transform.parent.GetComponent<PlayerNetwork>();
		if (pn == null) return;

		playersInside.Remove(other.gameObject);

		if (condition == TriggerCondition.Both) {
			if (playersInside.Count < 2) {
				if (waitingCoroutine != null) {
					StopCoroutine(waitingCoroutine);
					waitingCoroutine = null;
				}

				if (action == TriggerAction.SetObjectActive && targetObject != null) {
					targetObject.SetActive(!activeStateOnEnter);
				}
			}
		}
		else {
			if (action == TriggerAction.SetObjectActive) {
				if (playersInside.Count < 1 && targetObject != null) {
					targetObject.SetActive(!activeStateOnEnter);
				}
			}
		}
	}

	IEnumerator WaitAndExecuteBothCondition() {
		yield return new WaitForSeconds(syncDelay);

		if (playersInside.Count >= 2) {
			PlayerNetwork localPlayer = FindLocalPlayerInTrigger();

			if (localPlayer != null) {
				ExecuteStandardAction(localPlayer);
			}
		}

		waitingCoroutine = null;
	}

	PlayerNetwork FindLocalPlayerInTrigger() {
		foreach (GameObject go in playersInside) {
			if (go == null) continue;
			var pn = go.transform.parent.GetComponent<PlayerNetwork>();
			if (pn != null && pn.IsLocal) {
				return pn;
			}
		}
		return null;
	}

	void HandleTrigger(PlayerNetwork pn, bool isEnter) {
		bool conditionMet = CheckCondition(pn);

		if (conditionMet) {
			if (isEnter) ExecuteStandardAction(pn);
			else RevertAction();
		}
	}

	bool CheckCondition(PlayerNetwork pn) {
		if (condition == TriggerCondition.Both) return false;

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

	void ExecuteStandardAction(PlayerNetwork pn) {
		if (action == TriggerAction.SetObjectActive) {
			if (targetObject != null) targetObject.SetActive(activeStateOnEnter);
		}
		else {
			if (pn.IsLocal) {
				TeleportBySide(pn);
			}
		}
	}

	void RevertAction() {
		if (action == TriggerAction.SetObjectActive) {
			if (targetObject != null) targetObject.SetActive(!activeStateOnEnter);
		}
	}

	void TeleportBySide(PlayerNetwork pn) {
		int localSide = AuthManager.Instance.side;
		int playerSide = localSide;

		Transform target = playerSide == 1 ? teleportSide1 : teleportSide2;

		if (target != null) {

			pn.transform.position = target.position;
			pn.SetNetworkPosition(target.position);
		}
	}
}