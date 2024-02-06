using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.DefaultPort;
using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger.Symbols;
using Microsoft.VisualStudio.Shell;

#nullable enable

namespace DebugEngineEvalSample
{
    /// <summary>
    /// This is the interesting class in this sample, it shows off calling the
    /// Microsoft.VisualStudio.Debugger.Engine API to evaluate something.
    /// </summary>
    /// <remarks>
    /// The evaluation helper will evaluate within the context of a specific call stack
    /// frame. Each instance of the class should be disposed when evaluations are
    /// complete for the given stack frame.
    /// </remarks>
    internal sealed class EvaluationHelper : IDisposable
    {
        const int TimeoutInMS = 5000;
        DkmInspectionSession _inspectionSession;
        DkmInspectionContext _inspectionContext;
        DkmStackFrame _stackFrame;

        /// <summary>
        /// Create a new instance of the evaluation helper class. See 'remarks' for important information on using this type.
        /// </summary>
        /// <param name="dte">Visual Studio's DTE service</param>
        public EvaluationHelper(DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            StackFrame? currentDteFrame = dte.Debugger.CurrentStackFrame;
            if (currentDteFrame is null)
            {
                throw new EvaluationFailedException("There is no active call stack frame");
            }

            DkmStackFrame? currentDkmFrame = DkmStackFrame.ExtractFromDTEObject(currentDteFrame);
            if (currentDkmFrame is null)
            {
                throw new EvaluationFailedException("Current debugger is not supported by this extension");
            }

            _stackFrame = currentDkmFrame;

            DkmInstructionAddress? instructionAddress = currentDkmFrame.InstructionAddress;
            if (instructionAddress is null)
            {
                throw new EvaluationFailedException("Current stack frame does not support inspection");
            }

            _inspectionSession = DkmInspectionSession.Create(currentDkmFrame.Process, null);

            DkmLanguage language = currentDkmFrame.Process.EngineSettings.GetLanguage(currentDkmFrame.CompilerId);

            DkmWorkerProcessConnection? symbolsConnection = null;
            DkmInstructionSymbol? instructionSymbol = instructionAddress.GetSymbol();
            if (instructionSymbol is not null)
            {
                symbolsConnection = instructionSymbol.Module.SymbolsConnection;
            }
            else if (instructionAddress.RuntimeInstance.Id.RuntimeType == DkmRuntimeId.Native)
            {
                try
                {
                    symbolsConnection = DkmWorkerProcessConnection.GetLocalSymbolsConnection();
                }
                catch
                {
                    // Ignore failures obtaining the symbols connection since we can evaluate in the IDE process
                }
            }

            _inspectionContext = DkmInspectionContext.Create(
                _inspectionSession,
                instructionAddress.RuntimeInstance,
                currentDkmFrame.Thread,
                TimeoutInMS,
                DkmEvaluationFlags.TreatAsExpression | DkmEvaluationFlags.NoSideEffects | DkmEvaluationFlags.ForceRealFuncEval,
                DkmFuncEvalFlags.None,
                Radix: 10,
                language,
                ReturnValue: null,
                AdditionalVisualizationData: null,
                AdditionalVisualizationDataPriority: DkmCompiledVisualizationDataPriority.None,
                ReturnValues: null,
                symbolsConnection
                );
        }

        public void Dispose()
        {
            _inspectionSession.Close();
        }

        /// <summary>
        /// Asynchronously evaluates the given expression.
        /// </summary>
        /// <param name="expressionText">Text of the expression to evaluate</param>
        /// <returns>The value of the resulting expression</returns>
        public Task<string> EvaluateAsync(string expressionText)
        {
            TaskCompletionSource<string> taskCompletionSource = new TaskCompletionSource<string>();

            var workList = DkmWorkList.Create(null);

            AppendGetExpressionBytes(workList, expressionText, (string? value, Exception? ex) =>
            {
                if (value is not null)
                {
                    taskCompletionSource.SetResult(value);
                }
                else
                {
                    taskCompletionSource.SetException(ex ?? new EvaluationFailedException("Unexpected internal failure"));
                }
            });

            workList.BeginExecution();

            return taskCompletionSource.Task;
        }

        /// <summary>
        /// Synchronously evaluates the given expression. Where possible <see cref="EvaluateAsync"/> should be used instead.
        /// </summary>
        /// <param name="expressionText">Text of the expression to evaluate</param>
        /// <returns>The value of the resulting expression</returns>
        public string EvaluateSync(string expressionText)
        {
            var workList = DkmWorkList.Create(null);
            string? result = null;
            Exception? exception = null;
            AppendGetExpressionBytes(workList, expressionText, (string? value, Exception? ex) =>
            {
                result = value;
                exception = ex;
            });

            workList.Execute();

            if (result is not null)
            {
                return result;
            }
            else
            {
                throw exception ?? new EvaluationFailedException("Unexpected internal failure");
            }
        }

        private void AppendGetExpressionBytes(DkmWorkList workList, string expressionText, Action<string?, Exception?> completionRoutine)
        {
            DkmLanguageExpression expression = DkmLanguageExpression.Create(_inspectionContext.Language, DkmEvaluationFlags.TreatAsExpression, expressionText, null);
            bool success = false;

            try
            {
                _inspectionContext.EvaluateExpression(workList, expression, _stackFrame, (DkmEvaluateExpressionAsyncResult asyncResult) =>
                {
                    try
                    {
                        DkmEvaluationResult evaluationResult = asyncResult.ResultObject;
                        using (evaluationResult)
                        {
                            if (evaluationResult is not DkmSuccessEvaluationResult successEvaluationResult)
                            {
                                if (asyncResult.ResultObject is DkmFailedEvaluationResult failedEvaluationResult)
                                {
                                    throw new EvaluationFailedException(failedEvaluationResult.ErrorMessage);
                                }

                                throw new EvaluationFailedException("Unsupported evaluation result");
                            }

                            completionRoutine(successEvaluationResult.Value ?? string.Empty, null);
                        }
                    }
                    catch (Exception e)
                    {
                        completionRoutine(null, e);
                    }
                    expression.Close();
                });

                success = true; // completion routine now owns expression
            }
            finally
            {
                if (!success)
                {
                    expression.Close();
                }
            }
        }
    }
}
