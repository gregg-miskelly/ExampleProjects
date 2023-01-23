// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using CustomMessaging.Shared;

namespace CustomMessaging.Backend
{
    public class EntryPoint : IDkmCustomMessageForwardReceiver
    {
        public DkmCustomMessage SendLower(DkmCustomMessage customMessage)
        {
            var code = (CustomMessageDefinition.Code)customMessage.MessageCode;
            switch (code)
            {
                case CustomMessageDefinition.Code.HelloRequest:
                    return DkmCustomMessage.Create(null, null, CustomMessageDefinition.SourceId, (int)CustomMessageDefinition.Code.HelloResponse, string.Format("CustomMessaging.Backend replying at {0}", DateTime.Now), null);

                default:
                    throw new ArgumentOutOfRangeException(nameof(customMessage));
            }
        }
    }
}
