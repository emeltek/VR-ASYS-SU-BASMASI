using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TakePhoneController : MonoBehaviour
{
    public AudioSource takePhone;
    public GameObject phone;
    public bool isExamScene = false;

    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("Player") && WaterRiseEffect.isRainStarted == true)
        {
            if (isExamScene == false)
            {
                takePhone.Play();

            }
            else
            {
                ScoreManager.Instance.AddScore(20f);
            }
            phone.SetActive(false);
        }
    }
}
