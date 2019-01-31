using System;
using System.Linq;
using DBreeze.Utils;
using NBitcoin;
using NBitcoin.BitcoinCore;
using ProtoBuf;

namespace BRhodium.Node.Utilities
{
    /// <summary>
    /// Implementation of serialization and deserialization of objects that go into the DBreeze database.
    /// </summary>
    public class DBreezeSerializer
    {
        public Network Network { get; private set; }

        /// <summary>
        /// Initializes custom serializers for DBreeze engine.
        /// </summary>
        public void Initialize(Network network)
        {
            this.Network = network;
            CustomSerializator.ByteArraySerializator = this.Serializer;
            CustomSerializator.ByteArrayDeSerializator = this.Deserializer;

        }

        /// <summary>
        /// Serializes object to a binary data format.
        /// </summary>
        /// <param name="obj">Object to be serialized.</param>
        /// <returns>Binary data representing the serialized object.</returns>
        public byte[] Serializer(object obj)
        {
            IBitcoinSerializable serializable = obj as IBitcoinSerializable;
            if (serializable != null)
                return serializable.ToBytes(network: this.Network);

            uint256 u256 = obj as uint256;
            if (u256 != null)
                return u256.ToBytes();

            uint160 u160 = obj as uint160;
            if (u160 != null)
                return u160.ToBytes();

            uint? u32 = obj as uint?;
            if (u32 != null)
                return u32.ToBytes();

            if (obj.GetType() == typeof(DateTimeOffset))
            {
                DateTimeOffset dateTimeOffset = (DateTimeOffset)obj;
                return dateTimeOffset.ToBytes();
            }

            if (obj.GetType() == typeof(DateTime))
            {
                DateTime dateTime = (DateTime)obj;
                return dateTime.ToBytes();
            }

            object[] arr = obj as object[];
            if (arr != null)
            {
                byte[][] serializedItems = new byte[arr.Length][];
                int itemIndex = 0;
                foreach (object arrayObject in arr)
                {
                    byte[] serializedObject = this.Serializer(arrayObject);
                    serializedItems[itemIndex] = serializedObject;
                    itemIndex++;
                }
                return ConcatArrays(serializedItems);
            }

            IProtoBufSerializeable proto = obj as IProtoBufSerializeable;
            return SerializeProtobuf(obj);

            throw new NotSupportedException();
        }

        /// <summary>
        /// Concatenates multiple byte arrays into a single byte array.
        /// </summary>
        /// <param name="arrays">Arrays to concatenate.</param>
        /// <returns>Concatenation of input arrays.</returns>
        /// <remarks>Based on https://stackoverflow.com/a/415396/3835864 .</remarks>
        private static byte[] ConcatArrays(byte[][] arrays)
        {
            byte[] res = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, res, offset, array.Length);
                offset += array.Length;
            }
            return res;
        }

        /// <summary>
        /// Deserializes binary data to an object of specific type.
        /// </summary>
        /// <param name="bytes">Binary data representing a serialized object.</param>
        /// <param name="type">Type of the serialized object.</param>
        /// <returns>Deserialized object.</returns>
        public object Deserializer(byte[] bytes, Type type)
        {
            if (type == typeof(Coins))
            {
                Coins coin = new Coins();
                coin.ReadWrite(bytes, network: this.Network);
                return coin;
            }

            if (type == typeof(BlockHeader))
            {
                BlockHeader header = this.Network.Consensus.ConsensusFactory.CreateBlockHeader();
                header.ReadWrite(bytes, network: this.Network);
                return header;
            }

            if (type == typeof(RewindData))
            {
                RewindData rewind = new RewindData();
                rewind.ReadWrite(bytes, network: this.Network);
                return rewind;
            }

            if (type == typeof(uint256))
            {
                return new uint256(bytes);
            }
                

            if (type == typeof(Block))
            {
                return Block.Load(bytes, this.Network);
            }
                
            var o = Activator.CreateInstance(type) as IBitcoinSerializable;
            if (o != null)
            {
                o.ReadWrite(bytes);
                return o;
            }
            var proto = Activator.CreateInstance(type) as IProtoBufSerializeable;
            if (proto != null)
            {
                return DeserializeProtobuf(bytes,type);
            }

            throw new NotSupportedException();
        }

        /// <summary>
        /// Deserializes protobuf object from byte[]
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public T DeserializeProtobuf<T>(byte[] data)
        {
            T ret = default(T);

            using (System.IO.MemoryStream ms = new System.IO.MemoryStream(data))
            {

                ret = ProtoBuf.Serializer.Deserialize<T>(ms);
                ms.Close();
            }

            return ret;
        }

        /// <summary>
        /// Deserializes protobuf object from byte[]. Non-generic style.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="T"></param>
        /// <returns></returns>
        public static object DeserializeProtobuf(byte[] data, Type T)
        {
            object ret = null;
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream(data))
            {

                ret = ProtoBuf.Serializer.NonGeneric.Deserialize(T, ms);
                ms.Close();
            }

            return ret;
        }

        /// <summary>
        /// Serialize object using protobuf serializer
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public byte[] SerializeProtobuf(object data)
        {
            byte[] bt = null;
            using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
            {
                ProtoBuf.Serializer.NonGeneric.Serialize(ms, data);
                bt = ms.ToArray();
                ms.Close();
            }

            return bt;
        }
    }
}