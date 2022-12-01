using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Security.Principal;
using System.Security.AccessControl;
using Shared;
using System.Diagnostics;
using Microsoft.VisualStudio.Debugger;

namespace EvaluationMonitor
{
    internal class SharedMemory : DkmDataItem
    {
        private static readonly object s_creationLock = new object();
        private readonly MemoryMappedFile _mappedFile;
        private readonly MemoryMappedViewAccessor _viewAccessor;

        private SharedMemory(int processId)
        {
            // NOTE: This implementation doesn't handle --
            // 1. Processes that in a different Windows session. To do so, one needs complicated code to sometimes make the share memory name in the 'Session\' prefix, but not always
            // -or-
            // 2. App Container processes -- need more work in setting up the security for those
            MemoryMappedFileSecurity security = new MemoryMappedFileSecurity();
            security.AddAccessRule(new AccessRule<MemoryMappedFileRights>(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MemoryMappedFileRights.Read, AccessControlType.Allow));
            _mappedFile = MemoryMappedFile.CreateOrOpen(SharedMemoryConstants.SharedMemoryPrefix + processId.ToString(CultureInfo.InvariantCulture),
                SharedMemoryConstants.MaxSize, MemoryMappedFileAccess.ReadWrite, MemoryMappedFileOptions.None, security, System.IO.HandleInheritability.None);
            _viewAccessor = _mappedFile.CreateViewAccessor(0, SharedMemoryConstants.MaxSize);
        }

        public static bool TryGetInstance(DkmProcess process, out SharedMemory sharedMemory)
        {
            sharedMemory = null;
            if (process.LivePart == null)
                return false;

            sharedMemory = process.GetDataItem<SharedMemory>();
            if (sharedMemory != null)
            {
                return true;
            }

            lock (s_creationLock)
            {
                // Check again now that we have the lock
                sharedMemory = process.GetDataItem<SharedMemory>();
                if (sharedMemory != null)
                {
                    return true;
                }

                sharedMemory = new SharedMemory(process.LivePart.Id);
                process.SetDataItem(DkmDataCreationDisposition.CreateNew, sharedMemory);

                return true;
            }
        }

        internal void SetIsInEvaluation(bool newValue)
        {
            _viewAccessor.Write(position: 0, value: newValue);
        }

        protected override void OnClose()
        {
            _viewAccessor.Dispose();
            _mappedFile.Dispose();
        }
    }
}
