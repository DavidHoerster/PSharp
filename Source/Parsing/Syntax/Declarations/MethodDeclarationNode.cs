﻿//-----------------------------------------------------------------------
// <copyright file="MethodDeclarationNode.cs">
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
    /// Method declaration node.
    /// </summary>
    internal sealed class MethodDeclarationNode : PSharpSyntaxNode
    {
        #region fields

        /// <summary>
        /// The modifier token.
        /// </summary>
        internal Token Modifier;

        /// <summary>
        /// The inheritance modifier token.
        /// </summary>
        internal Token InheritanceModifier;

        /// <summary>
        /// The type identifier.
        /// </summary>
        internal Token TypeIdentifier;

        /// <summary>
        /// The identifier token.
        /// </summary>
        internal Token Identifier;

        /// <summary>
        /// The left parenthesis token.
        /// </summary>
        internal Token LeftParenthesisToken;

        /// <summary>
        /// List of parameter tokens.
        /// </summary>
        internal List<Token> Parameters;

        /// <summary>
        /// The right parenthesis token.
        /// </summary>
        internal Token RightParenthesisToken;

        /// <summary>
        /// The semicolon token.
        /// </summary>
        internal Token SemicolonToken;

        /// <summary>
        /// The statement block.
        /// </summary>
        internal StatementBlockNode StatementBlock;

        #endregion

        #region internal API

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="isModel">Is a model</param>
        internal MethodDeclarationNode(bool isModel)
            : base(isModel)
        {
            this.Parameters = new List<Token>();
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
            var text = "";
            Token initToken = null;

            if (base.IsModel)
            {
                text += "[Model]\n";
            }

            if (this.Modifier != null)
            {
                initToken = this.Modifier;
                text += this.Modifier.TextUnit.Text;
                text += " ";
            }

            if (this.InheritanceModifier != null)
            {
                initToken = this.InheritanceModifier;
                text += this.InheritanceModifier.TextUnit.Text;
                text += " ";
            }

            text += this.TypeIdentifier.TextUnit.Text;
            text += " ";

            text += this.Identifier.TextUnit.Text;

            text += this.LeftParenthesisToken.TextUnit.Text;

            for (int idx = 0; idx < this.Parameters.Count; idx++)
            {
                text += this.Parameters[idx].TextUnit.Text;

                if (idx < this.Parameters.Count - 1)
                {
                    text += " ";
                }
            }

            text += this.RightParenthesisToken.TextUnit.Text;

            if (this.StatementBlock != null)
            {
                this.StatementBlock.Rewrite(program);
                text += StatementBlock.GetRewrittenText();
            }
            else
            {
                text += this.SemicolonToken.TextUnit.Text + "\n";
            }

            if (initToken != null)
            {
                base.TextUnit = new TextUnit(text, initToken.TextUnit.Line);
            }
            else
            {
                base.TextUnit = new TextUnit(text, this.TypeIdentifier.TextUnit.Line);
            }
        }

        #endregion
    }
}
