﻿using System;
using System.Collections.Generic;
using Microsoft.PSharp;

namespace Raft
{
    /// <summary>
    /// A server in Raft can be one of the following three roles:
    /// follower, candidate or leader.
    /// </summary>
    internal class Server : Machine
    {
        #region server-specific events

        /// <summary>
        /// Used to configure the server.
        /// </summary>
        public class ConfigureEvent : Event
        {
            public int Id;
            public MachineId[] Servers;
            public MachineId Environment;

            public ConfigureEvent(int id, MachineId[] servers, MachineId env)
                : base()
            {
                this.Id = id;
                this.Servers = servers;
                this.Environment = env;
            }
        }

        /// <summary>
        /// Initiated by candidates during elections.
        /// </summary>
        public class VoteRequest : Event
        {
            public int Term; // candidate’s term
            public MachineId CandidateId; // candidate requesting vote
            public int LastLogIndex; // index of candidate’s last log entry
            public int LastLogTerm; // term of candidate’s last log entry

            public VoteRequest(int term, MachineId candidateId, int lastLogIndex, int lastLogTerm)
                : base()
            {
                this.Term = term;
                this.CandidateId = candidateId;
                this.LastLogIndex = lastLogIndex;
                this.LastLogTerm = lastLogTerm;
            }
        }

        /// <summary>
        /// Response to a vote request.
        /// </summary>
        public class VoteResponse : Event
        {
            public int Term; // currentTerm, for candidate to update itself
            public bool VoteGranted; // true means candidate received vote

            public VoteResponse(int term, bool voteGranted)
                : base()
            {
                this.Term = term;
                this.VoteGranted = voteGranted;
            }
        }
        
        /// <summary>
        /// Initiated by leaders to replicate log entries and
        /// to provide a form of heartbeat.
        /// </summary>
        public class AppendEntriesRequest : Event
        {
            public int Term; // leader's term
            public MachineId LeaderId; // so follower can redirect clients
            public int PrevLogIndex; // index of log entry immediately preceding new ones 
            public int PrevLogTerm; // term of PrevLogIndex entry
            public List<Log> Entries; // log entries to store (empty for heartbeat; may send more than one for efficiency) 
            public int LeaderCommit; // leader’s CommitIndex

            public MachineId ReceiverEndpoint; // client

            public AppendEntriesRequest(int term, MachineId leaderId, int prevLogIndex,
                int prevLogTerm, List<Log> entries, int leaderCommit, MachineId client)
                : base()
            {
                this.Term = term;
                this.LeaderId = leaderId;
                this.PrevLogIndex = prevLogIndex;
                this.PrevLogTerm = prevLogTerm;
                this.Entries = entries;
                this.LeaderCommit = leaderCommit;
                this.ReceiverEndpoint = client;
            }
        }
        
        /// <summary>
        /// Response to an append entries request.
        /// </summary>
        public class AppendEntriesResponse : Event
        {
            public int Term; // current Term, for leader to update itself 
            public bool Success; // true if follower contained entry matching PrevLogIndex and PrevLogTerm 

            public MachineId Server;
            public MachineId ReceiverEndpoint; // client
            
            public AppendEntriesResponse(int term, bool success, MachineId server, MachineId client)
                : base()
            {
                this.Term = term;
                this.Success = success;
                this.Server = server;
                this.ReceiverEndpoint = client;
            }
        }

        // Events for transitioning a server between roles.
        private class BecomeFollower : Event { }
        private class BecomeCandidate : Event { }
        private class BecomeLeader : Event { }

        #endregion

        #region fields

        /// <summary>
        /// The id of this server.
        /// </summary>
        int ServerId;

        /// <summary>
        /// The environment machine.
        /// </summary>
        MachineId Environment;

        /// <summary>
        /// The servers.
        /// </summary>
        MachineId[] Servers;

        /// <summary>
        /// Leader id.
        /// </summary>
        MachineId LeaderId;

        /// <summary>
        /// The timer of this server.
        /// </summary>
        MachineId Timer;

        /// <summary>
        /// Latest term server has seen (initialized to 0 on
        /// first boot, increases monotonically).
        /// </summary>
        int CurrentTerm;

        /// <summary>
        /// Candidate id that received vote in current term (or null if none).
        /// </summary>
        MachineId VotedFor;
        
        /// <summary>
        /// Log entries.
        /// </summary>
        List<Log> Logs;

        /// <summary>
        /// Index of highest log entry known to be committed (initialized
        /// to 0, increases monotonically). 
        /// </summary>
        int CommitIndex;

