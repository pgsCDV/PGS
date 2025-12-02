using UnityEngine;

public class TriggerPress : MonoBehaviour
{

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerNetwork>(out PlayerNetwork pn))
        {
            this.gameObject.GetComponent<MeshRenderer>().enabled = false;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<PlayerNetwork>(out PlayerNetwork pn))
        {
            this.gameObject.GetComponent<MeshRenderer>().enabled = true;
        }
    }
}
