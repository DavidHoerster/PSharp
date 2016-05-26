﻿//-----------------------------------------------------------------------
// <copyright file="IRemoteCommunication.cs">
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
using System.ServiceModel;

namespace Microsoft.PSharp.Remote
{
    /// <summary>
    /// Interface for remote P# machine communication.
    /// </summary>
    [ServiceContract(Namespace = "Microsoft.PSharp")]
    [ServiceKnownType("GetKnownTypes", typeof(KnownTypesProvider))]
    internal interface IRemoteCommunication
    {
        /// <summary>
        /// Creates a new machine of the given type and with
        /// the given event.
        /// </summary>
        /// <param name="typeName">Type of the machine</param>
        /// <param name="e">Event</param>
        /// <param name="friendlyName">A friendly name to be given to the machine</param>
        /// <returns>MachineId</returns> 
        [OperationContract]
        MachineId CreateMachine(string typeName, Event e, string friendlyName);

        /// <summary>
        /// Sends an asynchronous event to a machine.
        /// </summary>
        /// <param name="target">Target machine id</param>
        /// <param name="e">Event</param>
        //[OperationContract(IsOneWay = true)]
        [OperationContract]
        void SendEvent(MachineId target, Event e);
    }
}
