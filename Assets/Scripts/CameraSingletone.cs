using UnityEngine;

public class CameraSingletone : MonoBehaviour
{
	public static CameraSingletone instance;
	private void Awake() {
		if (instance == null) 
			instance = this;
		else Destroy(this);
	}
}