        /// <summary>
        /// Index of highest log entry applied to state machine (initialized
        /// to 0, increases monotonically).
        /// </summary>
        int LastApplied;

        /// <summary>
        /// For each server, index of the next log entry to send to that
        /// server (initialized to leader last log index + 1). 
        /// </summary>
        Dictionary<MachineId, int> NextIndex;

        /// <summary>
        /// For each server, index of highest log entry known to be replicated
        /// on server (initialized to 0, increases monotonically).
        /// </summary>
        Dictionary<MachineId, int> MatchIndex;

        /// <summary>
        /// Number of received votes.
        /// </summary>
        int VotesReceived;

        #endregion

        #region initialization

        [Start]
        [OnEntry(nameof(EntryOnInit))]
        [OnEventDoAction(typeof(ConfigureEvent), nameof(Configure))]
        [OnEventGotoState(typeof(BecomeFollower), typeof(Follower))]
        [DeferEvents(typeof(VoteRequest))]
        class Init : MachineState { }

        void EntryOnInit()
        {
            this.CurrentTerm = 0;

            this.LeaderId = null;
            this.VotedFor = null;

            this.Logs = new List<Log>();

            this.CommitIndex = 0;
            this.LastApplied = 0;

            this.NextIndex = new Dictionary<MachineId, int>();
            this.MatchIndex = new Dictionary<MachineId, int>();
        }

        void Configure()
        {
            this.ServerId = (this.ReceivedEvent as ConfigureEvent).Id;
            this.Servers = (this.ReceivedEvent as ConfigureEvent).Servers;
            this.Environment = (this.ReceivedEvent as ConfigureEvent).Environment;

            this.Timer = this.CreateMachine(typeof(Timer));
            this.Send(this.Timer, new Timer.ConfigureEvent(this.Id));
            this.Send(this.Timer, new Timer.StartTimer());

            this.Raise(new BecomeFollower());
        }

        #endregion

        #region follower

        [OnEntry(nameof(FollowerOnInit))]
        [OnEventDoAction(typeof(Client.Request), nameof(RedirectClientRequest))]
        [OnEventDoAction(typeof(Timer.Timeout), nameof(StartLeaderElection))]
        [OnEventDoAction(typeof(VoteRequest), nameof(VoteAsFollower))]
        [OnEventDoAction(typeof(VoteResponse), nameof(RespondVoteAsFollower))]
        [OnEventDoAction(typeof(AppendEntriesRequest), nameof(AppendEntriesAsFollower))]
        [OnEventDoAction(typeof(AppendEntriesResponse), nameof(RespondAppendEntriesAsFollower))]
        [OnEventGotoState(typeof(BecomeFollower), typeof(Follower))]
        [OnEventGotoState(typeof(BecomeCandidate), typeof(Candidate))]
        class Follower : MachineState { }

        void FollowerOnInit()
        {
            this.LeaderId = null;
            this.VotesReceived = 0;

            this.Send(this.Timer, new Timer.ResetTimer());
        }

        void RedirectClientRequest()
        {
            if (this.LeaderId != null)
            {
                this.Send(this.LeaderId, this.ReceivedEvent);
            }
            else
            {
                var request = this.ReceivedEvent as Client.Request;
                this.Send(request.Client, new Client.ResponseError());
            }
        }

        void StartLeaderElection()
        {
            this.Raise(new BecomeCandidate());
        }

        void VoteAsFollower()
        {
            var request = this.ReceivedEvent as VoteRequest;
            if (request.Term > this.CurrentTerm)
            {
                this.CurrentTerm = request.Term;
                this.VotedFor = null;
            }

            this.Vote(this.ReceivedEvent as VoteRequest);
        }

        void RespondVoteAsFollower()
        {
            var request = this.ReceivedEvent as VoteResponse;
            if (request.Term > this.CurrentTerm)
            {
                this.CurrentTerm = request.Term;
                this.VotedFor = null;
            }
        }

        void AppendEntriesAsFollower()
        {
            var request = this.ReceivedEvent as AppendEntriesRequest;
            if (request.Term > this.CurrentTerm)
            {
                this.CurrentTerm = request.Term;
                this.VotedFor = null;
            }

            this.AppendEntries(this.ReceivedEvent as AppendEntriesRequest);
        }

        void RespondAppendEntriesAsFollower()
        {
            var request = this.ReceivedEvent as AppendEntriesResponse;
            if (request.Term > this.CurrentTerm)
            {
                this.CurrentTerm = request.Term;
                this.VotedFor = null;
            }
        }

        #endregion

        #region candidate

