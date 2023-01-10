#include <iostream>
#include <stdio.h>

int main(int argc, char* argv[])
{
    for (int c = 0; c < argc; c++)
    {
        printf("argv[%d] = '%s'\n", c, argv[c]);
    }
    printf("argv[%d] = %p\n", argc, argv[argc]);

    std::cout << "Hello World" << std::endl;
}