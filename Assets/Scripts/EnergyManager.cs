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
    private bool scored = false;   // şalter puanı bir kere verilsin

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
            bool waterTooHigh = waterTerrain.transform.localPosition.y > -7.8f;

            if (isExamScene == false)
            {
                audioSource.PlayOneShot(waterTooHigh ? switchWrongBehaviour : switchOffSuccess);
            }

            if (!scored)
            {
                scored = true;
                if (waterTooHigh)
                    ScoreManager.Instance.SubtractScore(20f);
                else
                    ScoreManager.Instance.AddScore(20f);
            }
        }

        wasAllOff = allOff;
    }
}
