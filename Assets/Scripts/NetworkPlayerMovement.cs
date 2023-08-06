using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerMovement : NetworkBehaviour
{
    // References
    //public NetworkVariable<Rigidbody2D> rb;
    public Rigidbody2D rb;
    public ContactFilter2D movementFilter;

    // Player stats
    public NetworkVariable<float> MoveSpeed = new NetworkVariable<float>(2f);
    public NetworkVariable<int> Health = new NetworkVariable<int>(100);

    // Physics
    public float collisionOffset = 0f;
    public NetworkVariable<Vector2> facingDirection;
    public NetworkVariable<Vector2> playerPosition;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // Check if object has correct ownership
        if (!IsOwner)
        {
            return;
        }

        if (IsServer)
        {
            UpdateServer();
        }

        if (IsClient)
        {
            UpdateClient();
        }
    }
    void UpdateServer()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        Vector2 movementVector = new Vector2(moveX, moveY);
        rb.velocity = movementVector * MoveSpeed.Value;
    }
    void UpdateClient()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        // Send out server side movement command
        MoveServerRpc(moveX, moveY);

    }

    // --- ServerRPCs ---

    [ServerRpc]
    public void MoveServerRpc(float moveX, float moveY)
    {
        //rb.velocity = Vector2.zero;
        Vector2 movementVector = new Vector2(moveX, moveY);
        rb.velocity = movementVector * MoveSpeed.Value;
    }
}
