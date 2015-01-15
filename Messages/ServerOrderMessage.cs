using System;
using System.Linq;

namespace WhatsappQS.Message
{
    [Serializable]
    public class ServerOrderMessage : AbstractMessage
    {
        public Guid[] Order { get; set; }

        public override String ToString()
        {
            return base.ToString() + "ORDER(" + string.Join(", ", Order.ToList()) + ")";
        }
    }
}