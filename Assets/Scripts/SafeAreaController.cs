using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SafeAreaController : MonoBehaviour
{
    public VRMovementTracker tracker;
    public AudioSource upperfloorSuccess;
    private bool isAudioTriggered;
    public static bool isinSafeAreaController = false;
    public bool isExamScene = false;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && WaterRiseEffect.isRainStarted == true)
        {
            if (!isAudioTriggered)
            {
                tracker.SetSafeAreaStatus(true);
                isAudioTriggered = true;
                isinSafeAreaController = true;
                if (isExamScene == false)
                {
                    upperfloorSuccess.Play();

                }
                else
                {
                    ScoreManager.Instance.AddScore(20f);
                }

            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            tracker.SetSafeAreaStatus(false);
            isinSafeAreaController = false;
        }
    }
}