        [OnEntry(nameof(CandidateOnInit))]
        [OnEventDoAction(typeof(VoteRequest), nameof(VoteAsCandidate))]
        [OnEventDoAction(typeof(VoteResponse), nameof(RespondVoteAsCandidate))]
        [OnEventDoAction(typeof(AppendEntriesRequest), nameof(AppendEntriesAsCandidate))]
        [OnEventDoAction(typeof(AppendEntriesResponse), nameof(RespondAppendEntriesAsCandidate))]
        [OnEventGotoState(typeof(BecomeLeader), typeof(Leader))]
        [OnEventGotoState(typeof(BecomeFollower), typeof(Follower))]
        [IgnoreEvents(typeof(Timer.Timeout))] // temporary
        class Candidate : MachineState { }

        void CandidateOnInit()
        {
            this.CurrentTerm++;
            this.VotedFor = this.Id;
            this.VotesReceived = 1;

            for (int idx = 0; idx < this.Servers.Length; idx++)
            {
                if (idx == this.ServerId)
                    continue;

                var lastLogIndex = this.Logs.Count;
                var lastLogTerm = 0;
                if (lastLogIndex > 0)
                {
                    lastLogTerm = this.Logs[lastLogTerm - 1].Term;
                }

                this.Send(this.Servers[idx], new VoteRequest(this.CurrentTerm, this.Id,
                    lastLogIndex, lastLogTerm));
            }
        }

        void VoteAsCandidate()
        {
            var request = this.ReceivedEvent as VoteRequest;
            if (request.Term > this.CurrentTerm)
            {
                this.CurrentTerm = request.Term;
                this.VotedFor = null;
                this.Vote(this.ReceivedEvent as VoteRequest);
                this.Raise(new BecomeFollower());
            }
            else
            {
                this.Vote(this.ReceivedEvent as VoteRequest);
            }
        }

        void RespondVoteAsCandidate()
        {
            var request = this.ReceivedEvent as VoteResponse;
            if (request.Term > this.CurrentTerm)
            {
                this.CurrentTerm = request.Term;
                this.VotedFor = null;
                this.Raise(new BecomeFollower());
            }
            else if (request.Term != this.CurrentTerm)
            {
                return;
            }

            if (request.VoteGranted)
            {
                this.VotesReceived++;
                if (this.VotesReceived == (this.Servers.Length / 2) + 1)
                {
                    Console.WriteLine("\nleader: " + this.ServerId + " | term " + this.CurrentTerm + " | " +
                        this.VotesReceived + " votes" + " | log size: " + this.Logs.Count + "\n");
                    this.Raise(new BecomeLeader());
                }
            }
        }

        void AppendEntriesAsCandidate()
        {
            var request = this.ReceivedEvent as AppendEntriesRequest;
            if (request.Term > this.CurrentTerm)
            {
                this.CurrentTerm = request.Term;
                this.VotedFor = null;
                this.AppendEntries(this.ReceivedEvent as AppendEntriesRequest);
                this.Raise(new BecomeFollower());
            }
            else
            {
                this.AppendEntries(this.ReceivedEvent as AppendEntriesRequest);
            }
        }

        void RespondAppendEntriesAsCandidate()
        {
            var request = this.ReceivedEvent as AppendEntriesResponse;
            if (request.Term > this.CurrentTerm)
            {
                this.CurrentTerm = request.Term;
                this.VotedFor = null;
                this.Raise(new BecomeFollower());
            }
        }

        #endregion

        #region leader

        [OnEntry(nameof(LeaderOnInit))]
        [OnEventDoAction(typeof(Client.Request), nameof(ProcessClientRequest))]
        [OnEventDoAction(typeof(VoteRequest), nameof(VoteAsLeader))]
        [OnEventDoAction(typeof(VoteResponse), nameof(RespondVoteAsLeader))]
        [OnEventDoAction(typeof(AppendEntriesRequest), nameof(AppendEntriesAsLeader))]
        [OnEventDoAction(typeof(AppendEntriesResponse), nameof(RespondAppendEntriesAsLeader))]
        [OnEventGotoState(typeof(BecomeFollower), typeof(Follower))]
        class Leader : MachineState { }

