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
            }
        }
    }

    /// <summary>
    /// Oyuncu senaryo başlamadan güvenli alanın içindeyse OnTriggerEnter gelmez;
    /// yağmur başladıktan sonra içeride olduğu da işaretlenir (her girişte, yalnızca ilkinde değil).
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player") || WaterRiseEffect.isRainStarted == false || isinSafeAreaController) return;
        isinSafeAreaController = true;
        if (tracker != null) tracker.SetSafeAreaStatus(true);
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
