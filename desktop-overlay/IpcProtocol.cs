using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace FishSocialOverlay
{
    [DataContract]
    public sealed class IpcMessage
    {
        [DataMember(Name = "type")]
        public string Type { get; set; }

        [DataMember(Name = "version")]
        public int Version { get; set; } = 1;

        [DataMember(Name = "sequence")]
        public long Sequence { get; set; }

        [DataMember(Name = "loginState")]
        public string LoginState { get; set; }

        [DataMember(Name = "connectionState")]
        public string ConnectionState { get; set; }

        [DataMember(Name = "pondName")]
        public string PondName { get; set; }

        [DataMember(Name = "fishingPhase")]
        public string FishingPhase { get; set; }

        [DataMember(Name = "command")]
        public string Command { get; set; }

        public static IpcMessage Parse(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(IpcMessage));
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return serializer.ReadObject(stream) as IpcMessage;
        }

        public string ToJson()
        {
            var serializer = new DataContractJsonSerializer(typeof(IpcMessage));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, this);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
