#include <atlbase.h>
#include <stdio.h>
#include <atlstr.h>

inline _Ret_range_(0x8000000, 0xffffffff) HRESULT WIN32_ERROR(LONG lError)
{
    HRESULT hr = HRESULT_FROM_WIN32(lError);
    if (SUCCEEDED(hr))
        hr = E_FAIL;
    return hr;
}

inline _Ret_range_(0x8000000, 0xffffffff) HRESULT WIN32_LAST_ERROR()
{
    return WIN32_ERROR(GetLastError());
}

inline HRESULT WIN32_BOOL_CALL(BOOL rc)
{
    if (!rc)
        return WIN32_LAST_ERROR();
    return S_OK;
}

int __cdecl wmain(int argc, _In_ _TCHAR* argv[])
{
    HRESULT hr;

    if (argc == 2 && wcscmp(argv[1], L"/?") == 0 || wcscmp(argv[1], L"-?") == 0)
    {
        printf(
            "FakeOrchestrator: An example orchestrator (process which job is to execute\n"
            "other processes at the correct time) that supports debugging.\n"
            "\n"
            "Command line: Command line to execute.\n"
            "\n"
            "Environment variable %%VSDebugNotifyCmd%% is set to the\n"
            "VsDebugLaunchNotify command line.\n");

        return 0;
    }

    if (argc == 1)
    {
        printf("The syntax of the command is incorrect.\n");
        return -1;
    }

    LPCWSTR szCmdLine = GetCommandLine();

    if (szCmdLine[0] == '\"')
    {
        // If the command line begins with a quote, skip to the end
        szCmdLine = wcschr(&szCmdLine[1], '\"');

        // skip past the quote
        if (szCmdLine && szCmdLine[0])
            szCmdLine++;
    }
    else
    {
        WCHAR szFullPath[MAX_PATH];
        const DWORD nChars = GetModuleFileName(NULL, szFullPath, ARRAYSIZE(szFullPath));

        if (nChars && CompareStringOrdinal(szFullPath, nChars, szCmdLine, nChars, /*bIgnoreCase:*/ TRUE) == CSTR_EQUAL)
        {
            // The command line started with the full path to this exe
            szCmdLine += nChars;
        }
        else
        {
            // just find the first space, and assume the arguments start after that point.
            // This will not always work out, but when it doesn't, it will simply result
            // in an invalid argument
            szCmdLine = wcschr(szCmdLine, ' ');
        }
    }

    if (szCmdLine == NULL)
    {
        printf("The syntax of the command is incorrect.\n");
        return -1;
    }

    while (szCmdLine[0] == ' ')
        szCmdLine++;

    DWORD flags = 0;

    CString debuggerCommandLine;
    {
        WCHAR szVsDebugNotifyCmd[_MAX_PATH];
        if (GetEnvironmentVariable(L"VSDebugNotifyCmd", szVsDebugNotifyCmd, _countof(szVsDebugNotifyCmd)))
        {
            debuggerCommandLine = szVsDebugNotifyCmd;
            if (debuggerCommandLine.GetLength() > 0)
            {
                flags |= CREATE_SUSPENDED;
            }
        }
    }

    CString appCommandLine = szCmdLine;

    STARTUPINFO si = { 0 };
    si.dwFlags = STARTF_USESHOWWINDOW;
    si.wShowWindow = SW_SHOW;

#ifdef _X86_
    typedef BOOL(WINAPI* WOW64DISABLEWOW64FSREDIRECTION)(_Out_ PVOID* OldValue);
    typedef BOOL(WINAPI* WOW64REVERTWOW64FSREDIRECTION)(_In_ PVOID OldValue);

    const HMODULE hKernel32 = GetModuleHandle(TEXT("kernel32.dll"));

    const WOW64DISABLEWOW64FSREDIRECTION pWow64DisableWow64FsRedirection = (WOW64DISABLEWOW64FSREDIRECTION)
        GetProcAddress(hKernel32, "Wow64DisableWow64FsRedirection");

    const WOW64REVERTWOW64FSREDIRECTION pWow64RevertWow64FsRedirection = (WOW64REVERTWOW64FSREDIRECTION)
        GetProcAddress(hKernel32, "Wow64RevertWow64FsRedirection");

    PVOID OldValue = NULL;
    BOOL fRestoreRedirection = FALSE;
    if (pWow64DisableWow64FsRedirection != NULL && pWow64RevertWow64FsRedirection != NULL)
    {
        // When we are running under WOW, we need to disable folder redirection so that
        // we can look at applications from the native system directory
        fRestoreRedirection = pWow64DisableWow64FsRedirection(&OldValue);
    }
#endif

    PROCESS_INFORMATION pi;

    hr = WIN32_BOOL_CALL(CreateProcess(
        argv[1],
        appCommandLine.GetBuffer(),
        NULL, // process attributes
        NULL, // thread attributes
        false, // bInheritHandles
        flags, // flags
        NULL, // environment
        NULL, // current directory
        &si,
        &pi
    ));

#ifdef _X86_
    if (fRestoreRedirection)
    {
        pWow64RevertWow64FsRedirection(OldValue);
    }
#endif

    appCommandLine.ReleaseBuffer();

    if (hr != S_OK)
    {
        wprintf(L"Failed to launch '%s'. Error = 0x%08x.\n", argv[1], hr);
        return hr;
    }

    CHandle hProcessHolder; hProcessHolder.Attach(pi.hProcess);
    CHandle hThreadHolder; hThreadHolder.Attach(pi.hThread);

    if (debuggerCommandLine.GetLength() > 0)
    {
        debuggerCommandLine.AppendFormat(L" -p %d -t %d", pi.dwProcessId, pi.dwThreadId);

        PROCESS_INFORMATION debuggerProcessInfo;

        hr = WIN32_BOOL_CALL(CreateProcess(
            NULL,
            debuggerCommandLine.GetBuffer(),
            NULL, // process attributes
            NULL, // thread attributes
            false, // bInheritHandles
            0, // flags
            NULL, // environment
            NULL, // current directory
            &si,
            &debuggerProcessInfo
        ));

        debuggerCommandLine.ReleaseBuffer();

        if (FAILED(hr))
        {
            wprintf(L"ERROR: Unable to spawn VSDebugNotifyCmd (%s). Error code 0x%08x.\n", static_cast<LPCWSTR>(debuggerCommandLine), hr);

            TerminateProcess(pi.hProcess, MAXDWORD);

            return hr;
        }

        CloseHandle(debuggerProcessInfo.hProcess);
        CloseHandle(debuggerProcessInfo.hThread);
    }

    WaitForSingleObject(hProcessHolder, INFINITE);

    return hr;
}