        void LeaderOnInit()
        {
            this.VotesReceived = 0;

            this.Monitor<SafetyMonitor>(new SafetyMonitor.NotifyLeaderElected(this.Id, this.CurrentTerm));

            this.Send(this.Environment, new Environment.NotifyLeaderUpdate(this.Id));

            var logIndex = this.Logs.Count;
            var logTerm = this.GetLogTermForIndex(logIndex);

            this.NextIndex.Clear();
            this.MatchIndex.Clear();
            for (int idx = 0; idx < this.Servers.Length; idx++)
            {
                if (idx == this.ServerId)
                    continue;
                this.NextIndex.Add(this.Servers[idx], logIndex + 1);
                this.MatchIndex.Add(this.Servers[idx], 0);
            }

            for (int idx = 0; idx < this.Servers.Length; idx++)
            {
                if (idx == this.ServerId)
                    continue;
                this.Send(this.Servers[idx], new AppendEntriesRequest(this.CurrentTerm, this.Id,
                    logIndex, logTerm, new List<Log>(), this.CommitIndex, null));
            }
        }

        void ProcessClientRequest()
        {
            var request = this.ReceivedEvent as Client.Request;
            var log = new Log(this.CurrentTerm, request.Command);
            
            this.Logs.Add(log);
            
            var prevLogIndex = this.GetPreviousLogIndex();
            var prevLogTerm = this.GetLogTermForIndex(prevLogIndex);
            
            Console.WriteLine("\nleader: " + this.ServerId + " new client request " + request.Command +
                " | term " + this.CurrentTerm + " | log count:" + this.Logs.Count + "\n");

            this.VotesReceived = 1;
            for (int idx = 0; idx < this.Servers.Length; idx++)
            {
                if (idx == this.ServerId)
                    continue;

                var server = this.Servers[idx];
                var nextIndex = this.NextIndex[server];
                if (prevLogIndex + 1 < nextIndex)
                    continue;

                var trueIndex = nextIndex - 1;
                var logs = this.Logs.GetRange(trueIndex, this.Logs.Count - trueIndex);
                this.Send(server, new AppendEntriesRequest(this.CurrentTerm, this.Id, prevLogIndex,
                    prevLogTerm, logs, this.CommitIndex, request.Client));
            }
        }

        void VoteAsLeader()
        {
            var request = this.ReceivedEvent as VoteRequest;

            if (request.Term > this.CurrentTerm)
            {
                this.CurrentTerm = request.Term;
                this.VotedFor = null;
                this.Vote(this.ReceivedEvent as VoteRequest);
                this.Raise(new BecomeFollower());
            }
            else
            {
                this.Vote(this.ReceivedEvent as VoteRequest);
            }
        }

        void RespondVoteAsLeader()
        {
            var request = this.ReceivedEvent as VoteResponse;
            if (request.Term > this.CurrentTerm)
            {
                this.CurrentTerm = request.Term;
                this.VotedFor = null;
                this.Raise(new BecomeFollower());
            }
        }

        void AppendEntriesAsLeader()
        {
            var request = this.ReceivedEvent as AppendEntriesRequest;
            if (request.Term > this.CurrentTerm)
            {
                this.CurrentTerm = request.Term;
                this.VotedFor = null;
                this.AppendEntries(this.ReceivedEvent as AppendEntriesRequest);
                this.Raise(new BecomeFollower());
            }
        }
        
        void RespondAppendEntriesAsLeader()
        {
            var request = this.ReceivedEvent as AppendEntriesResponse;
            if (request.Term > this.CurrentTerm)
            {
                this.CurrentTerm = request.Term;
                this.VotedFor = null;
                this.Raise(new BecomeFollower());
            }
            else if (request.Term != this.CurrentTerm)
            {
                return;
            }

            if (request.Success)
            {
                this.NextIndex[request.Server] = this.Logs.Count + 1;
                this.MatchIndex[request.Server] = this.Logs.Count;

                this.VotesReceived++;
                if (request.ReceiverEndpoint != null &&
                    this.VotesReceived == (this.Servers.Length / 2) + 1)
                {
                    Console.WriteLine("\nleader-commit: " + this.ServerId + " in term " + this.CurrentTerm +
                        " with " + this.VotesReceived + " votes\n");

                    var commitIndex = this.MatchIndex[request.Server];
                    if (commitIndex > this.CommitIndex &&
                        this.Logs[commitIndex - 1].Term == this.CurrentTerm)
                    {
                        this.CommitIndex = commitIndex;
                    }

                    this.Send(request.ReceiverEndpoint, new Client.Response());
                }
            }
            else
            {
                this.NextIndex[request.Server] = this.NextIndex[request.Server] - 1;
                
                var prevLogIndex = this.GetPreviousLogIndex();
                var prevLogTerm = this.GetLogTermForIndex(prevLogIndex);

                Console.WriteLine("ID: " + this.ServerId);
                Console.WriteLine("term: " + this.CurrentTerm);
                Console.WriteLine("nextIndex: " + (this.NextIndex[request.Server] - 1));
                Console.WriteLine("count: " + (this.Logs.Count - (this.NextIndex[request.Server] - 1)));

                var nextIndex = this.NextIndex[request.Server] - 1;
                var logs = this.Logs.GetRange(nextIndex, this.Logs.Count - nextIndex);
                this.Send(request.Server, new AppendEntriesRequest(this.CurrentTerm, this.Id, prevLogIndex,
                    prevLogTerm, logs, this.CommitIndex, request.ReceiverEndpoint));
            }
        }

