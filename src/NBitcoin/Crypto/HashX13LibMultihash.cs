using System;
using NBitcoin.Native;
namespace NBitcoin.Crypto
{
    // this hashing class is not thread safe to use with static instances.
    // the hashing objects maintain state during hash calculation.
    // to use in a multi threaded environment create a new instance for every hash.

    public unsafe class HashX13LibMultihash
    {
        private void Digest(byte[] data, byte[] result, params object[] extra)
        {
            if(data.Length != 80){
                throw new ArgumentException($"Input data must be exactly 80 bytes long");
            }
            if (result.Length < 32) {
                throw new ArgumentException($"Result must be greater or equal 32 bytes");
            }
            fixed (byte* input = data)
            {
                fixed (byte* output = result)
                {
                    LibMultihash.x13(input, output, (uint)data.Length);
                }
            }
        }

        public uint256 Hash(byte[] input)
        {
            byte[] output = new byte[32];
            Digest(input, output, null);
            return new uint256(output);
        }
    }
}
