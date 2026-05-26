using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SafeAreaController : MonoBehaviour
{
    public VRMovementTracker tracker;
    public AudioSource upperfloorSuccess;
    private bool isAudioTriggered;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && WaterRiseEffect.isRainStarted == true)
        {
            if (!isAudioTriggered)
            {
                tracker.SetSafeAreaStatus(true);
                upperfloorSuccess.Play();
                isAudioTriggered = true;

            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            tracker.SetSafeAreaStatus(false);
        }
    }
}
