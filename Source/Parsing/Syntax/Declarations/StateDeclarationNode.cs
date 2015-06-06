﻿//-----------------------------------------------------------------------
// <copyright file="StateDeclarationNode.cs">
//      Copyright (c) 2015 Pantazis Deligiannis (p.deligiannis@imperial.ac.uk)
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
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PSharp.Parsing.Syntax
{
    /// <summary>
    /// State declaration node.
    /// </summary>
    internal sealed class StateDeclarationNode : PSharpSyntaxNode
    {
        #region fields

        /// <summary>
        /// True if the state is the initial state.
        /// </summary>
        internal readonly bool IsInitial;

        /// <summary>
        /// The machine parent node.
        /// </summary>
        internal readonly MachineDeclarationNode Machine;

        /// <summary>
        /// The state keyword.
        /// </summary>
        internal Token StateKeyword;

        /// <summary>
        /// The modifier token.
        /// </summary>
        internal Token Modifier;

        /// <summary>
        /// The identifier token.
        /// </summary>
        internal Token Identifier;

        /// <summary>
        /// The left curly bracket token.
        /// </summary>
        internal Token LeftCurlyBracketToken;

        /// <summary>
        /// Entry declaration.
        /// </summary>
        internal EntryDeclarationNode EntryDeclaration;

        /// <summary>
        /// Exit declaration.
        /// </summary>
        internal ExitDeclarationNode ExitDeclaration;

        /// <summary>
        /// Dictionary containing goto state transitions.
        /// </summary>
        internal Dictionary<Token, Token> GotoStateTransitions;

        /// <summary>
        /// Dictionary containing push state transitions.
        /// </summary>
        internal Dictionary<Token, Token> PushStateTransitions;

        /// <summary>
        /// Dictionary containing actions bindings.
        /// </summary>
        internal Dictionary<Token, Token> ActionBindings;

        /// <summary>
        /// Dictionary containing transitions on exit actions.
        /// </summary>
        internal Dictionary<Token, StatementBlockNode> TransitionsOnExitActions;

        /// <summary>
        /// Dictionary containing actions handlers.
        /// </summary>
        internal Dictionary<Token, StatementBlockNode> ActionHandlers;

        /// <summary>
        /// Set of deferred events.
        /// </summary>
        internal HashSet<Token> DeferredEvents;

        /// <summary>
        /// Set of ignored events.
        /// </summary>
        internal HashSet<Token> IgnoredEvents;

        /// <summary>
        /// The right curly bracket token.
        /// </summary>
        internal Token RightCurlyBracketToken;

        #endregion

        #region internal API

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="machineNode">PMachineDeclarationNode</param>
        /// <param name="isInit">Is initial state</param>
        /// <param name="isModel">Is a model</param>
        internal StateDeclarationNode(MachineDeclarationNode machineNode, bool isInit, bool isModel)
            : base(isModel)
        {
            this.IsInitial = isInit;
            this.Machine = machineNode;
            this.GotoStateTransitions = new Dictionary<Token, Token>();
            this.PushStateTransitions = new Dictionary<Token, Token>();
            this.ActionBindings = new Dictionary<Token, Token>();
            this.TransitionsOnExitActions = new Dictionary<Token, StatementBlockNode>();
            this.ActionHandlers = new Dictionary<Token, StatementBlockNode>();
            this.DeferredEvents = new HashSet<Token>();
            this.IgnoredEvents = new HashSet<Token>();
        }

        /// <summary>
        /// Adds a goto state transition.
        /// </summary>
        /// <param name="eventIdentifier">Token</param>
        /// <param name="stateIdentifier">Token</param>
        /// <param name="stmtBlock">Statement block</param>
        /// <returns>Boolean value</returns>
        internal bool AddGotoStateTransition(Token eventIdentifier, Token stateIdentifier, StatementBlockNode stmtBlock = null)
        {
            if (this.GotoStateTransitions.ContainsKey(eventIdentifier) ||
                this.PushStateTransitions.ContainsKey(eventIdentifier) ||
                this.ActionBindings.ContainsKey(eventIdentifier))
            {
                return false;
            }

            this.GotoStateTransitions.Add(eventIdentifier, stateIdentifier);
            if (stmtBlock != null)
            {
                this.TransitionsOnExitActions.Add(eventIdentifier, stmtBlock);
            }

            return true;
        }

        /// <summary>
        /// Adds a push state transition.
        /// </summary>
        /// <param name="eventIdentifier">Token</param>
        /// <param name="stateIdentifier">Token</param>
        /// <returns>Boolean value</returns>
        internal bool AddPushStateTransition(Token eventIdentifier, Token stateIdentifier)
        {
            if (this.Machine.IsMonitor)
            {
                return false;
            }

            if (this.GotoStateTransitions.ContainsKey(eventIdentifier) ||
                this.PushStateTransitions.ContainsKey(eventIdentifier) ||
                this.ActionBindings.ContainsKey(eventIdentifier))
            {
                return false;
            }

            this.PushStateTransitions.Add(eventIdentifier, stateIdentifier);

            return true;
        }

        /// <summary>
        /// Adds an action binding.
        /// </summary>
        /// <param name="eventIdentifier">Token</param>
        /// <param name="stateIdentifier">Token</param>
        /// <returns>Boolean value</returns>
        internal bool AddActionBinding(Token eventIdentifier, StatementBlockNode stmtBlock)
        {
            if (this.GotoStateTransitions.ContainsKey(eventIdentifier) ||
                this.PushStateTransitions.ContainsKey(eventIdentifier) ||
                this.ActionBindings.ContainsKey(eventIdentifier))
            {
                return false;
            }

            this.ActionBindings.Add(eventIdentifier, null);
            this.ActionHandlers.Add(eventIdentifier, stmtBlock);

            return true;
        }

        /// <summary>
        /// Adds an action binding.
        /// </summary>
        /// <param name="eventIdentifier">Token</param>
        /// <param name="actionIdentifier">Token</param>
        /// <returns>Boolean value</returns>
        internal bool AddActionBinding(Token eventIdentifier, Token actionIdentifier)
        {
            if (this.GotoStateTransitions.ContainsKey(eventIdentifier) ||
                this.PushStateTransitions.ContainsKey(eventIdentifier) ||
                this.ActionBindings.ContainsKey(eventIdentifier))
            {
                return false;
            }

            this.ActionBindings.Add(eventIdentifier, actionIdentifier);

            return true;
        }

        /// <summary>
        /// Adds a deferred event.
        /// </summary>
        /// <param name="eventIdentifier">Token</param>
        /// <returns>Boolean value</returns>
        internal bool AddDeferredEvent(Token eventIdentifier)
        {
            if (this.Machine.IsMonitor)
            {
                return false;
            }

            if (this.DeferredEvents.Contains(eventIdentifier) ||
                this.IgnoredEvents.Contains(eventIdentifier))
            {
                return false;
            }

            this.DeferredEvents.Add(eventIdentifier);

            return true;
        }

        /// <summary>
        /// Adds an ignored event.
        /// </summary>
        /// <param name="eventIdentifier">Token</param>
        /// <returns>Boolean value</returns>
        internal bool AddIgnoredEvent(Token eventIdentifier)
        {
            if (this.DeferredEvents.Contains(eventIdentifier) ||
                this.IgnoredEvents.Contains(eventIdentifier))
            {
                return false;
            }

            this.IgnoredEvents.Add(eventIdentifier);

            return true;
        }

        /// <summary>
        /// Returns the rewritten text.
        /// </summary>
        /// <returns>string</returns>
        internal override string GetRewrittenText()
        {
            return base.TextUnit.Text;
        }

        /// <summary>
        /// Rewrites the syntax node declaration to the intermediate C#
        /// representation.
        /// </summary>
        /// <param name="program">Program</param>
        internal override void Rewrite(IPSharpProgram program)
        {
            if (this.EntryDeclaration != null)
            {
                this.EntryDeclaration.Rewrite(program);
            }

            if (this.ExitDeclaration != null)
            {
                this.ExitDeclaration.Rewrite(program);
            }

            var text = "";
            var initToken = this.StateKeyword;

            if (this.IsInitial)
            {
                text += "[Initial]\n";
            }

            if (this.Modifier != null)
            {
                initToken = this.Modifier;
                text += this.Modifier.TextUnit.Text;
                text += " ";
            }

            if (!this.Machine.IsMonitor)
            {
                text += "class " + this.Identifier.TextUnit.Text + " : MachineState";
            }
            else
            {
                text += "class " + this.Identifier.TextUnit.Text + " : MonitorState";
            }

            text += "\n" + this.LeftCurlyBracketToken.TextUnit.Text + "\n";

            if (this.EntryDeclaration != null)
            {
                text += this.EntryDeclaration.GetRewrittenText();
            }

            if (this.ExitDeclaration != null)
            {
                text += this.ExitDeclaration.GetRewrittenText();
            }

            if (!this.Machine.IsMonitor)
            {
                text += this.InstrumentDeferredEvents();
            }

            text += this.InstrumentIgnoredEvents();

            text += this.RightCurlyBracketToken.TextUnit.Text + "\n";

            base.TextUnit = new TextUnit(text, initToken.TextUnit.Line);
        }

        #endregion

        #region private API

        /// <summary>
        /// Instruments the deferred events.
        /// </summary>
        /// <returns>Text</returns>
        private string InstrumentDeferredEvents()
        {
            if (this.DeferredEvents.Count == 0)
            {
                return "";
            }

            var text = "\n";
            text += " protected override System.Collections.Generic.HashSet<Type> DefineDeferredEvents()\n";
            text += " {\n";
            text += "  return new System.Collections.Generic.HashSet<Type>\n";
            text += "  {\n";

            var eventIds = this.DeferredEvents.ToList();
            for (int idx = 0; idx < eventIds.Count; idx++)
            {
                if (eventIds[idx].Type == TokenType.HaltEvent)
                {
                    text += "   typeof(Microsoft.PSharp.Halt)";
                }
                else if (eventIds[idx].Type == TokenType.DefaultEvent)
                {
                    text += "   typeof(Microsoft.PSharp.Default)";
                }
                else
                {
                    text += "   typeof(" + eventIds[idx].TextUnit.Text + ")";
                }

                if (idx < eventIds.Count - 1)
                {
                    text += ",\n";
                }
                else
                {
                    text += "\n";
                }
            }

            text += "  };\n";
            text += " }\n";

            return text;
        }

        /// <summary>
        /// Instruments the ignored events.
        /// </summary>
        /// <returns>Text</returns>
        private string InstrumentIgnoredEvents()
        {
            if (this.IgnoredEvents.Count == 0)
            {
                return "";
            }

            var text = "\n";
            text += " protected override System.Collections.Generic.HashSet<Type> DefineIgnoredEvents()\n";
            text += " {\n";
            text += "  return new System.Collections.Generic.HashSet<Type>\n";
            text += "  {\n";

            var eventIds = this.IgnoredEvents.ToList();
            for (int idx = 0; idx < eventIds.Count; idx++)
            {
                if (eventIds[idx].Type == TokenType.HaltEvent)
                {
                    text += "   typeof(Microsoft.PSharp.Halt)";
                }
                else if (eventIds[idx].Type == TokenType.DefaultEvent)
                {
                    text += "   typeof(Microsoft.PSharp.Default)";
                }
                else
                {
                    text += "   typeof(" + eventIds[idx].TextUnit.Text + ")";
                }

                if (idx < eventIds.Count - 1)
                {
                    text += ",\n";
                }
                else
                {
                    text += "\n";
                }
            }

            text += "  };\n";
            text += " }\n";

            return text;
        }

        #endregion
    }
}
