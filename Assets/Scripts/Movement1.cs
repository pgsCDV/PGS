using UnityEngine;

public class Movement1 : MonoBehaviour
{
    [Header("Ustawienia ruchu")]
    public float speed = 10.0f; 
    public bool invertX = false;
    public bool invertY = false;

    private Vector3 moveDirection;

    void Update()
    {
        float moveX = Input.acceleration.x;
        float moveY = Input.acceleration.y;

        if (invertX) moveX = -moveX;
        if (invertY) moveY = -moveY;

        moveDirection = new Vector3(moveX, 0, moveY);

        transform.Translate(moveDirection * speed * Time.deltaTime);
    }
}
