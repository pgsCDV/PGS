using System.Collections.Generic;
using UnityEngine;

public static class PlayerSpawnerInternal {
	public static List<string> uids = new List<string>();

	public static void Register(string uid) {
		if (!uids.Contains(uid)) uids.Add(uid);
	}

	public static void Unregister(string uid) {
		if (uids.Contains(uid)) uids.Remove(uid);
	}

	public static void Teleport(string uid, Vector3 pos) {
		PlayerSpawner.Instance.TrySetPlayerPosition(uid, pos);
	}
}
