using UnityEngine;

public class RiverRise : MonoBehaviour
{
    public float riseAmount = 1f;
    public float riseDuration = 10f;

    private float startY;
    private float targetY;
    private float elapsedTime = 0f;

    public float ElapsedTime => elapsedTime; 

    void Start()
    {
        startY = transform.position.y;
        targetY = startY + riseAmount;
    }

    void Update()
    {
        if (elapsedTime < riseDuration)
        {
            elapsedTime += Time.deltaTime;
            float newY = Mathf.Lerp(startY, targetY, elapsedTime / riseDuration);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }
}
