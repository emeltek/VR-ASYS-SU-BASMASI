using UnityEngine;
using System.Collections;

public class WaterFallController : MonoBehaviour
{
    public float delay = 2f;
    public float fadeInDuration = 3f;

    private ParticleSystem[] particleSystems;

    void Start()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>(includeInactive: true);

        
        foreach (ParticleSystem ps in particleSystems)
        {
            var emission = ps.emission;
            emission.rateOverTime = 0f; 
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        
        StartCoroutine(StartParticlesWithFadeIn());
    }

    IEnumerator StartParticlesWithFadeIn()
    {
        yield return new WaitForSeconds(delay);

        float elapsed = 0f;
        float maxRate = 700f;

        
        foreach (ParticleSystem ps in particleSystems)
        {
            ps.Play(); 
        }

        while (elapsed < fadeInDuration)
        {
            float t = elapsed / fadeInDuration;
            float rate = Mathf.Lerp(0f, maxRate, t);

            foreach (ParticleSystem ps in particleSystems)
            {
                var emission = ps.emission;
                emission.rateOverTime = rate;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        
        foreach (ParticleSystem ps in particleSystems)
        {
            var emission = ps.emission;
            emission.rateOverTime = maxRate;
        }
    }
}
