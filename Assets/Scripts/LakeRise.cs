using UnityEngine;

public class LakeRise : MonoBehaviour

{
    public float riseAmount = 2f;         
    public float riseDuration = 10f;      
    private float startY;
    private float targetY;
    private float elapsedTime = 0f;

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
