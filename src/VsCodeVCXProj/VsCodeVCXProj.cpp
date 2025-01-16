// Example .vcxproj that can build built in both VS and VS Code

#include <stdio.h>
#include <Windows.h>

int main()
{
    DWORD processId = GetCurrentProcessId();
    printf("Hello world from process %u\n", processId);
    return 0;
}
 