using UnityEngine;

public class PlayerNetwork : MonoBehaviour
{
    public bool IsLocal;
    private void Start() {
        if (!IsLocal) {
            Destroy(GetComponent<Movement1>());
            TryGetComponent<PlayerMovement>(out PlayerMovement pm);
            {
                Destroy(pm);
            }
        }
    }
}
