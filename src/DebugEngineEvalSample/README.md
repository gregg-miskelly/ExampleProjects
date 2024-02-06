## Summary

This is a sample to demonstrate how to use DTE + the Microsoft.VisualStudio.Debugger.Engine
API to evaluate expressions. The basic technique can also be used for other operations, like
reading memory.

All of the important code in this sample can be found in `_EvaluationHelper.cs`.

## How to exercise this sample

1. Load the solution
2. Launch the sample project, which should start an experimental instance of Visual Studio
3. In the experimental instance, open a 'Hello World' project
4. Add a variable with the name `myTestExpression`
5. Set a breakpoint at the line after where this expression is defined
6. Start debugging
7. Open the tools menu, and run 'Invoke TriggerAsyncEval' or 'Invoke TriggerSyncEval'
8. A message box should be displayed with the value
