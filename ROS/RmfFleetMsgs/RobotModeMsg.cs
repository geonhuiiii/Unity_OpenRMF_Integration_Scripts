// rmf_fleet_msgs/msg/RobotMode.msg (uint32 mode + constants)
using System;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

namespace RosMessageTypes.RmfFleetMsgs
{
    [Serializable]
    public class RobotModeMsg : Message
    {
        public const string k_RosMessageName = "rmf_fleet_msgs/RobotMode";
        public override string RosMessageName => k_RosMessageName;

        public const uint MODE_IDLE = 0;
        public const uint MODE_CHARGING = 1;
        public const uint MODE_MOVING = 2;
        public const uint MODE_PAUSED = 3;
        public const uint MODE_WAITING = 4;
        public const uint MODE_EMERGENCY = 5;
        public const uint MODE_GOING_HOME = 6;
        public const uint MODE_DOCKING = 7;
        public const uint MODE_ADAPTER_ERROR = 8;
        public const uint MODE_CLEANING = 9;

        public uint mode;
        public ulong mode_request_id;

        public RobotModeMsg()
        {
            mode = MODE_IDLE;
            mode_request_id = 0;
        }

        public RobotModeMsg(uint mode, ulong mode_request_id = 0)
        {
            this.mode = mode;
            this.mode_request_id = mode_request_id;
        }

        public static RobotModeMsg Deserialize(MessageDeserializer deserializer) => new RobotModeMsg(deserializer);

        private RobotModeMsg(MessageDeserializer deserializer)
        {
            deserializer.Read(out mode);
            deserializer.Read(out mode_request_id);
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(mode);
            serializer.Write(mode_request_id);
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod]
#endif
        public static void Register()
        {
            MessageRegistry.Register(k_RosMessageName, Deserialize);
        }
    }
}
