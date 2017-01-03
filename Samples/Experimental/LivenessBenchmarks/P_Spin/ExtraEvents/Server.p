﻿#include "Timer.p"

event Event1;
event Event2;
event Event3;
event CtrExceeded;

machine Server
{
	var ctr1 : int;
	var ctr2 : int;

	start state Init
	{
		entry
		{
			ctr1 = 0;
            ctr2 = 0;
            new Timer(this);
		}
		defer Event3;
		on Event1 do
		{
			ctr1 = ctr1 + 1;
		}
		on Event2 do
		{
			ctr2 = ctr2 + 1;
		}
		on TimerTickEvent do
		{
			if(ctr2 > ctr1)
            {
                announce CtrExceeded;
            }
		}
	}
}

spec LivenessMonitor observes CtrExceeded
{
	start cold state init
	{	
		on CtrExceeded goto HotState;
	}

	hot state HotState
	{
		on CtrExceeded goto HotState;
	}
}