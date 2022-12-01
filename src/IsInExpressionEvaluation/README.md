# IsInExpressionEvaluation

This solution provides an example of using a combination of notifications from vsdebugeng.dll (the Concord debug engine) and an in-process component to
detect when code is running due to an expression evaluations (ex: Data tips, Watch window, etc) instead of the normal usage of the app.

To use:
1. Ensure that 'EvaluationMonitor' is the startup project
2. Hit F5, this should launch an experimental instance of VS, with a sample debuggee project open
3. Run the sample debuggee project, which should stop at a 'Debug.Break()'
4. See that the console will print "No", and evaluating 'IsInExpressionEvaluation' from the watch/datatips/etc will print 'true'