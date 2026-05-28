using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InWaterTriggerController : MonoBehaviour
{
   public AudioSource inwaterAudio;
   public bool isExamScene = false;

   private void OnTriggerEnter(Collider other)
   {
      if (other.CompareTag("Water") && WaterRiseEffect.isRainStarted ==true)
      {
         if (isExamScene == false)
         {
            inwaterAudio.Play();

         }

         if (isExamScene == true)
         {
            ScoreManager.Instance.SubtractScore(25f);
            
         }
      }
   }
}
