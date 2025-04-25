using System.Runtime.InteropServices;

internal class Program
{
    private static void Main(string[] args)
    {
        string? dotnetRuntimePath = Path.GetDirectoryName(typeof(string).Assembly.Location);
        if (string.IsNullOrEmpty(dotnetRuntimePath))
        {
            Console.Error.WriteLine("Failed to get the .NET runtime path.");
            return;
        }

        Console.WriteLine("Testing test with .NET Runtime from: {0}", dotnetRuntimePath);
        Console.WriteLine("Process ID: {0}", Environment.ProcessId);

        int magicNumber = NativeMethods.GetMagicNumber();
        Console.WriteLine($"Magic number from native library: {magicNumber}");

        //Enable to test crash handling
        //string s = null!;

        ManagedCallback managedDelegate = (int x) =>
        {
            Console.WriteLine($"Managed delegate called with {x}");
            //Console.WriteLine("Length of string: {0}", s.Length);
            return x * 2;
        };

        int resturn = NativeMethods.CallBackToManaged(managedDelegate);
        Console.WriteLine($"Return value from native library: {resturn}");
    }
}

delegate int ManagedCallback(int x);

internal class NativeMethods
{
    [DllImport("MyNativeLib")]
    public static extern int GetMagicNumber();

    [DllImport("MyNativeLib", CharSet = CharSet.Ansi)]
    public static extern int CallBackToManaged([MarshalAs(UnmanagedType.FunctionPtr)] ManagedCallback managedCallback);
}