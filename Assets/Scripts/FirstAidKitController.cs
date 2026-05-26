using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirstAidKitController : MonoBehaviour
{
    public GameObject kit;

    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("LeftHand")|| other.CompareTag("RightHand"))
        {
            if ( WaterRiseEffect.isRainStarted == true)
            {
                kit.SetActive(false);
            }
            
        }
    }
}
