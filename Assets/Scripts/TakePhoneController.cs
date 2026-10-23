using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TakePhoneController : MonoBehaviour
{
    public AudioSource takePhone;
    public GameObject phone;
    public bool isExamScene = false;
    private bool collected = false;

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;

        if (other.CompareTag("Player") && WaterRiseEffect.isRainStarted == true)
        {
            collected = true;
            if (isExamScene == false)
            {
                takePhone.Play();

            }
            if (ScoreManager.Instance != null) ScoreManager.Instance.MarkPhoneTaken();
            phone.SetActive(false);
        }
    }
}
