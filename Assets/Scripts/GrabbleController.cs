using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrabbleController : MonoBehaviour
{
    public GameObject phone;

    public GameObject aidkit;

    public GameObject switchDoor;

    public GameObject switch1;
    public GameObject switch2;
    public GameObject switch3;
    public GameObject switch4;
    // Start is called before the first frame update
    void Start()
    {
        phone.GetComponent<BoxCollider>().enabled = false;
        aidkit.GetComponent<BoxCollider>().enabled = false;
        switchDoor.GetComponent<BoxCollider>().enabled = false;
        switch1.GetComponent<BoxCollider>().enabled = false;
        switch2.GetComponent<BoxCollider>().enabled = false;
        switch3.GetComponent<BoxCollider>().enabled = false;
        switch4.GetComponent<BoxCollider>().enabled = false;
    }

    public void EnableBoxCollider()
    {
        phone.GetComponent<BoxCollider>().enabled = true;
        aidkit.GetComponent<BoxCollider>().enabled = true;
        switchDoor.GetComponent<BoxCollider>().enabled = true;
        switch1.GetComponent<BoxCollider>().enabled = true;
        switch2.GetComponent<BoxCollider>().enabled = true;
        switch3.GetComponent<BoxCollider>().enabled = true;
        switch4.GetComponent<BoxCollider>().enabled = true;
    }
  
}
