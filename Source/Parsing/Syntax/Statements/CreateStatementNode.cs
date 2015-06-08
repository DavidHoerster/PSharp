﻿//-----------------------------------------------------------------------
// <copyright file="CreateStatementNode.cs">
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
    /// Create statement node.
    /// </summary>
    internal sealed class CreateStatementNode : StatementNode
    {
        #region fields

        /// <summary>
        /// The create keyword.
        /// </summary>
        internal Token CreateKeyword;

        /// <summary>
        /// The machine identifier.
        /// </summary>
        internal List<Token> MachineIdentifier;

        /// <summary>
        /// The left parenthesis token.
        /// </summary>
        internal Token LeftParenthesisToken;

        /// <summary>
        /// The machine creation payload.
        /// </summary>
        internal ExpressionNode Payload;

        /// <summary>
        /// The right parenthesis token.
        /// </summary>
        internal Token RightParenthesisToken;

        /// <summary>
        /// The for keyword.
        /// </summary>
        internal Token ForKeyword;

        /// <summary>
        /// The real machine identifier.
        /// </summary>
        internal List<Token> RealMachineIdentifier;

        #endregion

        #region internal API

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="program">Program</param>
        /// <param name="node">Node</param>
        internal CreateStatementNode(IPSharpProgram program, StatementBlockNode node)
            : base(program, node)
        {
            this.MachineIdentifier = new List<Token>();
            this.RealMachineIdentifier = new List<Token>();
        }

        /// <summary>
        /// Rewrites the syntax node declaration to the intermediate C#
        /// representation.
        /// </summary>
        /// <param name="program">Program</param>
        internal override void Rewrite()
        {
            var machineId = "";

            if (this.ForKeyword != null)
            {
                foreach (var id in this.RealMachineIdentifier)
                {
                    machineId += id.TextUnit.Text;
                }
            }
            else
            {
                foreach (var id in this.MachineIdentifier)
                {
                    machineId += id.TextUnit.Text;
                }
            }
            
            var isMonitor = this.IsMonitor(machineId);

            if (isMonitor)
            {
                if (this.ForKeyword != null)
                {
                    throw new RewritingException("A monitor cannot model a real machine.");
                }

                base.TextUnit = new TextUnit("", 0);
                return;
            }

            this.Payload.Rewrite();

            var text = this.GetRewrittenCreateStatement(machineId, isMonitor);

            base.TextUnit = new TextUnit(text, this.CreateKeyword.TextUnit.Line);
        }

        /// <summary>
        /// Rewrites the syntax node declaration to the intermediate C#
        /// representation using any given program models.
        /// </summary>
        internal override void Model()
        {
            var machineId = "";
            foreach (var id in this.MachineIdentifier)
            {
                machineId += id.TextUnit.Text;
            }

            var isMonitor = this.IsMonitor(machineId);

            if (isMonitor && this.ForKeyword != null)
            {
                throw new RewritingException("A monitor cannot model a real machine.");
            }

            this.Payload.Model();

            var text = this.GetRewrittenCreateStatement(machineId, isMonitor);

            base.TextUnit = new TextUnit(text, this.CreateKeyword.TextUnit.Line);
        }

        #endregion

        #region private API

        /// <summary>
        /// Returns the rewritten create statement.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="isMonitor">Is monitor</param>
        /// <returns>Text</returns>
        private string GetRewrittenCreateStatement(string machine, bool isMonitor)
        {
            var text = "this.";

            if (isMonitor)
            {
                text += "CreateMonitor<";
            }
            else
            {
                text += "Create<";
            }

            text += machine;

            text += ">(";

            text += this.Payload.TextUnit.Text;

            text += ")";

            text += this.SemicolonToken.TextUnit.Text + "\n";

            return text;
        }

        /// <summary>
        /// True if the given machine is a monitor.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <returns>Boolean</returns>
        private bool IsMonitor(string machine)
        {
            bool isMonitor = false;
            if (this.Program is PSharpProgram)
            {
                var project = (this.Program as PSharpProgram).Project;

                isMonitor = project.PSharpPrograms.Any(psp => psp.NamespaceDeclarations.Any(
                    ns => ns.MachineDeclarations.Any(md => md.IsMonitor && md.Identifier.
                    TextUnit.Text.Equals(machine))));

                if (!isMonitor)
                {
                    isMonitor = project.PPrograms.Any(ns => ns.MachineDeclarations.Any(
                        md => md.IsMonitor && md.Identifier.TextUnit.Text.Equals(machine)));
                }
            }
            else
            {
                var pProgram = this.Program as PProgram;
                isMonitor = pProgram.MachineDeclarations.Any(md =>
                    md.IsMonitor && md.Identifier.TextUnit.Text.Equals(machine));
            }

            return isMonitor;
        }

        #endregion
    }
}
