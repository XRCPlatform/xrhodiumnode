using System.Buffers;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Formatters;
using Newtonsoft.Json;
using Xunit;

namespace BRhodium.Bitcoin.Features.RPC.Tests
{
    public class RPCJsonOutputFormatterTest
    {
        private TestRPCJsonOutputFormatter formatter;
        private JsonSerializerSettings settings;
        private ArrayPool<char> charpool;

        public RPCJsonOutputFormatterTest()
        {
            this.settings = new JsonSerializerSettings();
        }

        [Fact]
        public void CreateJsonSerializerCreatesSerializerWithProvidedSettings()
        {
            var settings = new JsonSerializerSettings
            {
                Culture = new System.Globalization.CultureInfo("en-GB")
            };
            var formatter = new TestRPCJsonOutputFormatter(settings);
            JsonSerializer serializer = formatter.JsonSerializer;

            Assert.Equal("en-GB", serializer.Culture.Name);
        }

        [Fact]
        public void WriteResponseBodyAsyncWritesContextToResponseBody()
        {
            Stream bodyStream = new MemoryStream();
            DefaultHttpContext defaultContext = SetupDefaultContextWithResponseBodyStream(bodyStream);

            Stream stream = null;
            var context = new OutputFormatterWriteContext(defaultContext,
                (s, e) =>
                {
                    if (stream == null)
                    {
                        // only capture first stream. bodyStream is already under the test's control.
                        stream = s;
                    }

                    return new StreamWriter(s, e, 256, true);
                }, typeof(RPCAuthorization),
                new RPCAuthorization());

            var task = this.formatter.WriteResponseBodyAsync(context, Encoding.UTF8);
            task.Wait();

            using (StreamReader reader = new StreamReader(stream))
            {
                stream.Position = 0;
                var result = reader.ReadToEnd();
                Assert.Equal("{\"Authorized\":[],\"AllowIp\":[]}", result);
            }

            using (StreamReader reader = new StreamReader(bodyStream))
            {
                bodyStream.Position = 0;
                var result = reader.ReadToEnd();
                Assert.Equal("{\"result\":{\"Authorized\":[],\"AllowIp\":[]},\"id\":1,\"error\":null}", result);
            }
        }

        private static DefaultHttpContext SetupDefaultContextWithResponseBodyStream(Stream bodyStream)
        {
            var defaultContext = new DefaultHttpContext();
            var response = new HttpResponseFeature();
            response.Body = bodyStream;
            var featureCollection = new FeatureCollection();
            featureCollection.Set<IHttpResponseFeature>(response);
            defaultContext.Initialize(featureCollection);
            return defaultContext;
        }

        private class TestRPCJsonOutputFormatter : RPCJsonOutputFormatter
        {
            public TestRPCJsonOutputFormatter(JsonSerializerSettings serializerSettings) : base(serializerSettings)
            {
            }

            public new JsonSerializer JsonSerializer => base.JsonSerializer;
        }
    }
}
