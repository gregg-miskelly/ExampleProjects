// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace CustomMessaging.SampleUI
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("701547e2-93cc-4f26-a213-792416e43fcf")]
    public class CustomMessageTest : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CustomMessageTest"/> class.
        /// </summary>
        public CustomMessageTest() : base(null)
        {
            this.Caption = "CustomMessageTest";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new CustomMessageTestControl();
        }
    }
}
