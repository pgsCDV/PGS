using System.Collections.Generic;
using UnityEngine;

public enum TriggerCondition : byte {
    Any = 0,
    LocalOnly = 1,
    RemoteOnly = 2,
    Side1 = 3,
    Side2 = 4,
    Both = 5 // Ждем, пока наберется 2 человека
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

    // Список всех, кто сейчас внутри (и локальный, и удаленный)
    private HashSet<GameObject> playersInside = new HashSet<GameObject>();

    void OnTriggerEnter(Collider other) {
        var pn = other.transform.parent.GetComponent<PlayerNetwork>();
        if (pn == null) return;

        // 1. Добавляем вошедшего в список
        playersInside.Add(other.gameObject);

        // Чистка мусора на всякий случай
        playersInside.RemoveWhere(go => go == null);

        if (condition == TriggerCondition.Both) {
            // Логика "BOTH": Проверяем, есть ли двое?
            if (playersInside.Count >= 2) {
                // УСЛОВИЕ ВЫПОЛНЕНО!
                // Но мы не командуем всем списком. Мы ищем СЕБЯ (Local Player).

                PlayerNetwork localPlayer = FindLocalPlayerInTrigger();

                // Если я сам нахожусь внутри этого триггера - телепортируюсь.
                // (Если я снаружи, а в триггере двое других ботов/игроков - мне все равно)
                if (localPlayer != null) {
                    ExecuteStandardAction(localPlayer);
                }

                // Примечание: Удаленного игрока мы НЕ трогаем. 
                // На его компьютере сработает этот же код, и он телепортирует сам себя.
            }
        }
        else {
            // Обычная логика для остальных режимов
            HandleTrigger(pn, true);
        }
    }

    void OnTriggerExit(Collider other) {
        var pn = other.transform.parent.GetComponent<PlayerNetwork>();
        if (pn == null) return;

        playersInside.Remove(other.gameObject);

        // Логика выхода (для включения/выключения объектов)
        if (action == TriggerAction.SetObjectActive) {
            if (condition == TriggerCondition.Both) {
                // Если стало меньше 2 человек, условие нарушено
                if (playersInside.Count < 2 && targetObject != null) {
                    targetObject.SetActive(!activeStateOnEnter);
                }
            }
            else if (playersInside.Count < 1) {
                if (targetObject != null) targetObject.SetActive(!activeStateOnEnter);
            }
        }
    }

    // Ищем, есть ли Локальный игрок среди тех, кто в триггере
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
        // Both обрабатывается отдельно в Enter, здесь возвращаем false
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
            // Важно: мы выполняем действие ТОЛЬКО если это локальный игрок.
            // Удаленные объекты двигаются только через сеть, мы их не трогаем физикой.
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
        // Эта функция вызывается только для Local игрока (благодаря проверкам выше),
        // но сторону вычисляем честно.
        int localSide = AuthManager.Instance.side;

        // Раз pn.IsLocal == true, то playerSide всегда равен localSide
        int playerSide = localSide;

        Transform target = playerSide == 1 ? teleportSide1 : teleportSide2;

        if (target != null) {
            // Двигаем себя
            pn.transform.position = target.position;
            // Сообщаем сети, что мы переместились
            pn.SetNetworkPosition(target.position);
        }
    }
}