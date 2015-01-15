using System;
using System.Collections.Generic;

namespace WhatsappQS.Message
{
    [Serializable]
    public class BroadcastPublishedMessage : AbstractMessage
    {
        public override String ToString()
        {
            return base.ToString() + string.Format("BROADCAST PUBLISHED()");
        }
    }

    [Serializable]
    public class BroadcastUpdatesMessage : AbstractMessage
    {
        public List<string> Updates { get; set; }
        public Guid Turn { get; set; }

        public override String ToString()
        {
            return base.ToString() + string.Format("BROADCAST UPDATES(Turn= {0}, Updates={1})", Turn, string.Join(", ", Updates.ToArray()));
        }

        public override bool Equals(object obj)
        {
            if (Object.ReferenceEquals(this, obj))
                return true;
            BroadcastUpdatesMessage other = obj as BroadcastUpdatesMessage;
            if (other == null)
                return false;
            return this.Guid.Equals(other.Guid);
        }

        public override int GetHashCode()
        {
            return this.Guid.GetHashCode();
        }
    }
}
