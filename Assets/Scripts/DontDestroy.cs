using UnityEngine;

public class DontDestroy : MonoBehaviour {
	public bool dontDestroy;
	public void Awake() {
		if (dontDestroy) DontDestroyOnLoad(this);
	}
}