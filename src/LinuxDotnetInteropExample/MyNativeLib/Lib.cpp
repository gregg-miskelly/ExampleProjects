#include <stdio.h>
#include <string>
#include <iostream>
#include <unistd.h> // for getpid()
#include <sys/wait.h> // for waitpid()
#include <filesystem>
#include <sys/prctl.h>

#define PUBLIC_EXPORT __attribute__((visibility("default")))

int g_magicNumber = 41; // Global variable to store the magic number

extern "C" int PUBLIC_EXPORT GetMagicNumber()
{
    g_magicNumber++; // Increment the magic number each time this function is called
    return g_magicNumber;
}

extern "C" int PUBLIC_EXPORT CallBackToManaged(int (* pCallback)(int value))
{
    int magicNumber = GetMagicNumber();
    int returnValue = pCallback(magicNumber);
    return returnValue;
}