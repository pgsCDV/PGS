using UnityEngine;

public class MazePlayerSize : MonoBehaviour {
    private bool inside;
    private float lastTriggerTime = -1f;
    private float debounceTime = 0.04f;

    private void OnTriggerEnter(Collider other) {
        if (Time.time - lastTriggerTime < debounceTime) return;
        if (other.transform.parent.TryGetComponent<PlayerMovement>(out PlayerMovement pm)) {
            pm.transform.localScale = new Vector3(.55f, .55f, .55f);
            inside = true;
            lastTriggerTime = Time.time;
        }
    }

    private void OnTriggerExit(Collider other) {
        if (Time.time - lastTriggerTime < debounceTime) return;
        if (other.transform.parent.TryGetComponent<PlayerMovement>(out PlayerMovement pm)) {
            pm.transform.localScale = Vector3.one;
            inside = false;
            lastTriggerTime = Time.time;
        }
    }
}