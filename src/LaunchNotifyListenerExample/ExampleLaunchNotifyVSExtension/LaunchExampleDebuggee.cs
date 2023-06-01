using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace ExampleLaunchNotifyVSExtension
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class LaunchExampleDebuggee
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("628c8fb9-4f96-41d9-b0ca-4cc29046d97a");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        private IVsDebugLaunchNotifyListener110 currentListener;

        private Process currentOrchestratorProcess;

        /// <summary>
        /// Initializes a new instance of the <see cref="LaunchExampleDebuggee"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private LaunchExampleDebuggee(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static LaunchExampleDebuggee Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in LaunchExampleDebuggee's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new LaunchExampleDebuggee(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private unsafe void Execute(object sender, EventArgs e)
        {
            int hr;

            // NOTE: Update this based on where the root of the repo is on your machine
            const string pathToExampleDebuggee = @"C:\dd\ExampleProjects\src\LaunchNotifyListenerExample\ExampleDebuggee\bin\Debug\net6.0\ExampleDebuggee.exe";
            if (!File.Exists(pathToExampleDebuggee))
            {
                throw new InvalidOperationException("`pathToExampleDebuggee` in LaunchExampleDebuggee.Example needs to be updated in the sample.");
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            currentListener?.Close();
            currentOrchestratorProcess?.Close();

            var listenerFactory = this.package.GetService<SVsShellDebugger, IVsDebugLaunchNotifyListenerFactory110>();
            // This example is hard coded to always debug with the .NET6+/.NET Core engine guid
            Guid[] engineFilter = { VSConstants.DebugEnginesGuids.CoreSystemClr_guid };
            fixed (Guid* pEngineFilter = engineFilter)
            {
                // For local machine
                const string transportQualifier = null;
                Guid portSupplier = Guid.Empty;

                VsDebugLaunchNotifyListenerProperties[] properties = new VsDebugLaunchNotifyListenerProperties[1];
                properties[0].EngineFilterArray = (IntPtr)pEngineFilter;
                properties[0].EngineFilterCount = (uint)engineFilter.Length;
                properties[0].ExpectedSessionId = uint.MaxValue; // use the session of devenv/msvsmon

                hr = listenerFactory.StartListener(transportQualifier, ref portSupplier, properties, out currentListener);
                ErrorHandler.ThrowOnFailure(hr);
            }

            hr = currentListener.GetTargetStartInfo(out string notifyCommandLine, out string targetProcessEnvironment);
            ErrorHandler.ThrowOnFailure(hr);

            string orchestratorPath = Path.Combine(Path.GetDirectoryName(typeof(LaunchExampleDebuggee).Assembly.Location), "FakeOrchestrator.exe");
            if (!File.Exists(orchestratorPath))
            {
                throw new FileNotFoundException("FakeOrchestrator.exe not found", orchestratorPath);
            }

            var processStartInfo = new ProcessStartInfo(orchestratorPath);
            processStartInfo.EnvironmentVariables["VSDebugNotifyCmd"] = notifyCommandLine;
            processStartInfo.UseShellExecute = false;

            // For simplicity, this example just passes the environment variables to the orchestrator itself. A more real world
            // implementation would probably pass this in a way that it would just make it to the debuggee.
            if (targetProcessEnvironment != null)
            {
                foreach (string envVarPair in targetProcessEnvironment.Split('\0'))
                {
                    if (string.IsNullOrEmpty(envVarPair))
                    {
                        break;
                    }

                    int equalsIndex = envVarPair.IndexOf('=');
                    if (equalsIndex == -1)
                    {
                        throw new InvalidOperationException($"Invalid environment variable pair: {envVarPair}");
                    }

                    string envVarName = envVarPair.Substring(0, equalsIndex);
                    string envVarValue = envVarPair.Substring(equalsIndex + 1);

                    processStartInfo.EnvironmentVariables[envVarName] = envVarValue;
                }
            }

            processStartInfo.Arguments = $"\"{pathToExampleDebuggee}\"";

            currentOrchestratorProcess = new Process();
            currentOrchestratorProcess.StartInfo = processStartInfo;
            currentOrchestratorProcess.Exited += OrchestratorProcess_Exited;
            currentOrchestratorProcess.EnableRaisingEvents = true;
            currentOrchestratorProcess.Start();
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void OrchestratorProcess_Exited(object sender, EventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                int exitCode = this.currentOrchestratorProcess.ExitCode;

                this.currentOrchestratorProcess.Close();
                this.currentOrchestratorProcess = null;

                this.currentListener.Close();
                this.currentListener = null;

                if (exitCode != 0)
                {
                    MessageBox.Show($"Orchestrator process exited with non-zero exit code: {exitCode}");
                }
                else
                {
                    MessageBox.Show("Orchestrator process exited successfully");
                }
            }
            catch
            {
            }
        }
    }
}
