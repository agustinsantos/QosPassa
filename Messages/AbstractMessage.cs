using System;

namespace WhatsappQS.Message
{
    [Serializable]
    public class AbstractMessage
    {
        public Int32 Sequence { get; set; }

        public Guid Guid { get; set; }

        public override String ToString()
        {
            return "<" + Sequence + ", " + Guid + ">:";
        }

        private static int sequenceCounter = 0;

        public AbstractMessage()
        {
            Sequence = sequenceCounter++;
        }
    }
}
