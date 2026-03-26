// rmf_fleet_msgs/msg/Location.msg
using System;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using RosMessageTypes.BuiltinInterfaces;

namespace RosMessageTypes.RmfFleetMsgs
{
    [Serializable]
    public class LocationMsg : Message
    {
        public const string k_RosMessageName = "rmf_fleet_msgs/Location";
        public override string RosMessageName => k_RosMessageName;

        public TimeMsg t;
        public float x;
        public float y;
        public float yaw;
        public bool obey_approach_speed_limit;
        public float approach_speed_limit;
        public string level_name;
        public ulong index;

        public LocationMsg()
        {
            t = new TimeMsg();
            x = 0f;
            y = 0f;
            yaw = 0f;
            obey_approach_speed_limit = false;
            approach_speed_limit = 0f;
            level_name = "";
            index = 0;
        }

        public LocationMsg(TimeMsg t, float x, float y, float yaw, bool obey_approach_speed_limit, float approach_speed_limit, string level_name, ulong index)
        {
            this.t = t;
            this.x = x;
            this.y = y;
            this.yaw = yaw;
            this.obey_approach_speed_limit = obey_approach_speed_limit;
            this.approach_speed_limit = approach_speed_limit;
            this.level_name = level_name;
            this.index = index;
        }

        public static LocationMsg Deserialize(MessageDeserializer deserializer) => new LocationMsg(deserializer);

        private LocationMsg(MessageDeserializer deserializer)
        {
            this.t = TimeMsg.Deserialize(deserializer);
            deserializer.Read(out x);
            deserializer.Read(out y);
            deserializer.Read(out yaw);
            deserializer.Read(out obey_approach_speed_limit);
            deserializer.Read(out approach_speed_limit);
            deserializer.Read(out level_name);
            deserializer.Read(out index);
        }

        public override void SerializeTo(MessageSerializer serializer)
        {
            serializer.Write(this.t);
            serializer.Write(this.x);
            serializer.Write(this.y);
            serializer.Write(this.yaw);
            serializer.Write(this.obey_approach_speed_limit);
            serializer.Write(this.approach_speed_limit);
            serializer.Write(this.level_name);
            serializer.Write(this.index);
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
