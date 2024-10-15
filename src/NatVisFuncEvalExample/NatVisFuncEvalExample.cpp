// NatVisFuncEvalExample.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <iostream>

class ExampleClass
{
private:
    std::string m_str1 = "ExampleClass::m_str1";
    std::string m_str2 = "ExampleClass::m_str2";

public:
    ExampleClass() {}

    std::string StrFunc1()
    {
        return "StrFunc1: " + m_str1 + " " + m_str2;
    }

    std::string StrFunc2()
    {
        return "StrFunc2: " + m_str1 + " " + m_str2;
    }
};

int main()
{
    ExampleClass ec;

    __debugbreak();

    // Reference the functions, so they get included in the pdb.
    ec.StrFunc1();
    ec.StrFunc2();
}
