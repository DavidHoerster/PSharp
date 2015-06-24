﻿//-----------------------------------------------------------------------
// <copyright file="BlockSyntax.cs">
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
    /// Block syntax node.
    /// </summary>
    internal sealed class BlockSyntax : PSharpSyntaxNode
    {
        #region fields

        /// <summary>
        /// The machine parent node.
        /// </summary>
        internal readonly MachineDeclarationNode Machine;

        /// <summary>
        /// The state parent node.
        /// </summary>
        internal readonly StateDeclarationNode State;

        /// <summary>
        /// The open brace token.
        /// </summary>
        internal Token OpenBraceToken;

        /// <summary>
        /// List of statement nodes.
        /// </summary>
        internal List<StatementNode> Statements;

        /// <summary>
        /// The close brace token.
        /// </summary>
        internal Token CloseBraceToken;

        #endregion

        #region internal API

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="program">Program</param>
        /// <param name="machineNode">MachineDeclarationNode</param>
        /// <param name="stateNode">StateDeclarationNode</param>
        /// <param name="isModel">Is a model</param>
        internal BlockSyntax(IPSharpProgram program, MachineDeclarationNode machineNode,
            StateDeclarationNode stateNode, bool isModel)
            : base(program, isModel)
        {
            this.Machine = machineNode;
            this.State = stateNode;
            this.Statements = new List<StatementNode>();
        }

        /// <summary>
        /// Rewrites the syntax node declaration to the intermediate C#
        /// representation.
        /// </summary>
        /// <param name="program">Program</param>
        internal override void Rewrite()
        {
            foreach (var stmt in this.Statements)
            {
                stmt.Rewrite();
            }

            var text = this.GetRewrittenStatementBlock();

            if (this.OpenBraceToken != null)
            {
                base.TextUnit = new TextUnit(text, this.OpenBraceToken.TextUnit.Line);
            }
            else
            {
                base.TextUnit = new TextUnit(text, this.Statements.First().TextUnit.Line);
            }
        }

        /// <summary>
        /// Rewrites the syntax node declaration to the intermediate C#
        /// representation using any given program models.
        /// </summary>
        internal override void Model()
        {
            foreach (var stmt in this.Statements)
            {
                stmt.Model();
            }

            var text = this.GetRewrittenStatementBlock();

            if (this.OpenBraceToken != null)
            {
                base.TextUnit = new TextUnit(text, this.OpenBraceToken.TextUnit.Line);
            }
            else
            {
                base.TextUnit = new TextUnit(text, this.Statements.First().TextUnit.Line);
            }
        }

        #endregion

        #region private API

        /// <summary>
        /// Returns the rewritten statement block.
        /// </summary>
        /// <returns>Text</returns>
        private string GetRewrittenStatementBlock()
        {
            var text = "\n";

            if (this.OpenBraceToken != null &&
                this.CloseBraceToken != null)
            {
                text += this.OpenBraceToken.TextUnit.Text + "\n";
            }

            foreach (var stmt in this.Statements)
            {
                text += stmt.TextUnit.Text;
            }

            if (this.OpenBraceToken != null &&
                this.CloseBraceToken != null)
            {
                text += this.CloseBraceToken.TextUnit.Text + "\n";
            }

            return text;
        }

        #endregion
    }
}
