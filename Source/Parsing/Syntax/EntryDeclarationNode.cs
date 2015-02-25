﻿//-----------------------------------------------------------------------
// <copyright file="EntryDeclarationNode.cs">
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
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PSharp.Parsing.Syntax
{
    /// <summary>
    /// Entry declaration node.
    /// </summary>
    public sealed class EntryDeclarationNode : BaseActionDeclarationNode
    {
        #region fields

        /// <summary>
        /// The entry keyword.
        /// </summary>
        public Token EntryKeyword;

        #endregion

        #region public API

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="machineNode">MachineDeclarationNode</param>
        /// <param name="stateNode">StateDeclarationNode</param>
        public EntryDeclarationNode(MachineDeclarationNode machineNode, StateDeclarationNode stateNode)
            : base(machineNode, stateNode)
        {

        }

        /// <summary>
        /// Returns the full text.
        /// </summary>
        /// <returns>string</returns>
        public override string GetFullText()
        {
            return base.TextUnit.Text;
        }

        /// <summary>
        /// Returns the rewritten text.
        /// </summary>
        /// <returns>string</returns>
        public override string GetRewrittenText()
        {
            return base.RewrittenTextUnit.Text;
        }


        #endregion

        #region internal API

        /// <summary>
        /// Rewrites the syntax node declaration to the intermediate C#
        /// representation.
        /// </summary>
        /// <param name="position">Position</param>
        internal override void Rewrite(ref int position)
        {
            var start = position;
            var text = "";

            var protectedKeyword = "protected";
            var protectedTextUnit = new TextUnit(protectedKeyword, protectedKeyword.Length, text.Length);
            base.RewrittenTokens.Add(new Token(protectedTextUnit, this.EntryKeyword.Line, TokenType.Protected));
            text += protectedKeyword;
            text += " ";

            var overrideKeyword = "override";
            var overrideTextUnit = new TextUnit(overrideKeyword, overrideKeyword.Length, text.Length);
            base.RewrittenTokens.Add(new Token(overrideTextUnit, this.EntryKeyword.Line, TokenType.Override));
            text += overrideKeyword;
            text += " ";

            var voidKeyword = "void";
            var voidTextUnit = new TextUnit(voidKeyword, voidKeyword.Length, text.Length);
            base.RewrittenTokens.Add(new Token(voidTextUnit, this.EntryKeyword.Line, TokenType.TypeIdentifier));
            text += voidKeyword;
            text += " ";

            var onEntryKeyword = "OnEntry";
            var onEntryTextUnit = new TextUnit(onEntryKeyword, onEntryKeyword.Length, text.Length);
            base.RewrittenTokens.Add(new Token(onEntryTextUnit, this.EntryKeyword.Line, TokenType.Identifier));
            text += onEntryKeyword;

            var leftParenthesis = "(";
            var leftParenthesisTextUnit = new TextUnit(leftParenthesis, leftParenthesis.Length, text.Length);
            base.RewrittenTokens.Add(new Token(leftParenthesisTextUnit, this.EntryKeyword.Line, TokenType.LeftParenthesis));
            text += leftParenthesis;

            var rightParenthesis = ")";
            var rightParenthesisTextUnit = new TextUnit(rightParenthesis, rightParenthesis.Length, text.Length);
            base.RewrittenTokens.Add(new Token(rightParenthesisTextUnit, this.EntryKeyword.Line, TokenType.RightParenthesis));
            text += rightParenthesis;

            text += "\n" + base.LeftCurlyBracketToken.TextUnit.Text + "\n";
            base.RewrittenTokens.Add(this.LeftCurlyBracketToken);

            foreach (var stmt in base.RewriteStatements())
            {
                text += stmt.Text;//.TextUnit.Text;
                base.RewrittenTokens.Add(stmt);
            }

            text += base.RightCurlyBracketToken.TextUnit.Text + "\n";
            base.RewrittenTokens.Add(this.RightCurlyBracketToken);

            base.RewrittenTextUnit = new TextUnit(text, text.Length, start);
            position = base.RewrittenTextUnit.End + 1;
        }

        /// <summary>
        /// Generates a new text unit.
        /// </summary>
        internal override void GenerateTextUnit()
        {
            var text = this.EntryKeyword.TextUnit.Text;

            text += "\n" + base.LeftCurlyBracketToken.TextUnit.Text + "\n";

            foreach (var stmt in base.Statements)
            {
                text += stmt.TextUnit.Text;
            }

            text += base.RightCurlyBracketToken.TextUnit.Text + "\n";

            int length = base.RightCurlyBracketToken.TextUnit.End - this.EntryKeyword.TextUnit.Start + 1;

            base.TextUnit = new TextUnit(text, length, this.EntryKeyword.TextUnit.Start);
        }

        #endregion
    }
}
