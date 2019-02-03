using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using ProtoBuf;

namespace BRhodium.Bitcoin.Features.Wallet
{
    /// <summary>
    /// Register these serializers in walletFeature initialize or other application startup event.
    /// </summary>
    public class SurrogateProtoBufSerializers
    {
        [ProtoContract]
        public class ScriptSurogate
        {
            [ProtoMember(1)] public byte[] bytes = new byte[0];
            public static implicit operator Script(ScriptSurogate value)
            {
                return new Script(value?.bytes);
            }

            public static implicit operator ScriptSurogate(Script value)
            {
                if (value == null) return null;
                return new ScriptSurogate()
                {
                    bytes = value.ToBytes()
                };
            }
        }

        [ProtoContract]
        public class PartialMerkleTreeSurogate
        {
            [ProtoMember(1)] public byte[] bytes = new byte[0];
            public static implicit operator PartialMerkleTree(PartialMerkleTreeSurogate value)
            {
                PartialMerkleTree t = new PartialMerkleTree();
                t.FromBytes(value.bytes);
                return t;
            }
            public static implicit operator PartialMerkleTreeSurogate(PartialMerkleTree value)
            {
                if (value == null) return null;
                return new PartialMerkleTreeSurogate()
                {
                    bytes = value.ToBytes()
                };
            }
        }

        [ProtoContract]
        public class Uint256Surogate
        {
            [ProtoMember(1)] public byte[] bytes = new byte[0];
            public static implicit operator uint256(Uint256Surogate value)
            {
                return new uint256(value.bytes);
            }
            public static implicit operator Uint256Surogate(uint256 value)
            {
                if (value == null) return null;
                return new Uint256Surogate()
                {
                    bytes = value.ToBytes()
                };
            }
        }
        [ProtoContract]
        public class NetworkSurogate
        {
            [ProtoMember(1)]
            public string NetworkName { get; set; }
            public override string ToString()
            {
                return this.NetworkName;
            }

            public static explicit operator Network(NetworkSurogate value)
            {
                return Network.GetNetwork(value.NetworkName);
            }
            public static implicit operator NetworkSurogate(Network value)
            {
                return new NetworkSurogate()
                {
                    NetworkName = value?.Name
                };
            }
        }
        [ProtoContract]
        public class MoneySurogate
        {
            [ProtoMember(1)] public long Satoshis;
            public static implicit operator MoneySurogate(Money value)
            {
                if (value == null) return null;
               
                return new MoneySurogate()
                {
                    Satoshis = value.Satoshi
                };
            }
            public static implicit operator Money(MoneySurogate value)
            {
                var result = new Money(value.Satoshis);
                return result;
            }
        }

        [ProtoContract]
        public class DateTimeOffsetSurogate
        {
            [ProtoMember(1)] public DateTime UtcTime;
            [ProtoMember(2)] public TimeSpan Offset;

            public static implicit operator DateTimeOffsetSurogate(DateTimeOffset value)
            {
                return new DateTimeOffsetSurogate()
                {
                    UtcTime = value.UtcDateTime,
                    Offset = value.Offset
                };
            }

            public static implicit operator DateTimeOffset(DateTimeOffsetSurogate value)
            {
                var result = new DateTimeOffset(value.UtcTime);
                return result.ToOffset(value.Offset);
            }
        }
    }
}
