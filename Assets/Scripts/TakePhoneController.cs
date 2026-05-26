using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TakePhoneController : MonoBehaviour
{
    public AudioSource takePhone;
    public GameObject phone;

    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("Player") && WaterRiseEffect.isRainStarted == true)
        {
            takePhone.Play();
            phone.SetActive(false);
        }
    }
}
