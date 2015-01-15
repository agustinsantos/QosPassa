using System;

namespace WhatsappQS.Message
{
    public enum PublicationType
    {
        NONE,
        CAUSAL,
        SEQUENTIAL
    }

    [Serializable]
    public class GetFromResultMessage : AbstractMessage
    {
        public string[] MessageTexts { get; set; }
        public PublicationType PubType { get; set; }

        public override String ToString()
        {
            return base.ToString() + "RESULTGETFROM( #Messages: " + (MessageTexts != null? MessageTexts.Length.ToString() : "NULL") + ", " + PubType + ')';
        }
    }
}