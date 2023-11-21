using System;
using System.Runtime.InteropServices;

namespace CsMultiStartShared
{
	static internal class PipeHelper
    {
        public const string Name = "MultiStartTestPipe";

        public static async Task<string> ReadMessageAsync(Memory<byte> memory, Stream pipe)
        {
            const int sizeOfSize = 4;
            Memory<byte> sizeSlice = memory.Slice(0, sizeOfSize);
            await pipe.ReadAtLeastAsync(sizeSlice, sizeOfSize);
            int length = BitConverter.ToInt32(sizeSlice.Span);

            int contentByteCount = length * 2;
            Memory<byte> contentSlice = memory.Slice(0, contentByteCount);
            await pipe.ReadAtLeastAsync(contentSlice, contentByteCount);

            return CreateStringFromMemory(contentSlice);
        }

        private static string CreateStringFromMemory(Memory<byte> contentSlice)
        {
            var messageMemory = MemoryMarshal.Cast<byte, char>(contentSlice.Span);
            return new string(messageMemory);
        }

        public static void WriteMessage(BinaryWriter writer, string message)
        {
            writer.Seek(0, SeekOrigin.Begin);
            writer.Write(message.Length);
            foreach (char c in message)
            {
                writer.Write(c);
            }
            writer.Flush();
        }
    }
}
