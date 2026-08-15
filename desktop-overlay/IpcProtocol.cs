using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace FishSocialOverlay
{
    [DataContract]
    public sealed class OverlaySpotDto
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "x")]
        public float X { get; set; }

        [DataMember(Name = "y")]
        public float Y { get; set; }
    }

    [DataContract]
    public sealed class OverlayUserDto
    {
        [DataMember(Name = "playerId")]
        public string PlayerId { get; set; }

        [DataMember(Name = "userId")]
        public string UserId { get; set; }

        [DataMember(Name = "nickname")]
        public string Nickname { get; set; }

        [DataMember(Name = "spotId")]
        public string SpotId { get; set; }

        [DataMember(Name = "x")]
        public float X { get; set; }

        [DataMember(Name = "y")]
        public float Y { get; set; }

        [DataMember(Name = "hasPosition")]
        public bool HasPosition { get; set; }

        [DataMember(Name = "petVisualState")]
        public string PetVisualState { get; set; }

        [DataMember(Name = "isBot")]
        public bool IsBot { get; set; }
    }

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

        [DataMember(Name = "petVisualState")]
        public string PetVisualState { get; set; }

        [DataMember(Name = "pondId")]
        public string PondId { get; set; }

        [DataMember(Name = "ownSpotId")]
        public string OwnSpotId { get; set; }

        [DataMember(Name = "ownX")]
        public float OwnX { get; set; }

        [DataMember(Name = "ownY")]
        public float OwnY { get; set; }

        [DataMember(Name = "hasOwnPosition")]
        public bool HasOwnPosition { get; set; }

        [DataMember(Name = "spots")]
        public OverlaySpotDto[] Spots { get; set; }

        [DataMember(Name = "users")]
        public OverlayUserDto[] Users { get; set; }

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
