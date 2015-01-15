using System;

namespace WhatsappQS.Message
{
    public enum Role
    {
        CLIENT,
        SERVER,
        ADMIN
    }

    [Serializable]
    public class ConnectMessage : AbstractMessage
    {
        public Role Role { get; set; }

        public override String ToString()
        {
            return base.ToString() + "CONNECT("+ Role + ")";
        }
    }
}