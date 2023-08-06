using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class HandleStates : MonoBehaviour
{
    // Start is called before the first frame update
    public class InputState
    {
        public int tick;
        public float moveX;
        public float moveY;
    }
    public class TransformStateRW : INetworkSerializable
    {
        public int tick;
        public Vector2 finalPosition;
        public bool isMoving;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T: IReaderWriter
        {
            if (serializer.IsReader)
            {
                var reader = serializer.GetFastBufferReader();
                reader.ReadValueSafe(out tick);
                reader.ReadValueSafe(out finalPosition);
                reader.ReadValueSafe(out isMoving);
            }
            else
            {
                var writer = serializer.GetFastBufferWriter();
                writer.WriteValueSafe(tick);
                writer.WriteValueSafe(finalPosition);
                writer.WriteValueSafe(isMoving);
            }
        }
    }
}
