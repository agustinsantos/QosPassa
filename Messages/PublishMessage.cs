using System;

namespace WhatsappQS.Message
{
    [Serializable]
    public class PublishMessage : AbstractMessage
    {
        public string MessageText { get; set; }

        public override String ToString()
        {
            return base.ToString() + "PUBLISH(" + MessageText + ')';
        }
    }
}