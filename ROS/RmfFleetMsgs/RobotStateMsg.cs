// rmf_fleet_msgs/msg/RobotState.msg
using System;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

namespace RosMessageTypes.RmfFleetMsgs
{
    [Serializable]
    public class RobotStateMsg : Message
    {
        public const string k_RosMessageName = "rmf_fleet_msgs/RobotState";
        public override string RosMessageName => k_RosMessageName;

        public string name;
        public string model;
        public string task_id;
        public ulong seq;
        public RobotModeMsg mode;
        public float battery_percent;
        public LocationMsg location;
        public LocationMsg[] path;

        public RobotStateMsg()
        {
            name = "";
            model = "";
            task_id = "";
            seq = 0;
            mode = new RobotModeMsg();
            battery_percent = 0f;
            location = new LocationMsg();
            path = new LocationMsg[0];
        }

        public static RobotStateMsg Deserialize(MessageDeserializer deserializer) => new RobotStateMsg(deserializer);

        private RobotStateMsg(MessageDeserializer deserializer)
        {
            deserializer.Read(out name);
            deserializer.Read(out model);
            deserializer.Read(out task_id);
            deserializer.Read(out seq);
            mode = RobotModeMsg.Deserialize(deserializer);
            deserializer.Read(out battery_percent);
            location = LocationMsg.Deserialize(deserializer);
            int pathLen = deserializer.ReadLength();
            deserializer.Read(out path, LocationMsg.Deserialize, pathLen);
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(name);
            serializer.Write(model);
            serializer.Write(task_id);
            serializer.Write(seq);
            serializer.Write(mode);
            serializer.Write(battery_percent);
            serializer.Write(location);
            serializer.WriteLength(path);
            serializer.Write(path);
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
