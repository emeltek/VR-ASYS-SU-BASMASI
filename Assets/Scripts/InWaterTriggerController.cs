using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InWaterTriggerController : MonoBehaviour
{
   public AudioSource inwaterAudio;

   private void OnTriggerEnter(Collider other)
   {
      if (other.CompareTag("Water") && WaterRiseEffect.isRainStarted ==true)
      {
         inwaterAudio.Play();
      }
   }
}
