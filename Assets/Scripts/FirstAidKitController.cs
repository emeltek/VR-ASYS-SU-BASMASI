using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirstAidKitController : MonoBehaviour
{
    public GameObject kit;
    public bool isExamScene = false;
    private bool collected = false;
    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (other.CompareTag("LeftHand")|| other.CompareTag("RightHand"))
        {
            if ( WaterRiseEffect.isRainStarted == true)
            {
                collected = true;
                kit.SetActive(false);
                if (isExamScene == false)
                {
                    ScoreManager.Instance.AddScore(10f);
                }
            }
            
        }
    }
}
