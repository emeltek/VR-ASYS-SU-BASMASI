using UnityEngine;
using UnityEngine.SceneManagement;

public class EnergyManager : MonoBehaviour
{
    public SwitchState[] switches;
   public GameObject electricEffect;
    public AudioSource audioSource;
    public AudioClip switchOffSuccess;
    public AudioClip switchWrongBehaviour;
    public GameObject waterTerrain;


    private bool wasAllOff = true; // önceki durumu takip et

    void Update()
    {
        bool allOff = true;

        foreach (var sw in switches)
        {
            if (sw.isOn)
            {
                allOff = false;
                break;
            }
        }

        

        // Sadece durum değiştiğinde çal
        if (allOff && !wasAllOff)
        {
            if (waterTerrain.transform.localPosition.y > -7.8f)
            {
                audioSource.PlayOneShot(switchWrongBehaviour);
            }
            else
            {
                audioSource.PlayOneShot(switchOffSuccess);
            }
         

           
           
        }

        wasAllOff = allOff;
    }
}