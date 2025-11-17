using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
	public GameObject PlayerPrefab;
	public Transform Player, PlayerSpawn1, PlayerSpawn2;
	void Start()
	{
		AuthManager am = AuthManager.Instance;
		Transform pp = (am.side == 1 ? PlayerSpawn1 : PlayerSpawn2);
		GameObject go = Instantiate(PlayerPrefab,Player);
		go.name = am.GetCurrentUserId();
        go.transform.position = pp.position;
		CameraSingletone.instance.transform.position = go.transform.position;
		CameraSingletone.instance.transform.SetParent(go.transform);
	}
}
