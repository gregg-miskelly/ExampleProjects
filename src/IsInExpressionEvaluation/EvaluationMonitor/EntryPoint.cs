using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace EvaluationMonitor
{
    internal class EntryPoint : IDkmFuncEvalStartingNotification, IDkmFuncEvalCompletedNotification
    {
        public void OnFuncEvalStarting(DkmThread thread, DkmFuncEvalFlags flags, DkmEventDescriptor eventDescriptor)
        {
            if (!SharedMemory.TryGetInstance(thread.Process, out SharedMemory sharedMemory))
                return;

            sharedMemory.SetIsInEvaluation(newValue: true);
        }

        void IDkmFuncEvalCompletedNotification.OnFuncEvalCompleted(DkmThread thread, DkmFuncEvalFlags flags, DkmEventDescriptor eventDescriptor)
        {
            if (!SharedMemory.TryGetInstance(thread.Process, out SharedMemory sharedMemory))
                return;

            sharedMemory.SetIsInEvaluation(newValue: false);
        }
    }
}
