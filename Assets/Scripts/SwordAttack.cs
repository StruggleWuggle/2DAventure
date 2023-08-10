using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwordAttack : MonoBehaviour
{
    Collider2D swordCollider;
    Vector2 attackOffsetFromCharacter;

    // Start is called before the first frame update
    void Start()
    {
        swordCollider = GetComponent<Collider2D>();
        attackOffsetFromCharacter = transform.position;
    }
    public void StartAttack(Vector2 facingDirection)
    {
        swordCollider.enabled = true;
        // Convert facing direction into a attack direction 
        string attackDirection = FindDominantAxis(facingDirection);

        // Move hitbox according to dominant axis
        if (attackDirection == "Left")
        {
            AttackLeft();
        }
        else if (attackDirection == "Right")
        {
            AttackRight();
        }
        else if (attackDirection == "Up")
        {
            AttackUp();
        }
        else
        {
            AttackDown();
        }
    }
    public void StopAttack()
    {
        swordCollider.enabled = false;
    }

    private void AttackRight()
    {
        transform.position = attackOffsetFromCharacter;
        print("Attacked right.");
    }
    private void AttackLeft()
    {
        transform.position = new Vector2(attackOffsetFromCharacter.x * -1, attackOffsetFromCharacter.y);
        print("Attacked left.");
    }
    private void AttackUp()
    {
        print("Attacked up.");
    }
    private void AttackDown()
    {
        print("Attacked down.");
    }

    private string FindDominantAxis(Vector2 direction2D)
    {
        float[] directionalMagnitude = new float[]
        {
            (Mathf.Abs(direction2D.x) - direction2D.x)/2, // Left
            (Mathf.Abs(direction2D.x) + direction2D.x)/2, // Right
            (Mathf.Abs(direction2D.y) + direction2D.y)/2, // Up
            (Mathf.Abs(direction2D.y) - direction2D.y)/2  // Down 
        };

        float currentMax = directionalMagnitude[0];
        int currentMaxIndx = 0;
        for (int i = 0; i < directionalMagnitude.Length; i++)
        {
            if (currentMax < directionalMagnitude[i])
            {
                currentMax = directionalMagnitude[i];
                currentMaxIndx = i;
            }
        }

        string dominantAxis = "Null";
        switch (currentMaxIndx)
        {
            case 0:
                dominantAxis = "Left";
                break;
            case 1:
                dominantAxis = "Right";
                break;
            case 2:
                dominantAxis = "Up";
                break;
            case 3:
                dominantAxis = "Down";
                break;
        }

        return dominantAxis;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
