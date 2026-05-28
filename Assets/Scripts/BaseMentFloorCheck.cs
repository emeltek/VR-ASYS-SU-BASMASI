using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseMentFloorCheck : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && WaterRiseEffect.isRainStarted == true)
        {
            ScoreManager.Instance.SubtractScore(25f);
        }
    }
}
