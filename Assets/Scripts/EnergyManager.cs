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
    public bool isExamScene = false;
    public ScoreManager scoreManager;

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
                if (isExamScene == false)
                {
                    audioSource.PlayOneShot(switchWrongBehaviour);

                }
                ScoreManager.Instance.SubtractScore(20f);
            }
            else
            {
                if (isExamScene == false)
                {
                    audioSource.PlayOneShot(switchOffSuccess);

                }
                ScoreManager.Instance.AddScore(20f);
            }
         

           
           
        }

        wasAllOff = allOff;
    }
}