﻿//-----------------------------------------------------------------------
// <copyright file="SEMOneMachine33Test.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
//      EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
//      OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
// ----------------------------------------------------------------------------------
//      The example companies, organizations, products, domain names,
//      e-mail addresses, logos, people, places, and events depicted
//      herein are fictitious.  No association with any real company,
//      organization, product, domain name, email address, logo, person,
//      places, or events is intended or should be inferred.
// </copyright>
//-----------------------------------------------------------------------

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.PSharp.LanguageServices;
using Microsoft.PSharp.LanguageServices.Parsing;
using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp.DynamicAnalysis.Tests.Unit
{
    [TestClass]
    public class SEMOneMachine33Test : BasePSharpTest
    {
        [TestMethod]
        public void TestSEMOneMachine33()
        {
            var test = @"
using System;
using System.Collections.Generic;
using Microsoft.PSharp;

namespace SystematicTesting
{
    class Config : Event {
        public List<int> List;
        public int V;
        public Config(List<int> l, int v) : base(-1, -1) { this.List = l; this.V = v; }
    }

    class Unit : Event { }

    class SeqPayload : Event {
       public List<int> List;
        public SeqPayload(List<int> l) : base(-1, -1) { this.List = l; }
    }

    class Entry : Machine
    {
        List<int> l;
        int i;
        MachineId mac;
        Tuple<List<int>, int> t;

        [Start]
        [OnEntry(nameof(EntryInit))]
        class Init : MachineState { }

        void EntryInit()
        {
            l = new List<int>();
			l.Insert(0, 12);
			l.Insert(0, 23);
			l.Insert(0, 12);
			l.Insert(0, 23);
			l.Insert(0, 12);
			l.Insert(0, 23);
			mac = this.CreateMachine(typeof(Tester));
            this.Send(mac, new Config(l, 1));
			this.Send(mac, new SeqPayload(l));
        }
    }

    class Tester : Machine
    {
        List<int> ii;
        List<int> rec;
        int i;

        [Start]
        [OnEntry(nameof(EntryInit))]
        [OnEventDoAction(typeof(Config), nameof(Configure))]
        [OnEventGotoState(typeof(SeqPayload), typeof(TestItNow))]
        class Init : MachineState { }

        void EntryInit()
        {
            ii = new List<int>();
            rec = new List<int>();
        }

        void Configure()
        {
            ii = (this.ReceivedEvent as Config).List;
            this.Assert(ii[0] == 23);
            this.Assert((this.ReceivedEvent as Config).V == 1);
        }

        [OnEntry(nameof(EntryTestItNow))]
        class TestItNow : MachineState { }

        void EntryTestItNow()
        {
            rec = (this.ReceivedEvent as SeqPayload).List;
			i = rec.Count - 1;
			while (i >= 0)
			{
				this.Assert(rec[i] == ii[i]);
				i = i - 1;
			}
        }
    }

    public static class TestProgram
    {
        public static void Main(string[] args)
        {
            TestProgram.Execute();
            Console.ReadLine();
        }

        [Test]
        public static void Execute()
        {
            PSharpRuntime.CreateMachine(typeof(Entry));
        }
    }
}";

            var parser = new CSharpParser(new PSharpProject(),
                SyntaxFactory.ParseSyntaxTree(test), true);
            var program = parser.Parse();
            program.Rewrite();

            var sctConfig = Configuration.Create();
            sctConfig.SuppressTrace = true;
            sctConfig.Verbose = 2;
            sctConfig.SchedulingStrategy = SchedulingStrategy.DFS;
            sctConfig.SchedulingIterations = 5;

            var assembly = base.GetAssembly(program.GetSyntaxTree());
            var context = AnalysisContext.Create(sctConfig, assembly);
            var sctEngine = SCTEngine.Create(context).Run();

            Assert.AreEqual(0, sctEngine.NumOfFoundBugs);
        }
    }
}
