using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TakePhoneController : MonoBehaviour
{
    public AudioSource takePhone;

    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("Player"))
        {
            takePhone.Play();
        }
    }
}
