using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace DebugEngineEvalSample
{
    internal static class VSUtilities
    {
        public static T GetRequiredService<T>() => GetRequiredService<T>(typeof(T));

        public static T GetRequiredService<T>(Type serviceType)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            object result = Package.GetGlobalService(serviceType);
            if (result == null)
            {
                throw new ServiceUnavailableException(serviceType);
            }

            return (T)result;
        }
    }
}
