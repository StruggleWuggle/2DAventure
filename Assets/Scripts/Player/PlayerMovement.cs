using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed;
    public Rigidbody2D rb;
    public float collisionOffset;
    public Vector2 positionRB;
    public ContactFilter2D movementFilter;

    private Vector2 moveDirection;  // Also facing direction
    public Vector2 facingDirection;

    List<RaycastHit2D> collisionCasts = new List<RaycastHit2D>();

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
        positionRB = rb.position; // Update rigid body position as fast as possible
}

    private void FixedUpdate()
    {
        bool moveSuccess;
        // Only update facingDirection vector and velocity vector when moveDirection vector is non zero
        if (moveDirection != Vector2.zero)
        {
            moveSuccess = TryDirection(moveDirection);
            if (!moveSuccess)
            {
                moveSuccess = TryDirection(new Vector2(moveDirection.x, 0));
                if (!moveSuccess)
                {
                    moveSuccess = TryDirection(new Vector2(0, moveDirection.y));
                }
            }
        }
        else
        {
            rb.velocity = Vector2.zero;
        }
    }

    private bool TryDirection(Vector2 direction)
    {
        // Check for potential collisions in given vector direction. If direction is possible, move player and return true
        int count = rb.Cast(direction, movementFilter, collisionCasts, (moveSpeed * Time.fixedDeltaTime + collisionOffset));

        if(count == 0)
        {
            Vector2 currentMove = moveSpeed * direction;
            rb.velocity = currentMove;
            return true;
        }
        else
        {
            return false;
        }
    }
}
