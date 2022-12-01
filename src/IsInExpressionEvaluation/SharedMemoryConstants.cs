using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    internal static class SharedMemoryConstants
    {
        public const string SharedMemoryPrefix = "Local\\IsInExpressionEvaluation_";
        public const int MaxSize = 4096;
    }
}
