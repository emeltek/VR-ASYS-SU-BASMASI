using UnityEngine;

public class IncreaseEmission : MonoBehaviour
{
    public ParticleSystem particleSystem;
    public float startRate = 10f;
    public float endRate = 100f;
    public float duration = 5f;
    public float prewarmTime = 3f; 

    private float timer = 0f;
    private ParticleSystem.EmissionModule emissionModule;

    void Start()
    {
        if (particleSystem == null)
            particleSystem = GetComponent<ParticleSystem>();

        emissionModule = particleSystem.emission;

        
        SetEmissionRate(startRate);

        
        particleSystem.Simulate(prewarmTime, true, true);
        particleSystem.Play();
    }

    void Update()
    {
        if (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            float targetRate = Mathf.Lerp(startRate, endRate, t);
            SetEmissionRate(targetRate);
        }
    }

    void SetEmissionRate(float rate)
    {
        var rateOverTime = emissionModule.rateOverTime;
        rateOverTime.constant = rate;
        emissionModule.rateOverTime = rateOverTime;
    }
}
