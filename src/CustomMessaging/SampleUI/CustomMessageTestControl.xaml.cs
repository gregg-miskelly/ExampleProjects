// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Debugger;
using CustomMessaging.Shared;

namespace CustomMessaging.SampleUI
{
    /// <summary>
    /// Interaction logic for CustomMessageTestControl.
    /// </summary>
    public partial class CustomMessageTestControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CustomMessageTestControl"/> class.
        /// </summary>
        public CustomMessageTestControl()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Handles click on the button by displaying a message box.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event args.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Justification = "Sample code")]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            DkmProcess[] processes = null;

            try
            {
                processes = DkmProcess.GetProcesses();
            }
            catch (ObjectDisposedException)
            {
                // If debugging has never started, DkmProcess.GetProcesses will throw
            }

            if (processes is null || processes.Length == 0)
            {
                // NOTE: It is actually possible to send custom messages when not debugging, but this sample doesn't support it
                MessageBox.Show("Unable to send custom messages when not debugging", "Custom Message Test", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Arbitrarily choose the first process custom
            DkmProcess process = processes[0];

            DkmCustomMessage customMessage = DkmCustomMessage.Create(process.Connection, process, CustomMessageDefinition.SourceId, (int)CustomMessageDefinition.Code.HelloRequest, null, null);
            DkmCustomMessage reply = customMessage.SendLower();

            if (reply is null ||
                reply.MessageCode != (int)CustomMessageDefinition.Code.HelloResponse ||
                reply.Parameter1 is not string messageText)
            {
                MessageBox.Show("Unexpected custom message response", "Custom Message Test", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show("Custom message response:\n\n" + messageText, "Custom Message Test", MessageBoxButton.OK);
        }
    }
}