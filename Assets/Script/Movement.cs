using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class gyro_script: MonoBehaviour
{
    void Start()
    {
        Input.gyro.enabled = true;
    }
    void Update()
    {
        Debug.Log(Input.gyro.attitude);
        transform.rotation=Input.gyro.attitude;
    }
}
