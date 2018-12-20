using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BRhodium.Node.Utilities;

namespace BRhodium.Bitcoin.Features.RPC
{
    public interface IRPCAuthorization
    {
        List<IPAddress> AllowIp { get; }

        List<string> Authorized { get; }

        bool IsAuthorized(string user);

        bool IsAuthorized(IPAddress ip);

        bool IsWithoutAuthorization();
    }

    public class RPCAuthorization : IRPCAuthorization
    {
        public RPCAuthorization()
        {
            this.AllowIp = new List<IPAddress>();
            this.Authorized = new List<string>();
        }

        public List<string> Authorized { get; }

        public List<IPAddress> AllowIp { get; }

        public bool IsWithoutAuthorization()
        {
            if (this.Authorized.Count > 0)
            {
                foreach (var itemAuthorized in this.Authorized)
                {
                    if (!itemAuthorized.Contains("cookie")) return false;
                }
            }
            return true;
        }

        public bool IsAuthorized(string user)
        {
            Guard.NotEmpty(user, nameof(user));

            return this.Authorized.Any(a => a.Equals(user, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsAuthorized(IPAddress ip)
        {
            Guard.NotNull(ip, nameof(ip));

            return true;
        }
    }
}
