using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SnowMesh : MonoBehaviour
{
    public SnowInteractive snowManager;

    //If object actually collides with snow, than call deform function
    public void OnTriggerStay(Collider other)
    {
        if(other.tag != "Ground")
            snowManager.DeformSnow(other.gameObject);
    }
}
