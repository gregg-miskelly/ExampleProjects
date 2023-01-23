// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace CustomMessaging.Shared
{
    static class CustomMessageDefinition
    {
        // NOTE: must match Backend\CustomMessagingBackend.vsdconfigxml
        public static readonly Guid SourceId = new Guid("{FFAACB22-358D-4163-9F2F-233DB5830FD6}");

        public enum Code
        {
            HelloRequest = 0,
            HelloResponse = 1
        }
    }
}