using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using Shared;

namespace IsInExpressionEvaluation.DebuggeeSide
{
    public static class DebugHelper
    {
        static MemoryMappedViewAccessor _sharedMemory;

        public static bool IsInExpressionEvaluation()
        {
            if (!Debugger.IsAttached)
            {
                // No debugger is attached, so we cannot be in an expression evaluation.
                return false;
            }

            if (_sharedMemory == null)
            {
                try
                {
                    EnsureHaveSharedMemory();
                }
                catch
                {
                    // If anything goes wrong, we don't want to crash the target app
                    return false;
                }
            }

            return _sharedMemory.ReadBoolean(0);
        }

        private static void EnsureHaveSharedMemory()
        {
            MemoryMappedFile mappedFile = GetMappedFile();
            MemoryMappedViewAccessor sharedMemory = mappedFile.CreateViewAccessor(0, SharedMemoryConstants.MaxSize, MemoryMappedFileAccess.Read);

            if (Interlocked.CompareExchange(ref _sharedMemory, sharedMemory, null) != null)
            {
                // Another thread initialized first
                mappedFile.Dispose();
                sharedMemory.Dispose();
            }
        }

        private static MemoryMappedFile GetMappedFile()
        {
            int processId = System.Diagnostics.Process.GetCurrentProcess().Id;
            string mappingName = SharedMemoryConstants.SharedMemoryPrefix + processId.ToString(CultureInfo.InvariantCulture);

            MemoryMappedFile mappedFile;

            // NOTE: Ideally we would use MemoryMappedFile.CreateOrOpen, but that doesn't work as we need different access if we are creating
            // a new mapping vs. opening an existing one. Otherwise, when the debugger tries to create a read/write mapping, it will fail.

            try
            {
                mappedFile = MemoryMappedFile.OpenExisting(mappingName, MemoryMappedFileRights.Read, System.IO.HandleInheritability.None);
                return mappedFile;
            }
            catch (FileNotFoundException)
            {
            }

            // Try to create a new one
            try
            {
                mappedFile = MemoryMappedFile.CreateNew(mappingName, SharedMemoryConstants.MaxSize, MemoryMappedFileAccess.ReadWrite);
                return mappedFile;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            // Lastly, try again to open the existing one
            return MemoryMappedFile.OpenExisting(mappingName, MemoryMappedFileRights.Read, System.IO.HandleInheritability.None);
        }
    }
}
