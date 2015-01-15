using System;

namespace WhatsappQS.Message
{
    [Serializable]
    public class GetFromRequestMessage : AbstractMessage
    {
        public int MessageOrder { get; set; }

        public override String ToString()
        {
            return base.ToString() + "GETFROM(" + MessageOrder + ')';
        }
    }
}