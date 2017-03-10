﻿//-----------------------------------------------------------------------
// <copyright file="StateTests.cs">
//      Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//      EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//      MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
//      IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
//      CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
//      TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
//      SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
//-----------------------------------------------------------------------

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.PSharp.LanguageServices.Compilation;
using Microsoft.PSharp.LanguageServices.Parsing;
using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp.LanguageServices.Tests.Unit
{
    [TestClass]
    public class StateTests
    {
        [TestMethod, Timeout(10000)]
        public void TestStateDeclaration()
        {
            var test = @"
namespace Foo {
machine M {
start state S1 { }
state S2 { }
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        class S1 : MachineState
        {
        }

        class S2 : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestEntryDeclaration()
        {
            var test = @"
namespace Foo {
machine M {
start state S
{
entry{}
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEntry(nameof(psharp_S_on_entry_action))]
        class S : MachineState
        {
        }

        protected void psharp_S_on_entry_action()
        {}
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestExitDeclaration()
        {
            var test = @"
namespace Foo {
machine M {
start state S
{
exit{}
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnExit(nameof(psharp_S_on_exit_action))]
        class S : MachineState
        {
        }

        protected void psharp_S_on_exit_action()
        {}
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestEntryAndExitDeclaration()
        {
            var test = @"
namespace Foo {
machine M {
start state S
{
entry {}
exit {}
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEntry(nameof(psharp_S_on_entry_action))]
        [OnExit(nameof(psharp_S_on_exit_action))]
        class S : MachineState
        {
        }

        protected void psharp_S_on_entry_action()
        {}

        protected void psharp_S_on_exit_action()
        {}
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnEventGotoStateDeclaration()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on e goto S2;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventGotoState(typeof(e), typeof(S2))]
        class S1 : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnEventGotoStateDeclaration2()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on e1 goto S2;
on e2 goto S3;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventGotoState(typeof(e1), typeof(S2))]
        [OnEventGotoState(typeof(e2), typeof(S3))]
        class S1 : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnSimpleGenericEventGotoStateDeclaration()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on e<int> goto S2;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventGotoState(typeof(e<int>), typeof(S2))]
        class S1 : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnComplexGenericEventGotoStateDeclaration()
        {
            var test = @"
using System.Collections.Generic;
namespace Foo {
machine M {
start state S1
{
on e<List<Tuple<bool, object>>, Dictionary<string, float>> goto S2;
}
}
}";
            var expected = @"
using Microsoft.PSharp;
using System.Collections.Generic;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventGotoState(typeof(e<List<Tuple<bool,object>>,Dictionary<string,float>>), typeof(S2))]
        class S1 : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnQualifiedEventGotoStateDeclaration()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on Foo.e goto S2;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventGotoState(typeof(Foo.e), typeof(S2))]
        class S1 : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnEventGotoStateDeclarationWithBody()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on e goto S2 with {}
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventGotoState(typeof(e), typeof(S2), nameof(psharp_S1_e_action))]
        class S1 : MachineState
        {
        }

        protected void psharp_S1_e_action()
        {}
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnSimpleGenericEventGotoStateDeclarationWithBody()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on e<int> goto S2 with {}
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventGotoState(typeof(e<int>), typeof(S2), nameof(psharp_S1_e_type_0_action))]
        class S1 : MachineState
        {
        }

        protected void psharp_S1_e_type_0_action()
        {}
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnComplexGenericEventGotoStateDeclarationWithBody()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on e<List<Tuple<bool, object>>, Dictionary<string, float>> goto S2 with {}
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventGotoState(typeof(e<List<Tuple<bool,object>>,Dictionary<string,float>>), typeof(S2), nameof(psharp_S1_e_type_0_action))]
        class S1 : MachineState
        {
        }

        protected void psharp_S1_e_type_0_action()
        {}
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOMultipleGenericEventGotoStateDeclarationWithBody()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on e goto S2 with {}
on e<int> goto S2 with {}
on e<List<Tuple<bool, object>>, Dictionary<string, float>> goto S2 with {}
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventGotoState(typeof(e), typeof(S2), nameof(psharp_S1_e_action))]
        [OnEventGotoState(typeof(e<int>), typeof(S2), nameof(psharp_S1_e_type_1_action))]
        [OnEventGotoState(typeof(e<List<Tuple<bool,object>>,Dictionary<string,float>>), typeof(S2), nameof(psharp_S1_e_type_2_action))]
        class S1 : MachineState
        {
        }

        protected void psharp_S1_e_action()
        {}

        protected void psharp_S1_e_type_1_action()
        {}

        protected void psharp_S1_e_type_2_action()
        {}
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnEventDoActionDeclaration()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on e do Bar;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventDoAction(typeof(e), nameof(Bar))]
        class S1 : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnEventDoActionDeclaration2()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on e1 do Bar;
on e2 do Baz;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventDoAction(typeof(e1), nameof(Bar))]
        [OnEventDoAction(typeof(e2), nameof(Baz))]
        class S1 : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnSimpleGenericEventDoActionDeclaration()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on e<int> do Bar;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventDoAction(typeof(e<int>), nameof(Bar))]
        class S1 : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnComplexGenericEventDoActionDeclaration()
        {
            var test = @"
using System.Collection.Generic;
namespace Foo {
machine M {
start state S1
{
on e<List<Tuple<bool, object>>, Dictionary<string, float>> do Bar;
}
}
}";
            var expected = @"
using Microsoft.PSharp;
using System.Collection.Generic;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventDoAction(typeof(e<List<Tuple<bool,object>>,Dictionary<string,float>>), nameof(Bar))]
        class S1 : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnQualifiedEventDoActionDeclaration()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on Foo.e do Bar;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventDoAction(typeof(Foo.e), nameof(Bar))]
        class S1 : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnEventDoActionDeclarationWithBody()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on e do {}
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventDoAction(typeof(e), nameof(psharp_S1_e_action))]
        class S1 : MachineState
        {
        }

        protected void psharp_S1_e_action()
        {}
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestOnEventGotoStateAndDoActionDeclaration()
        {
            var test = @"
namespace Foo {
machine M {
start state S1
{
on e1 goto S2;
on e2 do Bar;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventGotoState(typeof(e1), typeof(S2))]
        [OnEventDoAction(typeof(e2), nameof(Bar))]
        class S1 : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestIgnoreEventDeclaration()
        {
            var test = @"
namespace Foo {
machine M {
start state S
{
ignore e;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [IgnoreEvents(typeof(e))]
        class S : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestIgnoreEventDeclaration2()
        {
            var test = @"
namespace Foo {
machine M {
start state S
{
ignore e1, e2;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [IgnoreEvents(typeof(e1), typeof(e2))]
        class S : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestIgnoreEventDeclarationQualifiedComplex()
        {
            var test = @"
namespace Foo {
machine M {
start state S
{
ignore e1<int>, Foo.e2<Bar.e3>;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [IgnoreEvents(typeof(e1<int>), typeof(Foo.e2<Bar.e3>))]
        class S : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestDeferEventDeclaration()
        {
            var test = @"
namespace Foo {
machine M {
start state S
{
defer e;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [DeferEvents(typeof(e))]
        class S : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestDeferEventDeclaration2()
        {
            var test = @"
namespace Foo {
machine M {
start state S
{
defer e1,e2;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [DeferEvents(typeof(e1), typeof(e2))]
        class S : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestDeferEventDeclarationQualifiedComplex()
        {
            var test = @"
namespace Foo {
machine M {
start state S
{
defer e1<int>, halt, default, Foo.e2<Bar.e3>;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [DeferEvents(typeof(e1<int>), typeof(Microsoft.PSharp.Halt), typeof(Microsoft.PSharp.Default), typeof(Foo.e2<Bar.e3>))]
        class S : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestDefaultEvent()
        {
            var test = @"
namespace Foo {
machine M {
start state S
{
on default goto S2;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventGotoState(typeof(Microsoft.PSharp.Default), typeof(S2))]
        class S : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestHaltEvent()
        {
            var test = @"
namespace Foo {
machine M {
start state S
{
on halt goto S2;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventGotoState(typeof(Microsoft.PSharp.Halt), typeof(S2))]
        class S : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestWildcardEventDefer()
        {
            var test = @"
namespace Foo {
machine M {
start state S
{
defer *;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [DeferEvents(typeof(Microsoft.PSharp.WildCardEvent))]
        class S : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

        [TestMethod, Timeout(10000)]
        public void TestWildcardEventAction()
        {
            var test = @"
namespace Foo {
machine M {
start state S
{
on *,e1.e2 goto S2;
on * push S2;
}
}
}";
            var expected = @"
using Microsoft.PSharp;

namespace Foo
{
    class M : Machine
    {
        [Microsoft.PSharp.Start]
        [OnEventGotoState(typeof(Microsoft.PSharp.WildCardEvent), typeof(S2))]
        [OnEventGotoState(typeof(e1.e2), typeof(S2))]
        [OnEventPushState(typeof(Microsoft.PSharp.WildCardEvent), typeof(S2))]
        class S : MachineState
        {
        }
    }
}";
            LanguageTestUtilities.AssertRewritten(expected, test);
        }

    }
}
