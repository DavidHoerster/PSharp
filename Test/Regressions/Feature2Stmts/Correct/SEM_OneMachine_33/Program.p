﻿//Tests sequences and type casting
event unit;
event seqpayload: seq[int];

main machine Entry {
	var l: seq[int];
    var i:int;
	var mac:machine;
	var t :(seq[int], int);
    start state init {
       entry {
			l += (0,12);
			l += (0,23);
			l += (0,12);
			l += (0,23);
			l += (0,12);
			l += (0,23);
			mac = new Tester(l, 1);
			send mac, seqpayload, l;
	   }
    }
}

machine Tester {
	var ii:seq[int];
	var rec:seq[int];
	var i:int;
	start state init {
		entry {
		      ii = (payload as (seq[int],int)).0;
			  assert( (payload as (seq[int],int)).0[0] == 23 );
		      assert( (payload as (seq[int],int)).1 == 1 );
		}
		on seqpayload goto testitnow;
	}
	
	state testitnow {
		entry {
		    rec = payload as seq[int];
			i = sizeof(rec) - 1;
			while(i >= 0)
			{
				assert(rec[i] == ii[i]);
				i = i - 1;
			}
		}
	}
}
