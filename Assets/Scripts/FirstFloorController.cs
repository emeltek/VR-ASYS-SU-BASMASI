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

    private bool checkedOnce;

    /// <summary>
    /// Su en yüksek seviyeye ulaşınca çağrılır (WaterRiseEffect, iki su parçası için iki kez çağırabilir).
    /// Eğitimde uyarı sesi çalar. Sınavda konum cezası senaryo sonunda GNN'den gelir (GnnRiskScorer).
    /// </summary>
    public void FirstFloorCheck()
    {
        if (checkedOnce) return;
        checkedOnce = true;

        if (isCharacterEntered && isExamScene == false && firstFloorAudio != null)
        {
            firstFloorAudio.Play();
        }
    }
    
}