        #endregion

        #region core methods

        void Vote(VoteRequest request)
        {
            var lastLogIndex = this.Logs.Count;
            var lastLogTerm = this.GetLogTermForIndex(lastLogIndex);

            if (request.Term < this.CurrentTerm ||
                (this.VotedFor != null && this.VotedFor != request.CandidateId) ||
                lastLogIndex > request.LastLogIndex ||
                lastLogTerm > request.LastLogTerm)
            {
                Console.WriteLine("\nvote: " + this.ServerId + " false | term " + this.CurrentTerm +
                    " | log size: " + this.Logs.Count + "\n");
                this.Send(request.CandidateId, new VoteResponse(this.CurrentTerm, false));
            }
            else
            {
                Console.WriteLine("\nvote: " + this.ServerId + " true | term " + this.CurrentTerm +
                    " | log size: " + this.Logs.Count + "\n");

                this.VotedFor = request.CandidateId;
                this.LeaderId = null;

                this.Send(request.CandidateId, new VoteResponse(this.CurrentTerm, true));
            }
        }

        void AppendEntries(AppendEntriesRequest request)
        {
            if (request.Term < this.CurrentTerm)
            {
                Console.WriteLine("\nappend false: " + this.ServerId + " | term " + this.CurrentTerm +
                    " | log size: " + this.Logs.Count + " | last applied: " + this.LastApplied + "\n");
                this.Send(this.Timer, new Timer.ResetTimer());
                this.Send(request.LeaderId, new AppendEntriesResponse(this.CurrentTerm, false,
                    this.Id, request.ReceiverEndpoint));
            }
            else
            {
                this.Send(this.Timer, new Timer.ResetTimer());

                Console.WriteLine("\nappend true: " + this.ServerId + " | term " + this.CurrentTerm +
                    " | log size: " + this.Logs.Count + " | entries received: " + request.Entries.Count +
                    " | last applied: " + this.LastApplied + "\n");

                if (request.PrevLogIndex > 0 &&
                    (this.Logs.Count < request.PrevLogIndex ||
                    this.Logs[request.PrevLogIndex - 1].Term != request.PrevLogTerm))
                {
                    this.Send(request.LeaderId, new AppendEntriesResponse(this.CurrentTerm,
                        false, this.Id, request.ReceiverEndpoint));
                }
                else
                {
                    if (request.Entries.Count > 0)
                    {
                        var currentIndex = request.PrevLogIndex + 1;
                        foreach (var entry in request.Entries)
                        {
                            if (this.Logs.Count < currentIndex)
                            {
                                this.Logs.Add(entry);
                            }
                            else if (this.Logs[currentIndex - 1].Term != entry.Term)
                            {
                                this.Logs.RemoveRange(currentIndex - 1, this.Logs.Count - (currentIndex - 1));
                                this.Logs.Add(entry);
                            }

                            currentIndex++;
                        }
                    }

                    if (request.LeaderCommit > this.CommitIndex &&
                        this.Logs.Count < request.LeaderCommit)
                    {
                        this.CommitIndex = this.Logs.Count;
                    }
                    else if (request.LeaderCommit > this.CommitIndex)
                    {
                        this.CommitIndex = request.LeaderCommit;
                    }

                    if (this.CommitIndex > this.LastApplied)
                    {
                        this.LastApplied++;
                    }

                    this.LeaderId = request.LeaderId;
                    this.Send(request.LeaderId, new AppendEntriesResponse(this.CurrentTerm,
                        true, this.Id, request.ReceiverEndpoint));
                }
            }
        }

        int GetPreviousLogIndex()
        {
            var logIndex = 0;
            if (this.Logs.Count > 0)
            {
                logIndex = this.Logs.Count - 1;
            }

            return logIndex;
        }

        int GetLogTermForIndex(int logIndex)
        {
            var logTerm = 0;
            if (logIndex > 0)
            {
                logTerm = this.Logs[logIndex - 1].Term;
            }

            return logTerm;
        }

        #endregion
    }
}
