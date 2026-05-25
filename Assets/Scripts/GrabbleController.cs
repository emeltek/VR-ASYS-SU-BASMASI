using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrabbleController : MonoBehaviour
{
    public GameObject phone;

    public GameObject aidkit;

    public GameObject switchDoor;
    // Start is called before the first frame update
    void Start()
    {
        phone.GetComponent<BoxCollider>().enabled = false;
        aidkit.GetComponent<BoxCollider>().enabled = false;
        switchDoor.GetComponent<BoxCollider>().enabled = false;
    }

    public void EnableBoxCollider()
    {
        phone.GetComponent<BoxCollider>().enabled = true;
        aidkit.GetComponent<BoxCollider>().enabled = true;
        switchDoor.GetComponent<BoxCollider>().enabled = true;
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
