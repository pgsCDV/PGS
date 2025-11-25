using UnityEngine;
using UnityEngine.SceneManagement;

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