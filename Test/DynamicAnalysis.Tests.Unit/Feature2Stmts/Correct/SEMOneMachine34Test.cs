﻿//-----------------------------------------------------------------------
// <copyright file="SEMOneMachine34Test.cs" company="Microsoft">
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.PSharp.LanguageServices;
using Microsoft.PSharp.LanguageServices.Parsing;
using Microsoft.PSharp.Tooling;

namespace Microsoft.PSharp.DynamicAnalysis.Tests.Unit
{
    [TestClass]
    public class SEMOneMachine34Test
    {
        #region tests

        [TestMethod]
        public void TestSEMOneMachine34()
        {
            var test = @"
using System;
using System.Collections.Generic;
using Microsoft.PSharp;

namespace SystematicTesting
{
    class E1 : Event { }
    class E2 : Event { }
    class E3 : Event { }
    class E4 : Event { }

    class MachOS : Machine
    {
        int Int;
        bool Bool;
        MachineId mach;
        Dictionary<int, int> m;
        List<bool> s;

        [Start]
        [OnEntry(nameof(EntryInit))]
        [OnEventDoAction(typeof(E1), nameof(Foo1))]
        [OnEventDoAction(typeof(E2), nameof(Foo2))]
        [OnEventDoAction(typeof(E3), nameof(Foo3))]
        [OnEventDoAction(typeof(E4), nameof(Foo4))]
        class Init : MachineState { }

        void EntryInit()
        {
            m = new Dictionary<int, int>();
            s = new List<bool>();
            m.Add(0, 1);
            m.Add(1, 2);
			s.Add(true);
			s.Add(false);
			s.Add(true);
			this.Send(this.Id, new E1(), Tuple.Create(1, true));
			this.Send(this.Id, new E2(), 0, false);
            this.Send(this.Id, new E3(), 1);
			this.Send(this.Id, new E4(), Tuple.Create(m, s));

        }

        void Foo1()
        {
            Int = (int)(this.Payload as Tuple<int, bool>).Item1;
            this.Assert(Int == 1);
            Bool = (bool)(this.Payload as Tuple<int, bool>).Item2;
            this.Assert(Bool == true);
        }

        void Foo2()
        {
            Int = (int)(this.Payload as object[])[0];
            this.Assert(Int == 0);
            Bool = (bool)(this.Payload as object[])[1];
            this.Assert(Bool == false);
        }

        void Foo3()
        {
            Int = (int)this.Payload;
            this.Assert(Int == 1);
        }

        void Foo4()
        {
            Int = ((this.Payload as Tuple<Dictionary<int, int>, List<bool>>).Item1 as Dictionary<int, int>)[0];
            this.Assert(Int == 1);
            Bool = ((this.Payload as Tuple<Dictionary<int, int>, List<bool>>).Item2 as List<bool>)[2];
            this.Assert(Bool == true);
        }
    }

    public static class TestProgram
    {
        public static void Main(string[] args)
        {
            TestProgram.Execute();
            Console.ReadLine();
        }

        [EntryPoint]
        public static void Execute()
        {
            PSharpRuntime.CreateMachine(typeof(MachOS));
        }
    }
}";

            var parser = new CSharpParser(new PSharpProject(), SyntaxFactory.ParseSyntaxTree(test), true);
            var program = parser.Parse();
            program.Rewrite();

            Configuration.Verbose = 2;
            Configuration.SchedulingIterations = 100;
            Configuration.SchedulingStrategy = "dfs";

            var assembly = this.GetAssembly(program.GetSyntaxTree());
            AnalysisContext.Create(assembly);

            SCTEngine.Setup();
            SCTEngine.Run();

            Assert.AreEqual(0, SCTEngine.NumOfFoundBugs);
        }

        #endregion

        #region helper methods

        /// <summary>
        /// Get assembly from the given text.
        /// </summary>
        /// <param name="tree">SyntaxTree</param>
        /// <returns>Assembly</returns>
        private Assembly GetAssembly(SyntaxTree tree)
        {
            Assembly assembly = null;
            
            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Machine).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(BugFindingDispatcher).Assembly.Location)
            };

            var compilation = CSharpCompilation.Create(
                "PSharpTestAssembly", new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);
                if (result.Success)
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    assembly = Assembly.Load(ms.ToArray());
                }
            }

            return assembly;
        }

        #endregion
    }
}
