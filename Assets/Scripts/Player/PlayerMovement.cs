using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed;
    public Rigidbody2D rb;
    public float collisionOffset = 0.5f;
    public Vector2 positionRB;

    private Vector2 moveDirection;  // Also facing direction
    public Vector2 facingDirection;

    List<RaycastHit2D> castCollisions = new List<RaycastHit2D>();

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        moveDirection= new Vector2(moveX, moveY).normalized;
        positionRB = rb.position;
    }

    private void FixedUpdate()
    {
        // Only update facingDirection vector and velocity vector when moveDirection vector is non zero
        if (math.any(moveDirection))
        {
            rb.velocity = new Vector2(moveDirection.x * moveSpeed, moveDirection.y * moveSpeed);
            facingDirection = new Vector2(moveDirection.x, moveDirection.y);
        }
        else
        {
            rb.velocity = Vector2.zero;
        }
    }
}
