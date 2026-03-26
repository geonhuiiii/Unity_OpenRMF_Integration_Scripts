// rmf_fleet_msgs/msg/PathRequest.msg
using System;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;

namespace RosMessageTypes.RmfFleetMsgs
{
    [Serializable]
    public class PathRequestMsg : Message
    {
        public const string k_RosMessageName = "rmf_fleet_msgs/PathRequest";
        public override string RosMessageName => k_RosMessageName;

        public string fleet_name;
        public string robot_name;
        public LocationMsg[] path;
        public string task_id;

        public PathRequestMsg()
        {
            fleet_name = "";
            robot_name = "";
            path = new LocationMsg[0];
            task_id = "";
        }

        public static PathRequestMsg Deserialize(MessageDeserializer deserializer) => new PathRequestMsg(deserializer);

        private PathRequestMsg(MessageDeserializer deserializer)
        {
            deserializer.Read(out fleet_name);
            deserializer.Read(out robot_name);
            int pathLen = deserializer.ReadLength();
            deserializer.Read(out path, LocationMsg.Deserialize, pathLen);
            deserializer.Read(out task_id);
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(fleet_name);
            serializer.Write(robot_name);
            serializer.WriteLength(path);
            serializer.Write(path);
            serializer.Write(task_id);
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
