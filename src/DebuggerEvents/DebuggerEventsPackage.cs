using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace DebuggerEventsExample
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = false)]
    [Guid(DebuggerEventsPackage.PackageGuidString)]
    // Automatically load the package when debugging starts
#pragma warning disable VSSDK004 // Use BackgroundLoad flag in ProvideAutoLoad attribute for asynchronous auto load -- async load will not work for this package
    [ProvideAutoLoad(VSConstants.UICONTEXT.Debugging_string)]
#pragma warning restore VSSDK004 // Use BackgroundLoad flag in ProvideAutoLoad attribute for asynchronous auto load.
    public sealed class DebuggerEventsPackage : Package, IDebugEventCallback2
    {
        private IVsDebugger _debuggerPackage;
        private readonly Guid IID_IDebugProcessCreateEvent2 = typeof(IDebugProcessCreateEvent2).GUID;
        private readonly Guid IID_IDebugProcessDestroyEvent2 = typeof(IDebugProcessDestroyEvent2).GUID;

        /// <summary>
        /// VSIXPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "f09ab26a-1c14-4ad8-8615-bf57fde1fbb7";

        protected override void Initialize()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            base.Initialize();

            _debuggerPackage = (IVsDebugger)Package.GetGlobalService(typeof(IVsDebugger));
            if (_debuggerPackage == null)
            {
                throw new InvalidOperationException();
            }

            CheckHR(_debuggerPackage.AdviseDebugEventCallback(this));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
#pragma warning disable VSTHRD108 // Assert thread affinity unconditionally
                ThreadHelper.ThrowIfNotOnUIThread();
#pragma warning restore VSTHRD108 // Assert thread affinity unconditionally

                if (_debuggerPackage != null)
                {
                    _debuggerPackage.UnadviseDebugEventCallback(this);
                }
            }
        }

        private void OnProcessCreate(IDebugProcess2 process)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!TryGetProcessId(process, out object processId))
            {
                Debug.Fail("How did a process create event not have a process id?");
                return;
            }

            Debug.WriteLine("Process Create Event for process {0}", processId);
        }

        private void OnProcessDestroy(IDebugProcess2 process)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!TryGetProcessId(process, out object processId))
            {
                Debug.Fail("How did a process destroy event not have a process id?");
                return;
            }

            Debug.WriteLine("Process Destroy Event for process {0}", processId);
        }

        public int Event(IDebugEngine2 engine, IDebugProcess2 process, IDebugProgram2 program, IDebugThread2 thread, IDebugEvent2 @event, ref Guid iidEvent, uint eventAttribs)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (iidEvent == IID_IDebugProcessCreateEvent2)
                {
                    OnProcessCreate(process);
                }
                else if (iidEvent == IID_IDebugProcessDestroyEvent2)
                {
                    OnProcessDestroy(process);
                }
            }
            finally
            {
                Release(engine);
                Release(process);
                Release(program);
                Release(thread);
                Release(@event);
            }

            return 0;
        }

        private static bool TryGetProcessId(IDebugProcess2 process, out object processId)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            processId = 0;

            if (object.ReferenceEquals(process, null))
            {
                return false;
            }

            AD_PROCESS_ID[] processIdArray = new AD_PROCESS_ID[1];
            if (process.GetPhysicalProcessId(processIdArray) != 0)
            {
                return false;
            }

            enum_AD_PROCESS_ID processIdType = (enum_AD_PROCESS_ID)processIdArray[0].ProcessIdType;
            switch (processIdType)
            {
                case enum_AD_PROCESS_ID.AD_PROCESS_ID_SYSTEM:
                    processId = processIdArray[0].dwProcessId;
                    return true;

                case enum_AD_PROCESS_ID.AD_PROCESS_ID_GUID:
                    processId = processIdArray[0].guidProcessId;
                    return true;

                default:
                    Debug.Fail("Unknown process id type???");
                    return false;
            }
        }

        private static void Release(object maybeComObject)
        {
            if (!object.ReferenceEquals(maybeComObject, null) && Marshal.IsComObject(maybeComObject))
            {
                Marshal.ReleaseComObject(maybeComObject);
            }
        }

        private static void CheckHR(int hr)
        {
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }
    }
}
