using System;

namespace WhatsappQS.Message
{
    [Serializable]
    public class DisconnectMessage : AbstractMessage
    {
        public override String ToString()
        {
            return base.ToString() + "DISCONNECT()";
        }
    }
}