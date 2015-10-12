﻿//-----------------------------------------------------------------------
// <copyright file="SEMTwoMachines15Test.cs" company="Microsoft">
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
    public class SEMTwoMachines15Test : BasePSharpTest
    {
        [TestMethod]
        public void TestNewMonitor3()
        {
            var test = @"
using System;
using Microsoft.PSharp;

namespace SystematicTesting
{
    class Config : Event {
        public bool Value;
        public Config(bool v) : base(1, -1) { this.Value = v; }
    }

    class Real1 : Machine
    {
        bool test = false;

        [Start]
        [OnEntry(nameof(EntryInit))]
        class Init : MachineState { }

        void EntryInit()
        {
            this.CreateMonitor(typeof(M));
            this.Monitor<M>(new Config(test));
        }
    }

    class M : Monitor
    {
        [Start]
        [OnEntry(nameof(EntryX))]
        [OnEventDoAction(typeof(Config), nameof(Configure))]
        class X : MonitorState { }

        void EntryX() { }

        void Configure()
        {
            this.Assert((this.ReceivedEvent as Config).Value == false); // passes
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
            PSharpRuntime.CreateMachine(typeof(Real1));
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

            var assembly = base.GetAssembly(program.GetSyntaxTree());
            var context = AnalysisContext.Create(sctConfig, assembly);
            var sctEngine = SCTEngine.Create(context).Run();

            Assert.AreEqual(0, sctEngine.NumOfFoundBugs);
        }
    }
}
