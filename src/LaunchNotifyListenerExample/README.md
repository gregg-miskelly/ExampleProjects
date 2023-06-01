## About this folder

This folder shows an example of consuming IVsDebugLaunchNotifyListenerFactory110/IVsDebugLaunchNotifyListener110.

The most interesting code is in ExampleLaunchNotifyVSExtension\LaunchExampleDebuggee.cs.

Here is the definition of the from msdbg110.idl:

```
interface IVsDebugLaunchNotifyListenerFactory110: IUnknown
{
    // Instructs the debugger to connect to the specified computer, and begin
    // listening for notification that new target process have started. Target
    // processes should be started suspended, and the debugger can be notified
    // via the command line returned from 
    // IVsDebugLaunchNotifyListener110.GetNotifyCommandLine.
    //
    // guidPortSupplier may for GUID_NULL or guidLocalPortSupplier for local debugging
    // or remote debugging over Windows Authentication. It may be guidNativePortSupplier 
    // for remote debugging without authentication (including non-native scenarios). Other
    // port suppliers are not currently supported
    //
    // E_REMOTE_CONNECT_USER_CANCELED can be returned if the user cancels the remote
    // connection. VSDEBUG_ERROR_CANNOT_LAUNCH_DEBUG_TARGETS is returned for all other
    // errors, and error information is available through COM.
    HRESULT StartListener(
        [in] LPCWSTR transportQualifier,
        [in] REFGUID guidPortSupplier,
        [in] struct VsDebugLaunchNotifyListenerProperties* pProperties,
        [out] IVsDebugLaunchNotifyListener110** ppListener
        );
};

interface IVsDebugLaunchNotifyListener110: IUnknown
{
    // Close the listener so that no further requests are sent. The listener is 
    // automatically closed when the debug session is destroyed.
    HRESULT Close();

    // Obtains the information required to start the target process, and have the debugger 
    // immediately attach.
    // 
    // pNotifyCommandLine: Returns a command line to execute on the target computer. The
    // command line will start with a quoted path to where VsDebugLaunchNotify.exe can be
    // found on the target computer and will include any arguments. The host environment 
    // is responsible for starting VsDebugLaunchNotify.exe on the target computer with 
    // this command line, and with the process id and thread id appended as a suffix such
    // that the full command line is:
    //   %returnValueFromThisMethod% -p %processId% -t %threadId%
    //
    // pTargetProcessEnvironment: environment variables to pass to the target process as 
    // it starts. This value is depends on the engines being used to debug the target 
    // process. If no environment variables are required, NULL is returned. When 
    // environment variables are required, the format is a double-null terminated string.
    // See the documentation for CreateProcess Win32 API for more information.
    HRESULT GetTargetStartInfo(
        [out] BSTR* pNotifyCommandLine,
        [out] BSTR* pTargetProcessEnvironment);

    // Gets the IDebugCoreServer2 which represents the connection to the target computer.
    // For remote debugging scenarios, this can be used to communicate with the host
    // environment on the target computer through DkmCustomMessage.
    HRESULT GetServer([out] IDebugCoreServer2** ppServer);
};
```

## Using this sample

1. Open the solution
2. Set 'ExampleLaunchNotifyVSExtension' as the startup project
3. Open 'ExampleProjects\src\LaunchNotifyListenerExample\ExampleLaunchNotifyVSExtension\LaunchExampleDebuggee.cs', and update `pathToExampleDebuggee`
4. Start debugging, which should launch an experimental instance of VS
5. In the experimental instance: open 'ExampleProjects\src\LaunchNotifyListenerExample\ExampleDebuggee\Program.cs' and set a breakpoint
6. In the experimental instance: Tools->Invoke LaunchExampleDebuggee
