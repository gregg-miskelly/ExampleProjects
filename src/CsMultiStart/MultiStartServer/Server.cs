using CsMultiStartShared;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;

namespace MultiStartServer
{
    internal class Server
    {
        static async Task Main(string[] args)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            // Uncomment this generate many thread create/exit events during the test
            // SpinUpAndDownThreads(cancellationTokenSource.Token);

            Memory<byte> requestBuffer = new Memory<byte>(new byte[256]);
            var replyBuffer = new byte[256];
            MemoryStream replyStream = new MemoryStream(replyBuffer);
            var replyWriter = new BinaryWriter(replyStream, encoding: new UnicodeEncoding());

            using (var pipe = new NamedPipeServerStream(PipeHelper.Name, PipeDirection.InOut, maxNumberOfServerInstances: 1, PipeTransmissionMode.Byte, PipeOptions.FirstPipeInstance | PipeOptions.CurrentUserOnly))
            {
                await pipe.WaitForConnectionAsync();

                while (true)
                {
                    string requestMessage;
                    try
                    {
                        requestMessage = await PipeHelper.ReadMessageAsync(requestBuffer, pipe);
                    }
                    catch (EndOfStreamException)
                    {
                        cancellationTokenSource.Cancel();
                        return;
                    }

                    Console.WriteLine(requestMessage);

                    PipeHelper.WriteMessage(replyWriter, "Me too! (from server)");

                    ReadOnlyMemory<byte> replyMemory = new ReadOnlyMemory<byte>(replyBuffer, 0, (int)replyStream.Position);
                    await pipe.WriteAsync(replyMemory);
                }
            }
        }

        private static void SpinUpAndDownThreads(CancellationToken cancellationToken)
        {
            void SpanNewThread(Thread? parent, int number, ulong iteration)
            {
                Thread t = new Thread(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (parent is not null)
                    {
                        parent.Join();
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    SpanNewThread(Thread.CurrentThread, number, iteration + 1);
                });

                t.Name = $"Server worker thread #{number} v{iteration}";
                t.Start();
            }

            for (int x = 0; x < 5; x++)
            {
                SpanNewThread(null, x + 1, 0);
            }
        }
    }
}
