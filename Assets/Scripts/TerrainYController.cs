using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainYController : MonoBehaviour
{
    public GameObject terrain;
    // Update is called once per frame
    
    void Update()
    {
        Debug.Log("terrain : "+ terrain.transform.localPosition.y);
    }
}
