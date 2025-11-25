using UnityEngine;
using UnityEngine.SceneManager;

public class lava : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            SceneManager.LoadScene(SceneManager.GetActivateScene().name);
        }
    }
}