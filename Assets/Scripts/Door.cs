using UnityEngine;

public class DoorScript : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    public void Open()
    {
        if (animator != null)
        {
            animator.SetTrigger("Open");
        }
    }
}