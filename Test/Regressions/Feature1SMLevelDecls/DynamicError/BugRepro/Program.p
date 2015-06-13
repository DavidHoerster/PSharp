﻿// This test found a bug in treatment of function parameters
// in the case when there is a scheduling point inside the function (such as a send), 
// and the mutation performed before the scheduling point is used after it

event PING: machine;
event PONG: machine;

main machine Driver {
   var fd: machine;
    var nodeseq: seq[machine];
   var nodemap: map[machine, bool];
    var i: int;
    var n: machine;
   start state Init {
        entry {
            new Safety();
            i = 0;
            while (i < 2) {
                n = new Node();
                nodeseq += (i, n);
                nodemap += (n, true);
                i = i + 1;
            }
            new Liveness(nodemap);
            fd = new FailureDetector(nodeseq);
            send fd, REGISTER_CLIENT, this;
            i = 0;
            while (i < 2) {
                send nodeseq[i], halt;
                i = i + 1;
            }
        }
        on NODE_DOWN do {
            monitor Liveness, NODE_DOWN, payload;
        };
    }
}

event ROUND_DONE;
event REGISTER_CLIENT: machine;
event UNREGISTER_CLIENT: machine;
event NODE_DOWN: machine;
event TIMER_CANCELED;

machine FailureDetector {
    var nodes: seq[machine];
   var clients: map[machine, bool];
    var attempts: int;
    var alive: map[machine, bool];
    var responses: map[machine, bool];
   var timer: machine;
    
   start state Init {
       entry {
             nodes = payload as seq[machine];
            InitializeAliveSet(0);
            timer = new Timer(this);
            raise UNIT;          
       }
        on REGISTER_CLIENT do { clients[payload as machine] = true; };
        on UNREGISTER_CLIENT do { if ((payload as machine) in clients) clients -= (payload as machine); };
       on UNIT push SendPing;
   }
   state SendPing {
       entry {
            SendPingsToAliveSet(0);
            send timer, START, 100;
        }
       on PONG do { 
            if ((payload as machine) in alive) {
                 responses[(payload as machine)] = true; 
                 if (sizeof(responses) == sizeof(alive)) {
                     send timer, CANCEL;
                     raise TIMER_CANCELED;
                 }
            }
        };
        on TIMEOUT do { 
            attempts = attempts + 1;
			Console.WriteLine("attempts: " + attempts);
			Console.WriteLine("responses: " + responses.Count);
			Console.WriteLine("alive: " + alive.Count);
            if (sizeof(responses) < sizeof(alive) && attempts < 2) {
                raise UNIT;
            }
            Notify(1, 0);
            raise ROUND_DONE;
        };
        on ROUND_DONE goto Reset;
        on UNIT goto SendPing;
        on TIMER_CANCELED push WaitForCancelResponse;
    }
     state WaitForCancelResponse {
         defer TIMEOUT, PONG;
         on CANCEL_SUCCESS do { raise ROUND_DONE; };
         on CANCEL_FAILURE do { pop; };
     }
     state Reset {
        entry {
             attempts = 0;
             responses = default(map[machine, bool]);
             send timer, START, 1000;
         }
         on TIMEOUT goto SendPing;
         ignore PONG;
     }

     fun InitializeAliveSet(i: int) {
        i = 0;
        while (i < sizeof(nodes)) {
            alive += (nodes[i], true);
            i = i + 1;
        }
     }
     fun SendPingsToAliveSet(i: int) {
        i = 0;
        while (i < sizeof(nodes)) {
            if (nodes[i] in alive && !(nodes[i] in responses)) {
                monitor Safety, M_PING, nodes[i];
                send nodes[i], PING, this;
            }
            i = i + 1;
        }
     }
    fun Notify(i: int, j: int) {
         i = 0;
         while (i < sizeof(nodes)) {
             if (nodes[i] in alive && !(nodes[i] in responses)) {
                 alive -= nodes[i];
                 j = 0;
                 while (j < sizeof(clients)) {
                     send keys(clients)[j], NODE_DOWN, nodes[i];
                     j = j + 1;
                 }
             }
             i = i + 1;
         }
     }
}

machine Node {
    start state WaitPing {
       on PING do {
            monitor Safety, M_PONG, this;
            send payload as machine, PONG, this;
        };
   }
}

event M_PING: machine;
event M_PONG: machine;
monitor Safety {
    var pending: map[machine, int];
   start state Init {
        on M_PING do { 
            if (!((payload as machine) in pending))
                pending[(payload as machine)] = 0;
            pending[(payload as machine)] = pending[(payload as machine)] + 1;
			Console.WriteLine(" >> higher: " + pending[(payload as machine)]);
            assert (pending[(payload as machine)] <= 3);   //fails
        };
        on M_PONG do { 
            assert ((payload as machine) in pending);
            assert (0 < pending[(payload as machine)]);
            pending[(payload as machine)] = pending[(payload as machine)] - 1;
			Console.WriteLine(" >> lower: " + pending[(payload as machine)]);
        };
    }
}

monitor Liveness {
    var nodes: map[machine, bool];
    start hot state Init {
        entry {
            nodes = payload as map[machine, bool];
        }
        on NODE_DOWN do {
            nodes -= payload as machine;
            if (sizeof(nodes) == 0) 
                raise UNIT;
        };
        on UNIT goto Done;
    }
    state Done {
    }
}