using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace RelaunchForeverTest
{
    internal class RelaunchForeverRunner : IDebugEventCallback2
    {
        const int DelayTime = 10000; // 10 seconds
        private static readonly Guid IID_IDebugSessionDestroyEvent2 = typeof(IDebugSessionDestroyEvent2).GUID;
        private static readonly Guid IID_IDebugStopCompleteEvent2 = typeof(IDebugStopCompleteEvent2).GUID;
        private static readonly Guid IID_IDebugEntryPointEvent2 = typeof(IDebugEntryPointEvent2).GUID;
        private static readonly Guid guidRelaunchForeverPane = new Guid("2a2df34b-1c98-4d8c-bc0d-168c5189599e");
        private static readonly Guid guidDebuggerLoggingPane = new Guid("CCC83D5E-9815-4829-9628-D7A7A83CA06F");

        private IVsDebugger _debuggerPackage;
        private IVsUIShell _shell;
        private IVsOutputWindow _outputWindowService;
        private IVsOutputWindowPane _output;
        private readonly HashSet<Guid> _ignoredNonStoppingEvents = new HashSet<Guid>()
        {
            typeof(IDebugThreadCreateEvent2).GUID,
            typeof(IDebugThreadDestroyEvent2).GUID,
            typeof(IDebugCustomEvent110).GUID
        };
        private int _lastOutputStringTime;
        private int _iteration;
        private bool _expectingSessionDestroy;
        private bool _isDelayRunning;
        private bool _isClosed;

        public RelaunchForeverRunner()
        {
        }

        internal void Start()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _debuggerPackage = (IVsDebugger)Package.GetGlobalService(typeof(IVsDebugger));
            if (_debuggerPackage == null)
            {
                throw new InvalidOperationException();
            }

            _shell = (IVsUIShell)Package.GetGlobalService(typeof(SVsUIShell));
            if (_shell == null)
            {
                throw new InvalidOperationException();
            }

            _outputWindowService = (IVsOutputWindow)Package.GetGlobalService(typeof(SVsOutputWindow));
            if (_outputWindowService == null)
            {
                throw new InvalidOperationException();
            }

            CheckHR(_outputWindowService.CreatePane(guidRelaunchForeverPane, "Relaunch Forever", fInitVisible: 1, fClearWithSolution: 0));

            CheckHR(_outputWindowService.GetPane(guidRelaunchForeverPane, out _output));

            CheckHR(_debuggerPackage.AdviseDebugEventCallback(this));
            StartDebugging();
        }

        private void StartDebugging()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_outputWindowService.GetPane(guidDebuggerLoggingPane, out IVsOutputWindowPane debuggerLoggingPane) == VSConstants.S_OK)
            {
                debuggerLoggingPane.Clear();
            }

            _iteration++;
            _output.Activate();
            _output.OutputStringThreadSafe(string.Format("Starting debugging (#{0}) at {1}.\n", _iteration, DateTime.Now.ToString("hh:mm:ss.FF")));
            CheckHR(_shell.PostExecCommand(VSConstants.CMDSETID.StandardCommandSet97_guid, (uint)VSConstants.VSStd97CmdID.Start, 0, null));
        }

        private void Close()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_isClosed)
            {
                return;
            }

            _isClosed = true;

            if (_debuggerPackage != null)
            {
                CheckHR(_debuggerPackage.UnadviseDebugEventCallback(this));
                _debuggerPackage = null;
            }
        }

        int IDebugEventCallback2.Event(IDebugEngine2 engine, IDebugProcess2 process, IDebugProgram2 program, IDebugThread2 thread, IDebugEvent2 @event, ref Guid iidEvent, uint dwAttrib)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (iidEvent == IID_IDebugSessionDestroyEvent2)
                {
                    OnSessionDestroy();
                }
                else if ((dwAttrib & (uint)enum_EVENTATTRIBUTES.EVENT_STOPPING) != 0)
                {
                    OnStoppingEvent(iidEvent);
                }
                else
                {
                    OnNonStoppingEvent(iidEvent);
                }

                return 0;
            }
            finally
            {
                Release(engine);
                Release(process);
                Release(program);
                Release(thread);
                Release(@event);
            }
        }

        private void OnStoppingEvent(in Guid iidEvent)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (iidEvent == IID_IDebugStopCompleteEvent2 || iidEvent == IID_IDebugEntryPointEvent2)
            {
                return;
            }

            _output.OutputStringThreadSafe(string.Format("Debugging stopped unexpectedly for event '{0}'. Stopping.\n", iidEvent));
            Close();
        }

        private void OnNonStoppingEvent(in Guid iidEvent)
        {
            if (_isClosed || _expectingSessionDestroy || _ignoredNonStoppingEvents.Contains(iidEvent))
            {
                return;
            }

            _lastOutputStringTime = Environment.TickCount;
            if (!_isDelayRunning)
            {
                _isDelayRunning = true;
                _ = WaitForDelayAsync();
            }
        }

        private async Task WaitForDelayAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                int delayTime = DelayTime;
                while (true)
                {
                    await Task.Delay(delayTime);

                    if (_isClosed)
                    {
                        return;
                    }

                    int elapsedTime = Environment.TickCount - _lastOutputStringTime;
                    if (elapsedTime >= delayTime)
                    {
                        break;
                    }

                    delayTime = DelayTime - elapsedTime;
                }

                if (IsVSAssertActive())
                {
                    _output.OutputStringThreadSafe("VSAssert detected. Stopping.\n");
                    Close();
                    return;
                }

                _expectingSessionDestroy = true;
                CheckHR(_shell.PostExecCommand(VSConstants.CMDSETID.StandardCommandSet97_guid, (uint)VSConstants.VSStd97CmdID.Stop, 0, null));
            }
            catch (Exception ex)
            {
                _ = _output.OutputStringThreadSafe(string.Format("{0} in WaitForDelayAsync. {1}\n", ex.GetType().ToString(), ex.Message));
                Close();
            }
            finally
            {
                _isDelayRunning = false;
            }
        }

        private void OnSessionDestroy()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!_expectingSessionDestroy)
            {
                // Session destroy happened when we didn't cause it. Stop running.
                _output.OutputStringThreadSafe("Debug session ended unexpectedly. Stopping.\n");
                Close();
                return;
            }

            _expectingSessionDestroy = false;
            StartDebugging();
        }

        private static bool IsVSAssertActive()
        {
            const string VSAssertTitle = "Visual Studio Assertion";
            char[] buffer = new char[VSAssertTitle.Length + 2];

            bool foundVSAssert = false;

            NativeMethods.EnumWindows((IntPtr hwnd, int lParam) =>
            {
                int lenText = NativeMethods.InternalGetWindowText(hwnd, buffer, buffer.Length);
                if (lenText != VSAssertTitle.Length)
                {
                    return true;
                }

                if (buffer[buffer.Length-2] == 0 && buffer.Take(buffer.Length-2).SequenceEqual(VSAssertTitle))
                {
                    foundVSAssert = true;
                    return false;
                }

                return true;
            }, IntPtr.Zero);

            return foundVSAssert;
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

        static class NativeMethods
        {
            public delegate bool WindowEnumProc(IntPtr hwnd, int lParam);

            [DllImport("user32.dll")]
            public static extern int EnumWindows([MarshalAs(UnmanagedType.FunctionPtr)] WindowEnumProc callPtr, IntPtr lPar);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern int InternalGetWindowText(IntPtr hWnd, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] char[] text, int maxCount);
        }
    }
}