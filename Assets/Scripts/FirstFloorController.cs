using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirstFloorController : MonoBehaviour
{
    public static bool isCharacterEntered;
    public AudioSource firstFloorAudio;
    public bool isExamScene = false;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isCharacterEntered = true;
        }
        
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isCharacterEntered = false;
        }
    }

    public void FirstFloorCheck()
    {
        if (isCharacterEntered)
        {
            if (isExamScene == true)
            {
                ScoreManager.Instance.SubtractScore(20f);
            }

            if (isExamScene == false)
            {
                firstFloorAudio.Play();

            }

        }
    }
    
}
