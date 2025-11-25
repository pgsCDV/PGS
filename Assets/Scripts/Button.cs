using UnityEngine;
using System.Collections;

public class ButtonScript : MonoBehaviour
{
    public GameObject doors;
    public float openDuration = 1f;
    private bool isPressed = false;
    private bool doorsOpen = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isPressed && !doorsOpen)
        {
            isPressed = true;
            StartCoroutine(OpenDoorsSmooth());
            Debug.Log("Przycisk wci?ni?ty! Drzwi si? otwieraj? p?ynnie.");
        }
    }

    IEnumerator OpenDoorsSmooth()
    {
        doorsOpen = true;

        Quaternion startRotation = doors.transform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0f, 90f, 0f);

        float elapsedTime = 0f;

        while (elapsedTime < openDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / openDuration;
            doors.transform.rotation = Quaternion.Lerp(startRotation, endRotation, progress);
            yield return null;
        }

        doors.transform.rotation = endRotation;
    }
}