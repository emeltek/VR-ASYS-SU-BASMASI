using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class RoadRain : MonoBehaviour
{
    public float speedIncreaseRate = 0.5f;  
    private Material rainMaterial;
    private float currentSpeed;
    public float maxSpeed = 4.0f;

    void Start()
    {

        rainMaterial = GetComponent<MeshRenderer>().material;

        
        currentSpeed = rainMaterial.GetFloat("_Speed");
    }

    void Update()
    {
        
        if (currentSpeed < maxSpeed)
        {
            currentSpeed += speedIncreaseRate * Time.deltaTime;
            currentSpeed = Mathf.Min(currentSpeed, maxSpeed); 
            rainMaterial.SetFloat("_Speed", currentSpeed);
        }
    }
}
