using CsMultiStartShared;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;

namespace MultiStartClient
{
    internal class Client
    {
        static async Task Main(string[] args)
        {
            var pipeClient = new NamedPipeClientStream(PipeHelper.Name);
            await pipeClient.ConnectAsync();

            var requestBuffer = new byte[256];
            MemoryStream requestStream = new MemoryStream(requestBuffer);
            var requestWriter = new BinaryWriter(requestStream, encoding: new UnicodeEncoding());

            Memory<byte> replyBuffer = new Memory<byte>(new byte[256]);

            for (int i = 0; i < 100; i++)
            {
                PipeHelper.WriteMessage(requestWriter, $"Message #{i + 1}");
                ReadOnlyMemory<byte> requestMemory = new ReadOnlyMemory<byte>(requestBuffer, 0, (int)requestStream.Position);
                await pipeClient.WriteAsync(requestMemory);

                string reply = await PipeHelper.ReadMessageAsync(replyBuffer, pipeClient);
                Console.WriteLine("Server returned: {0}", reply);
            }
        }
    }
}
