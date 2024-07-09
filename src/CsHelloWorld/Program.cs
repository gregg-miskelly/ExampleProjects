using System.Runtime.InteropServices;

Console.WriteLine("Welcome to C# Hello world, running on {0} ({1}).", RuntimeInformation.RuntimeIdentifier, RuntimeInformation.ProcessArchitecture);

var numbers = new List<int>();
while (true)
{
    Console.WriteLine("Enter a number or 'q' to quit");
    var input = Console.ReadLine();
    if (input == "q")
    {
        break;
    }

    if (int.TryParse(input, out var number))
    {
        numbers.Add(number);
    }
    else
    {
        Console.WriteLine("Invalid number");
    }
}

Console.WriteLine("Numbers entered: {0}", string.Join(", ", numbers));