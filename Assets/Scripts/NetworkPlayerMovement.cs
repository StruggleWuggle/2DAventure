using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
    public NetworkVariable<float> MoveSpeed = new NetworkVariable<float>(0.8f);
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
    private float ROLLBACK_THRESHOLD = .01f;

    // Set player keys here
    KeyCode RunInput = KeyCode.LeftShift;
    KeyCode AttackInput = KeyCode.Space;
    

    // Check for player inputs
    private float inputMoveX = 0;
    private float inputMoveY = 0;
    private bool runDownPress = false;

    // Animations
    Animator animator;
    SpriteRenderer spriteRenderer;
    private string currentAnimationState;
    public enum animationState
    {
        Idle,
        Walking,
        Running,
        Attacking
    }

    Dictionary<int, string> animationStateToString = new Dictionary<int, string>()
    {
        {(int)animationState.Idle, "player_idle" },
        {(int)animationState.Running, "player_run" },
        {(int)animationState.Walking, "player_walk" },
        {(int)animationState.Attacking, "player_attack" }
    };

    // Debugging
    public float timeSincePositionCheck = 0f;

    private void OnServerStateChanged(HandleStates.TransformStateRW clientState, HandleStates.TransformStateRW serverState)
    {
        // Check and reconcile local client predicted movement with server movement
        if (!IsLocalPlayer || IsHost)
        {
            return;
        }
        // Edge case for first call where no previous states have been stored yet
        if (previousTransformState == null)
        {
            previousTransformState = serverState;
            return;
        }

        // Check if client and server agree on corresponding tick
        //HandleStates.TransformStateRW calculatedState = _transformStates.First(localState => serverState.tick == localState.tick);
        HandleStates.TransformStateRW calculatedState = null;
        for (int i = 0; i < _transformStates.Length; i++)
        {
            if (_transformStates[i] != null && _transformStates[i].tick == serverState.tick)
            {
                calculatedState = _transformStates[i];
                print(calculatedState.tick);
                print(serverState.tick);
                break;
            }
        }

        if (calculatedState != null)
        {
            //timeSincePositionCheck = 0f;
            //CorrectPlayerPosition(serverState); // For some reasoning moving it into the if statement below causes a huge delay
            float deltaX = Mathf.Abs(calculatedState.finalPosition.x - serverState.finalPosition.x);
            float deltaY = Mathf.Abs(calculatedState.finalPosition.y - serverState.finalPosition.y);
            if (deltaX > ROLLBACK_THRESHOLD || deltaY > ROLLBACK_THRESHOLD)
            {
                timeSincePositionCheck = 0f;
                // Then client is out of sync
                print("Correcting client positon");

                // Teleport player and update corresponding state register
                //rb.position = serverState.finalPosition;
                // Find corresponding state in stored state array based on matching tick and update position value
                for (int i = 0; i < _transformStates.Length; i++)
                {
                    if (_transformStates[i] != null && _transformStates[i].tick == serverState.tick)
                    {
                        //_transformStates[i+1] = serverState;
                        break;
                    }
                }

                // Replay moves after server tick
                //ReplayMovesAfterTick(serverState);


                //CorrectPlayerPosition(serverState);     // Teleport player at failed tick
                //ReplayMovesAfterTick(serverState);
            }
        }

        previousTransformState = clientState;
    }
    private void CorrectPlayerPosition(HandleStates.TransformStateRW correctedState)
    {
        print("Teleporting");
        // Teleport client

        rb.isKinematic = true;

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

        rb.isKinematic = false;
    }

    private void ReplayMovesAfterTick(HandleStates.TransformStateRW lastValidState)
    {
        // Get all states from stored state array that have a tick value greater than correctedState tick value
        IDictionary<int, HandleStates.InputState> stateDict = new Dictionary<int, HandleStates.InputState>();

        for (int i = 0; i < _inputStates.Length; i++)
        {
            if (_inputStates[i] == null)
            {
                continue;
            }
            
            if (_inputStates[i].tick > lastValidState.tick)
            {
                // Append dictionary
                stateDict[_inputStates[i].tick] = _inputStates[i];
            }

            // Update largest tick value encountered
            if (_inputStates[i].tick > tick)
            {
                break;
            }

        }

        // Execute corresponding input states 
        for (int i = lastValidState.tick + 1; i <= tick; i++)
        {
            // Check if i > 1024
            if (i == buffer)
            {
                break;
            }

            Move(stateDict[i].moveX, stateDict[i].moveY, false); //NEED TO ACCOUNT FOR RUNNING

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
    private void Start()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();   
    }

    private void Update()
    {
        if (IsClient && IsLocalPlayer)
        {
            inputMoveX = Input.GetAxisRaw("Horizontal");
            inputMoveY = Input.GetAxisRaw("Vertical");
            runDownPress = Input.GetKey(RunInput);
            print(runDownPress);
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // Flip sprite if needed
        if (inputMoveX < 0)
        {
            //FlipSpriteClientRpc(true);
        }
        else
        {
            //FlipSpriteClientRpc(false);
        }

        if (IsClient && IsLocalPlayer)
        {
            // Update player physics based on movement inputs
            if (inputMoveX != 0 || inputMoveY != 0)
            {   
                //UpdateAnimationStateServerRpc(animationStateToString[(int)animationState.Walking]);
                //UpdateAnimationState(animationStateToString[(int)animationState.Walking]);
                ProcessLocalPlayerMovement(inputMoveX, inputMoveY);
            }
            else
            {
                //UpdateAnimationStateServerRpc(animationStateToString[(int)animationState.Idle]);
                //UpdateAnimationState(animationStateToString[(int)animationState.Idle]);
            }
            //bool isAttacking = Input.GetKey(Attack);
        }
        else
        {
            UpdateOtherPlayers();
        }
    }
    public void ProcessLocalPlayerMovement(float _moveX, float _moveY)
    {
        tickDeltaTime += Time.fixedDeltaTime;

        if (tickDeltaTime > tickRate)
        {
            int bufferIndex = tick % buffer;

            MoveServerRpc(_moveX, _moveY, tick, runDownPress);    // Send out move input to server
            Move(_moveX, _moveY, runDownPress);    // Client side movement only

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

            //Reduce tick rate back down and prevent overflow of buffer
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
    public void Move(float moveX, float moveY, bool isRunning)
    {
        Vector2 movementVector = new Vector2(moveX, moveY).normalized;

        if (isRunning)
        {
            movementVector = movementVector * MoveSpeed.Value * 2;
        }
        else
        {
            movementVector = movementVector * MoveSpeed.Value;
        }
        rb.AddForce(movementVector);
    }

    public void UpdateOtherPlayers()
    {
        // Method to upgate rigidbody positions of all other players

        tickDeltaTime += Time.fixedDeltaTime;

        if (currentServerTransformState.Value == null)
        {
            return;
        }
        if (tickDeltaTime > tickRate && currentServerTransformState.Value.isMoving)
        {
            if (IsClient)
            {
                rb.position = currentServerTransformState.Value.finalPosition;
            }

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

    private void UpdateAnimationState(string newAnimationState)
    {
        // Animation gaurd stopping the same animation from playing repeatedly
        if (currentAnimationState == newAnimationState) return;

        // Play animation
        animator.Play(newAnimationState);

        // Update current animation state
        currentAnimationState = newAnimationState;
    }

    // --- RPCs ---
    [ClientRpc]
    public void FlipSpriteClientRpc(bool flipState)
    {
        spriteRenderer.flipX = flipState;
    }
    [ServerRpc]
    public void FlipSpriteServerRpc(bool flipState)
    {
        spriteRenderer.flipX = flipState;
    }
    [ServerRpc]
    private void UpdateAnimationStateServerRpc(string newAnimationState)
    {
        // Animation gaurd stopping the same animation from playing repeatedly
        if (currentAnimationState == newAnimationState) return;

        // Play animation
        animator.Play(newAnimationState);

        // Update current animation state
        currentAnimationState = newAnimationState;
    }

    [ServerRpc]
    public void MoveServerRpc(float moveX, float moveY, int tick, bool isRunning)
    {
        Move(moveX, moveY, isRunning);
        HandleStates.TransformStateRW transformState = new()
        {
            tick = tick,
            finalPosition = rb.position,
            isMoving = true,
        };

        // Check for packet loss by checking if tick != previousTransformState Tick + 1
        // If missed packet, send packet again
        previousTransformState = currentServerTransformState.Value;
        currentServerTransformState.Value = transformState;
        int tickToBeCompared = 0;
        if (previousTransformState != null)
        {
            tickToBeCompared = previousTransformState.tick + 1;
            if (tickToBeCompared > buffer)
            {
                tickToBeCompared = 0;
            }
        }
        if (tick != tickToBeCompared)
        {
            // Then packet loss has occured
            print("Packet loss");
            print(tick);
            print(previousTransformState.tick + 1);
        }

    }
}
