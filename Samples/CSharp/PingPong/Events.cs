﻿using Microsoft.PSharp;

namespace PingPong
{
    #region Events

    internal class Config : Event
    {
        public MachineId Id;

        public Config(MachineId id)
            : base(-1, -1)
        {
            this.Id = id;
        }
    }

    internal class Unit : Event { }
    internal class Ping : Event { }
    internal class Pong : Event { }

    #endregion
}
