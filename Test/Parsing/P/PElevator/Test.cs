﻿using System;
using System.Collections.Generic;
using Microsoft.PSharp;

namespace PElevator
{
    public class Test
    {
        static void Main(string[] args)
        {
            Runtime.RegisterNewEvent(typeof(eOpenDoor));
            Runtime.RegisterNewEvent(typeof(eCloseDoor));
            Runtime.RegisterNewEvent(typeof(eResetDoor));
            Runtime.RegisterNewEvent(typeof(eDoorOpened));
            Runtime.RegisterNewEvent(typeof(eDoorClosed));
            Runtime.RegisterNewEvent(typeof(eDoorStopped));
            Runtime.RegisterNewEvent(typeof(eObjectDetected));
            Runtime.RegisterNewEvent(typeof(eTimerFired));
            Runtime.RegisterNewEvent(typeof(eOperationSuccess));
            Runtime.RegisterNewEvent(typeof(eOperationFailure));
            Runtime.RegisterNewEvent(typeof(eSendCommandToOpenDoor));
            Runtime.RegisterNewEvent(typeof(eSendCommandToCloseDoor));
            Runtime.RegisterNewEvent(typeof(eSendCommandToStopDoor));
            Runtime.RegisterNewEvent(typeof(eSendCommandToResetDoor));
            Runtime.RegisterNewEvent(typeof(eStartDoorCloseTimer));
            Runtime.RegisterNewEvent(typeof(eStopDoorCloseTimer));
            Runtime.RegisterNewEvent(typeof(eUnit));
            Runtime.RegisterNewEvent(typeof(eStopTimerReturned));
            Runtime.RegisterNewEvent(typeof(eObjectEncountered));

            Runtime.RegisterNewMachine(typeof(Elevator));
            Runtime.RegisterNewMachine(typeof(User));
            Runtime.RegisterNewMachine(typeof(Door));
            Runtime.RegisterNewMachine(typeof(Timer));

            Runtime.RegisterNewMonitor(typeof(M));

            Runtime.Start();
        }
    }
}
