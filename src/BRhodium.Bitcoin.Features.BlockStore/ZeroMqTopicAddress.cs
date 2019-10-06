using System;

namespace BRhodium.Bitcoin.Features.BlockStore
{
    public class ZeroMqTopicAddress
    {
        public ZeroMqTopicAddress(string blockNotifyZeroCmd)
        {
            string[] parts = blockNotifyZeroCmd.Split('|');
            if (parts.Length == 3)
            {
                Address = parts[0];
                Topic = parts[1];
                Message = parts[2];
            }            
        }

        public string Topic { get; set; }
        public string Address { get; set; }
        public string Message { get; set; }

        public string RenderMessage(string instanceMessage)
        {
            return instanceMessage;
        }
    }
}