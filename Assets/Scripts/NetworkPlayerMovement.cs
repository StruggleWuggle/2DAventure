using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.VisualScripting;
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
    private HandleStates.TransformStateRW[] _transformStates = new HandleStates.TransformStateRW[buffer];

    // For server based rollback
    public NetworkVariable<HandleStates.TransformStateRW> currentServerTransformState = new NetworkVariable<HandleStates.TransformStateRW>(default, NetworkVariableReadPermission.Everyone);
    public HandleStates.TransformStateRW previousTransformState;

    private void OnServerStateChanged(HandleStates.TransformStateRW previousState, HandleStates.TransformStateRW serverState)
    {
        // Check and reconcile local client predicted movement with server movement
        if (!IsLocalPlayer)
        {
            return;
        }

        // Edge case for first call where no previous states have been stored yet
        if (previousTransformState != null)
        {
            previousTransformState = serverState;
        }

        // Check if client and server agree on corresponding tick
        /*
        HandleStates.TransformStateRW calculatedState = _transformStates.First(localState => serverState.tick == localState.tick);
        if (calculatedState.finalPosition != serverState.finalPosition)
        {
            Debug.Log("Correcting client positon");
            // Then client is out of sync
            //CorrectPlayerPosition(serverState);     // Teleport player at failed tick
            //ReplayMovesAfterTick(serverState);


        }
        */

        previousTransformState = previousState;
    }
    private void CorrectPlayerPosition(HandleStates.TransformStateRW correctedState)
    {
        // Disable character movements TODO

        // Teleport client
        rb.position = correctedState.finalPosition;

        // Find corresponding state in stored state array based on matching tick and update position value
        for (int i = 0; i < _transformStates.Length; i++)
        {
            if (_transformStates[i].tick == correctedState.tick)
            {
                _transformStates[i] = correctedState;
                break;
            }
        }
    }

    private void ReplayMovesAfterTick(HandleStates.TransformStateRW lastValidState)
    {
        // Get all states from stored state array that have a tick value greater than correctedState tick value
        IDictionary<int, HandleStates.InputState> stateDict = new Dictionary<int, HandleStates.InputState>();
        int maxTickValue = 0;
        for (int i = 0; i < _inputStates.Length; i++)
        {
            if (_inputStates[i].tick > lastValidState.tick)
            {
                // Append dictionary
                stateDict[_inputStates[i].tick] = _inputStates[i];

                // Update largest tick value encountered
                if (maxTickValue == 0 || _inputStates[i].tick > maxTickValue)
                {
                    maxTickValue = _inputStates[i].tick;
                }
            }

        }

        // Execute corresponding input states 
        for (int i = lastValidState.tick + 1; i <= maxTickValue; i++)
        {
            Move(stateDict[i].moveX, stateDict[i].moveY);

            // Get new transform state
            HandleStates.TransformStateRW updatedTransformState = new HandleStates.TransformStateRW()
            {
                tick = stateDict[i].tick,
                finalPosition = rb.position,
                isMoving = true,
            };

            // Update corresponding transform state array
            for (int j = 0; j < _transformStates.Length; j++)
            {
                if (_transformStates[j].tick == stateDict[i].tick)
                {
                    _transformStates[j] = updatedTransformState;
                    break;
                }
            }
        }

    }

    private void OnEnable()
    {
        currentServerTransformState.OnValueChanged += OnServerStateChanged;
    }

    // Update is called once per frame
    void Update()
    {
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
    public void ProcessLocalPlayerMovement(float _moveX, float _moveY)
    {
        tickDeltaTime += Time.deltaTime;

        if (tickDeltaTime > tickRate)
        {
            int bufferIndex = tick % buffer;

            MoveServerRpc(_moveX, _moveY, tick);    // Send out move input to server
            //Move(_moveX, _moveY);    // Client side movement only

            // Update states and historic state array
            HandleStates.InputState inputState = new()
            {
                tick = tick,
                moveX = _moveX,
                moveY = _moveY,
            };

            HandleStates.TransformStateRW transformState = new()
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
        //rb.isKinematic= false;
        Vector2 movementVector = new Vector2(moveX, moveY);
        //rb.AddForce(movementVector * MoveSpeed.Value);
        rb.velocity = movementVector * MoveSpeed.Value * 10;
        if (moveX > 0)
        {
            print(movementVector);
        }
    }

    public void UpdateOtherPlayers()
    {
        tickDeltaTime += Time.deltaTime;
        //if (tickDeltaTime > tickRate && currentServerTransformState.Value.isMoving) //
        if (tickDeltaTime > tickRate)
        {
            if (currentServerTransformState.Value != null)
            {
                rb.position = currentServerTransformState.Value.finalPosition;
            }
            //rb.position = new Vector2(.5f, .5f);
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
    //[ServerRpc(RequireOwnership = false)]
    public void MoveServerRpc(float moveX, float moveY, int tick)
    {
        Move(moveX, moveY);
        HandleStates.TransformStateRW transformState = new()
        {
            tick = tick,
            finalPosition = rb.position,
            isMoving = true,
        };
        // TODO
        // Check for packet loss by checking if tick != previousTransformState Tick + 1
        // If missed packet, send packet again

        previousTransformState = currentServerTransformState.Value;
        currentServerTransformState.Value = transformState;


    }
    [ClientRpc]
    public void UpdatePlayerPositionsClientRpc()
    {
        rb.position = currentServerTransformState.Value.finalPosition;
    }
}