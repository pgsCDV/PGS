using UnityEngine;

public class lava : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            debug.log("Gracz umar");
            Destroy(other.gameObject);
        }
    }
}