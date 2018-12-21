namespace BRhodium.Bitcoin.Features.Wallet
{
    public class WalletNotification
    {
        private TransactionNotificationType trigger;
        private string command;

        public WalletNotification(string subcription)
        {
            if (subcription.Contains(":") && (subcription.Contains("recieved") | subcription.Contains("sent")))
            {
                
                string trigger = subcription.Substring(0, subcription.IndexOf(":"));
                string command = subcription.Substring(subcription.IndexOf(":") + 1);

                switch (trigger.ToLower())
                {
                    case "recieved":
                        this.trigger = TransactionNotificationType.Received;
                        break;
                    case "sent":
                        this.trigger = TransactionNotificationType.Sent;
                        break;
                    default:
                        break;
                }
                this.command = command;               
            }
            else
            {
                trigger = TransactionNotificationType.All;
                this.command = subcription;
            }
        }

        public TransactionNotificationType Trigger { get => this.trigger; set => this.trigger = value; }
        public string Command { get => this.command; set => this.command = value; }
    }
}