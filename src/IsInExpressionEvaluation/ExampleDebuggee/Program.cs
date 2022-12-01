using System.Diagnostics;
using IsInExpressionEvaluation.DebuggeeSide;

namespace ExampleDebuggee
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // This message should print "No"
            Console.WriteLine("Am I called from an expression evaluation? {0}", IsInExpressionEvaluation ? "Yes" : "No");

            // Stopping here, and inspecting 'IsInExpressionEvaluation' should return 'true'
            Debugger.Break();
        }

        static public bool IsInExpressionEvaluation => DebugHelper.IsInExpressionEvaluation();
    }
}