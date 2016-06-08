﻿//-----------------------------------------------------------------------
// <copyright file="NotificationListener.cs">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//      EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//      MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//      IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//      CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//      TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//      SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Reflection;
using System.ServiceModel;

namespace Microsoft.PSharp.Remote
{
    /// <summary>
    /// Class implementing a remote notification listening service.
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    internal class NotificationListener : IContainerService
    {
        /// <summary>
        /// Notifies the container to start the P# runtime.
        /// </summary>
        void IContainerService.NotifyStartPSharpRuntime()
        {
            Container.StartPSharpRuntime();
        }

        /// <summary>
        /// Notifies the container to terminate.
        /// </summary>
        void IContainerService.NotifyTerminate()
        {
            Environment.Exit(1);
        }
    }
}
