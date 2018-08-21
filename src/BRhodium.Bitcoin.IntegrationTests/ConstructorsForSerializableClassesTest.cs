using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NBitcoin;
using Xunit;

namespace BRhodium.Node.IntegrationTests
{
    public class ConstructorsForSerializableClassesTest
    {
        /// <summary>
        /// Serializes input and returns deserialized object.
        /// </summary>
        /// <remarks>Needed for troubleshooting this test.</remarks>
        private T CloneViaSerializeDeserialize<T>(T input) where T : IBitcoinSerializable
        {
            MemoryStream ms = new MemoryStream();
            BitcoinStream bitcoinStream = new BitcoinStream(ms, true);

            input.ReadWrite(bitcoinStream);
            ms.Position = 0;

            bitcoinStream = new BitcoinStream(bitcoinStream.Inner, false);
            var obj = Activator.CreateInstance<T>();
            obj.ReadWrite(bitcoinStream);
            return obj;
        }
    }
}
