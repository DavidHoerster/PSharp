﻿event x;
main machine TestMachine {
    var x : int;
    start state Init {
        entry {
        foo(1, 3, x);    
        }
    }
    
    fun foo (x : any, y : int, z : event) : int {
   return 0;
    }

}