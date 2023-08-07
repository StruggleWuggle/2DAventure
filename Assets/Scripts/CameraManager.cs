using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using Unity.Netcode;

public class CameraManager : NetworkBehaviour
{
    [SerializeField] private CinemachineVirtualCamera camera;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (IsOwner)
        {
            camera.Priority = 10;
        }
        else
        {
            camera.Priority = 0;
        }
    }
}
