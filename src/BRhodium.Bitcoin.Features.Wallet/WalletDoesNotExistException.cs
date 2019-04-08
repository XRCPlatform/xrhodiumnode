using System;
using System.Runtime.Serialization;

namespace BRhodium.Bitcoin.Features.Wallet
{
    [Serializable]
    internal class WalletDoesNotExistException : Exception
    {
        public WalletDoesNotExistException()
        {
        }

        public WalletDoesNotExistException(string message) : base(message)
        {
        }

        public WalletDoesNotExistException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected WalletDoesNotExistException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}