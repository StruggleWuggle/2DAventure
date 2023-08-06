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

    // Client prediction
    private int tick = 0;
    private float tickRate = 1f / 60f;
    private float tickDeltaTime = 0;
    private const int buffer = 1024;

    private HandleStates.InputState[] _inputStates = new HandleStates.InputState[buffer];
    private HandleStates.TranssformStateRW[] _transformStates = new HandleStates.TranssformStateRW[buffer];

    // For server based rollback
    public NetworkVariable<HandleStates.TranssformStateRW> currentServerTransformState = new();
    public HandleStates.TranssformStateRW previousTransformState;

    // Start is called before the first frame update
    void Start()
    {

    }

    private void OnServerStateChanged(HandleStates.TranssformStateRW previousValue, HandleStates.TranssformStateRW newValue)
    {
        previousTransformState= previousValue;
    }
    private void OnEnable()
    {
        currentServerTransformState.OnValueChanged += OnServerStateChanged;
    }

    // Update is called once per frame
    void Update()
    {
        // Check if object has correct ownership
        if (!IsOwner)
        {
            //return;
        }

        if (IsClient && IsLocalPlayer)
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveY = Input.GetAxisRaw("Vertical");
            ProcessLocalPlayerMovement(moveX, moveY);
        }
        else
        {
            UpdateOtherPlayers();
        }
    }
    void UpdateServer()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        Vector2 movementVector = new Vector2(moveX, moveY);
        rb.velocity = movementVector * MoveSpeed.Value;
    }
    public void ProcessLocalPlayerMovement(float _moveX, float _moveY)
    {
        tickDeltaTime += Time.deltaTime;

        if (tickDeltaTime > tickRate)
        {
            int bufferIndex = tick % buffer;

            MoveServerRpc(_moveX, _moveY, tick);    // Send out move input to server
            Move(_moveX, _moveY);    // Client side movement only

            // Update states and historic state array
            HandleStates.InputState inputState = new()
            {
                tick = tick,
                moveX = _moveX,
                moveY = _moveY,
            };

            HandleStates.TranssformStateRW transformState = new()
            {
                tick = tick,
                finalPosition = rb.position,
                isMoving = true,
            };
            _inputStates[bufferIndex] = inputState;
            _transformStates[bufferIndex] = transformState;

            // Reduce tick rate back down and prevent overflow of buffer
            tickDeltaTime -= tickRate;
            if (tick >= buffer)
            {
                tick = 0;
            }
            else
            {
                tick++;
            }
        }
    }
    public void Move(float moveX, float moveY)
    {
        Vector2 movementVector = new Vector2(moveX, moveY);
        rb.velocity = movementVector * MoveSpeed.Value;
    }

    public void UpdateOtherPlayers()
    {
        tickDeltaTime += Time.deltaTime;
        print(tickDeltaTime);
        //if (tickDeltaTime > tickRate && currentServerTransformState.Value.isMoving)
        if (tickDeltaTime > tickRate)
        {
            rb.position = currentServerTransformState.Value.finalPosition;
        }

        //tickDeltaTime -= tickRate;
        if (tick >= buffer)
        {
            tick = 0;
        }
        else
        {
            tick++;
        }
    }

    // --- ServerRPCs ---

    [ServerRpc]
    public void MoveServerRpc(float moveX, float moveY, int tick)
    {
        Move(moveX, moveY);
        HandleStates.TranssformStateRW transformState = new()
        {
            tick = tick,
            finalPosition = rb.position,
            isMoving = true,
        };
        previousTransformState = currentServerTransformState.Value;
        currentServerTransformState.Value = transformState;

    }
}
