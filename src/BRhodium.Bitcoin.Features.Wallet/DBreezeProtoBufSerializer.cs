using System;
using BRhodium.Node.Utilities;
using NBitcoin;
using ProtoBuf;
using DBreeze;
using DBreeze.Utils;

namespace BRhodium.Bitcoin.Features.Wallet
{
    public class DBreezeProtoBufSerializer
    {
        internal void Initialize(Network network)
        {
            CustomSerializator.ByteArraySerializator = ProtobufSerializer.SerializeProtobuf;
            CustomSerializator.ByteArrayDeSerializator = ProtobufSerializer.DeserializeProtobuf;
        }
    }
}