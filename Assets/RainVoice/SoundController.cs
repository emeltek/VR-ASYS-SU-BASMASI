using UnityEngine;

public class SoundController : MonoBehaviour
{
    public AudioClip loopingSound;
    public AudioClip intervalSound;
    public AudioClip LightSound;

    public float interval = 10f;
    public float lightInterval = 15f; 

    private AudioSource loopingSource;
    private AudioSource intervalSource;
    private AudioSource lightSource;

    private float timer1;
    private float timer2;

    void Start()
    {
        loopingSource = gameObject.AddComponent<AudioSource>();
        intervalSource = gameObject.AddComponent<AudioSource>();
        lightSource = gameObject.AddComponent<AudioSource>();

        
        loopingSource.clip = loopingSound;
        loopingSource.loop = true;
        loopingSource.playOnAwake = false;
        loopingSource.volume = 0.7f;
        loopingSource.Play();

       
        intervalSource.clip = intervalSound;
        intervalSource.loop = false;
        intervalSource.playOnAwake = false;
        intervalSource.volume = 1.0f;

        
        lightSource.clip = LightSound;
        lightSource.loop = false;
        lightSource.playOnAwake = false;
        lightSource.volume = 2.0f;

        timer1 = interval;
        timer2 = lightInterval;
    }

    void Update()
    {
        timer1 -= Time.deltaTime;
        timer2 -= Time.deltaTime;

        if (timer1 <= 0f)
        {
            intervalSource.Play();
            timer1 = interval + Random.Range(-2f, 2f); 
        }

        if (timer2 <= 0f)
        {
            lightSource.Play();
            timer2 = lightInterval + Random.Range(-3f, 3f); 
        }
    }
}
