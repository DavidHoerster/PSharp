﻿//-----------------------------------------------------------------------
// <copyright file="PExpressionNode.cs">
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

namespace Microsoft.PSharp.Parsing.PSyntax
{
    /// <summary>
    /// Expression node.
    /// </summary>
    public class PExpressionNode : PSyntaxNode
    {
        #region fields

        /// <summary>
        /// The block node.
        /// </summary>
        public readonly PStatementBlockNode Parent;

        /// <summary>
        /// The statement tokens.
        /// </summary>
        public List<Token> StmtTokens;

        /// <summary>
        /// The rewritten statement tokens.
        /// </summary>
        protected List<Token> RewrittenStmtTokens;

        /// <summary>
        /// Received payloads.
        /// </summary>
        public List<PPayloadReceiveNode> Payloads;

        /// <summary>
        /// Pending received payloads.
        /// </summary>
        private List<PPayloadReceiveNode> PendingPayloads;

        /// <summary>
        /// The current index.
        /// </summary>
        private int Index;

        #endregion

        #region public API

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="node">Node</param>
        public PExpressionNode(PStatementBlockNode node)
            : base()
        {
            this.Parent = node;
            this.StmtTokens = new List<Token>();
            this.RewrittenStmtTokens = new List<Token>();
            this.Payloads = new List<PPayloadReceiveNode>();
            this.PendingPayloads = new List<PPayloadReceiveNode>();
        }

        /// <summary>
        /// Returns the full text.
        /// </summary>
        /// <returns>string</returns>
        public override string GetFullText()
        {
            if (this.StmtTokens.Count == 0)
            {
                return "";
            }
            
            return base.TextUnit.Text;
        }

        /// <summary>
        /// Returns the rewritten text.
        /// </summary>
        /// <returns>string</returns>
        public override string GetRewrittenText()
        {
            if (this.StmtTokens.Count == 0)
            {
                return "";
            }
            
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
            if (this.StmtTokens.Count == 0)
            {
                return;
            }
            
            this.Index = 0;
            this.RewrittenStmtTokens = this.StmtTokens.ToList();
            this.PendingPayloads = this.Payloads.ToList();
            this.RunSpecialisedRewrittingPass(ref position);
            this.RewriteNextToken(ref position);

            var start = position;
            var text = "";

            foreach (var token in this.RewrittenStmtTokens)
            {
                text += token.TextUnit.Text;
            }

            base.RewrittenTextUnit = new TextUnit(text,
                this.StmtTokens.First().TextUnit.Line, start);
            position = base.RewrittenTextUnit.End + 1;
        }

        /// <summary>
        /// Generates a new text unit.
        /// </summary>
        internal override void GenerateTextUnit()
        {
            if (this.StmtTokens.Count == 0)
            {
                return;
            }

            var text = "";

            foreach (var tok in this.StmtTokens)
            {
                if (tok == null)
                {
                    continue;
                }

                text += tok.TextUnit.Text;
            }

            base.TextUnit = new TextUnit(text, this.StmtTokens.First().TextUnit.Line,
                this.StmtTokens.First().TextUnit.Start);
        }

        #endregion

        #region protected API

        /// <summary>
        /// Can be overriden by child classes to implement specialised
        /// rewritting functionality.
        /// </summary>
        /// <param name="position">Position</param>
        protected virtual void RunSpecialisedRewrittingPass(ref int position)
        {

        }

        /// <summary>
        /// Rewrites the machine type.
        /// </summary>
        /// param name="position">Position</param>
        protected void RewriteMachineType(ref int position)
        {
            var textUnit = new TextUnit("Machine", this.RewrittenStmtTokens[this.Index].TextUnit.Line,
                this.RewrittenStmtTokens[this.Index].TextUnit.Start);
            this.RewrittenStmtTokens[this.Index] = new Token(textUnit, this.RewrittenStmtTokens[this.Index].Type);
        }

        /// <summary>
        /// Rewrites the this keyword.
        /// </summary>
        /// param name="position">Position</param>
        protected void RewriteThis(ref int position)
        {
            if (this.Parent.State == null)
            {
                return;
            }

            int line = this.RewrittenStmtTokens[this.Index].TextUnit.Line;
            var text = "this.Machine";
            this.RewrittenStmtTokens[this.Index] = new Token(new TextUnit(text, line, position));
            position += text.Length;
        }

        /// <summary>
        /// Rewrites the sizeof keyword.
        /// </summary>
        /// param name="position">Position</param>
        protected void RewriteSizeOf(ref int position)
        {
            var sizeOfIndex = this.Index;

            int line = this.RewrittenStmtTokens[this.Index].TextUnit.Line;
            var text = ".Count";

            this.RewrittenStmtTokens.RemoveAt(this.Index);
            if (this.RewrittenStmtTokens.Count == this.Index ||
                this.RewrittenStmtTokens[this.Index].Type != TokenType.LeftParenthesis)
            {
                return;
            }

            int leftParenIndex = this.Index;
            this.Index++;

            int counter = 1;
            while (this.Index < this.RewrittenStmtTokens.Count)
            {
                if (this.RewrittenStmtTokens[this.Index].Type == TokenType.LeftParenthesis)
                {
                    counter++;
                }
                else if (this.RewrittenStmtTokens[this.Index].Type == TokenType.RightParenthesis)
                {
                    counter--;
                }

                if (counter == 0)
                {
                    break;
                }

                this.Index++;
            }

            this.RewrittenStmtTokens.RemoveAt(this.Index);
            this.RewrittenStmtTokens.RemoveAt(leftParenIndex);

            this.RewrittenStmtTokens.Insert(this.Index - 1, new Token(new TextUnit(text, line, position)));
            position += text.Length;
            this.Index = sizeOfIndex - 1;
        }

        /// <summary>
        /// Rewrites the tuple assignment.
        /// </summary>
        /// param name="position">Position</param>
        protected void RewriteTupleAssignment(ref int position)
        {
            var assignIndex = this.Index;

            this.Index++;
            this.SkipWhiteSpaceTokens();
            if (this.Index == this.RewrittenStmtTokens.Count ||
                this.RewrittenStmtTokens[this.Index] == null ||
                this.RewrittenStmtTokens[this.Index].Type != TokenType.LeftParenthesis)
            {
                this.Index = assignIndex;
                return;
            }

            int leftParenIndex = this.Index;
            this.Index++;

            int counter = 1;
            while (this.Index < this.RewrittenStmtTokens.Count)
            {
                if (this.RewrittenStmtTokens[this.Index] != null &&
                    this.RewrittenStmtTokens[this.Index].Type == TokenType.Null)
                {
                    var nullStr = "default(Machine)";
                    var nullTextUnit = new TextUnit(nullStr, this.RewrittenStmtTokens[this.Index].TextUnit.Line,
                        this.RewrittenStmtTokens[this.Index].TextUnit.Start);
                    this.RewrittenStmtTokens[this.Index] = new Token(nullTextUnit);
                }

                if (this.RewrittenStmtTokens[this.Index] != null &&
                    this.RewrittenStmtTokens[this.Index].Type == TokenType.LeftParenthesis)
                {
                    counter++;
                }
                else if (this.RewrittenStmtTokens[this.Index] != null && 
                    this.RewrittenStmtTokens[this.Index].Type == TokenType.RightParenthesis)
                {
                    counter--;
                }

                if (counter == 0)
                {
                    break;
                }

                this.Index++;
            }

            if (counter > 0)
            {
                this.Index = assignIndex;
                return;
            }

            this.Index++;
            this.SkipWhiteSpaceTokens();
            if (this.Index < this.RewrittenStmtTokens.Count &&
                this.RewrittenStmtTokens[this.Index] != null &&
                this.RewrittenStmtTokens[this.Index].TextUnit.Text.StartsWith("."))
            {
                this.Index = assignIndex;
                return;
            }

            int line = this.RewrittenStmtTokens[leftParenIndex].TextUnit.Line;
            var tupleStr = "Container.Create(";
            var textUnit = new TextUnit(tupleStr, line,
                this.RewrittenStmtTokens[leftParenIndex].TextUnit.Start);
            this.RewrittenStmtTokens[leftParenIndex] = new Token(textUnit);
            this.Index = assignIndex;
        }

        /// <summary>
        /// Rewrites the member identifier.
        /// </summary>
        /// <param name="position">Position</param>
        protected void RewriteMemberIdentifier(ref int position)
        {
            if (this.Parent.Machine == null || this.Parent.State == null ||
                !(this.Parent.Machine.FieldDeclarations.Any(val => val.Identifier.TextUnit.Text.
                Equals(this.RewrittenStmtTokens[this.Index].TextUnit.Text)) ||
                this.Parent.Machine.FunctionDeclarations.Any(val => val.Identifier.TextUnit.Text.
                Equals(this.RewrittenStmtTokens[this.Index].TextUnit.Text))))
            {
                return;
            }

            int line = this.RewrittenStmtTokens[this.Index].TextUnit.Line;
            var text = "(this.Machine as " + this.Parent.Machine.Identifier.TextUnit.Text + ").";
            this.RewrittenStmtTokens.Insert(this.Index, new Token(new TextUnit(text, line, position)));
            position += text.Length;
            this.Index++;
        }

        /// <summary>
        /// Rewrites a tuple, recursively.
        /// </summary>
        /// <param name="index">Index</param>
        protected void RewriteTuple(ref int index)
        {
            var tupleIdx = index;
            index++;

            int tupleSize = 1;
            bool expectsComma = false;
            while (index < this.RewrittenStmtTokens.Count &&
                this.RewrittenStmtTokens[index].Type != TokenType.RightParenthesis)
            {
                if (!expectsComma &&
                    (this.RewrittenStmtTokens[index].Type != TokenType.Identifier &&
                    this.RewrittenStmtTokens[index].Type != TokenType.This &&
                    this.RewrittenStmtTokens[index].Type != TokenType.True &&
                    this.RewrittenStmtTokens[index].Type != TokenType.False &&
                    this.RewrittenStmtTokens[index].Type != TokenType.LeftParenthesis) ||
                    (expectsComma && this.RewrittenStmtTokens[index].Type != TokenType.Comma))
                {
                    break;
                }

                if (this.RewrittenStmtTokens[index].Type == TokenType.Identifier ||
                    this.RewrittenStmtTokens[index].Type == TokenType.This ||
                    this.RewrittenStmtTokens[index].Type == TokenType.True ||
                    this.RewrittenStmtTokens[index].Type == TokenType.False)
                {
                    index++;
                    expectsComma = true;
                }
                else if (this.RewrittenStmtTokens[index].Type == TokenType.LeftParenthesis)
                {
                    this.RewriteTuple(ref index);
                    index++;
                    expectsComma = true;
                }
                else if (this.RewrittenStmtTokens[index].Type == TokenType.Comma)
                {
                    tupleSize++;
                    expectsComma = false;
                    index++;
                }
            }

            if (tupleSize > 1)
            {
                var tupleStr = "Container.Create(";
                var textUnit = new TextUnit(tupleStr, this.RewrittenStmtTokens[tupleIdx].TextUnit.Line,
                    this.RewrittenStmtTokens[tupleIdx].TextUnit.Start);
                this.RewrittenStmtTokens[tupleIdx] = new Token(textUnit);
            }
            else
            {
                this.RewrittenStmtTokens.RemoveAt(index);
                this.RewrittenStmtTokens.RemoveAt(tupleIdx);
            }
        }

        /// <summary>
        /// Rewrites the tuple index identifier.
        /// </summary>
        /// <param name="position">Position</param>
        protected void RewriteTupleIndexIdentifier(ref int position)
        {
            int index = -1;
            if (!int.TryParse(this.RewrittenStmtTokens[this.Index].TextUnit.Text, out index))
            {
                return;
            }

            index++;
            if (this.Index == 0 ||
                this.RewrittenStmtTokens[this.Index - 1].Type != TokenType.Dot)
            {
                return;
            }

            int line = this.RewrittenStmtTokens[this.Index].TextUnit.Line;
            var text = "Item" + index;
            this.RewrittenStmtTokens[this.Index] = new Token(new TextUnit(text, line, position));
            position += text.Length;
        }

        /// <summary>
        /// Rewrites the insert element at seq.
        /// </summary>
        /// <param name="position">Position</param>
        protected void RewriteInsertElementAtSeq(ref int position)
        {
            var seqIndex = this.Index;
            if (this.Parent.Machine == null ||
                !(this.Parent.Machine.FieldDeclarations.Any(val => val.Identifier.TextUnit.Text.
                Equals(this.RewrittenStmtTokens[this.Index].TextUnit.Text))))
            {
                return;
            }

            var field = this.Parent.Machine.FieldDeclarations.Find(val => val.Identifier.TextUnit.Text.
                Equals(this.RewrittenStmtTokens[this.Index].TextUnit.Text));
            if (field.TypeNode.Type.Type != PType.Seq)
            {
                return;
            }

            this.Index++;
            this.SkipWhiteSpaceTokens();
            if (this.Index == this.RewrittenStmtTokens.Count ||
                this.RewrittenStmtTokens[this.Index] == null ||
                this.RewrittenStmtTokens[this.Index].Type != TokenType.InsertOp)
            {
                this.Index = seqIndex;
                return;
            }

            this.Index++;
            this.SkipWhiteSpaceTokens();
            while (this.Index - 1 > seqIndex)
            {
                this.RewrittenStmtTokens.RemoveAt(this.Index - 1);
                this.Index--;
            }

            int line = this.RewrittenStmtTokens[this.Index].TextUnit.Line;
            var text = ".Insert";
            this.RewrittenStmtTokens.Insert(this.Index, new Token(new TextUnit(text, line, position)));
            position += text.Length;
            this.Index = seqIndex;
        }

        /// <summary>
        /// Rewrites the remove element of seq.
        /// </summary>
        /// <param name="position">Position</param>
        protected void RewriteRemoveElementOfSeq(ref int position)
        {
            var seqIndex = this.Index;
            if (this.Parent.Machine == null ||
                !(this.Parent.Machine.FieldDeclarations.Any(val => val.Identifier.TextUnit.Text.
                Equals(this.RewrittenStmtTokens[this.Index].TextUnit.Text))))
            {
                return;
            }

            var field = this.Parent.Machine.FieldDeclarations.Find(val => val.Identifier.TextUnit.Text.
                Equals(this.RewrittenStmtTokens[this.Index].TextUnit.Text));
            if (field.TypeNode.Type.Type != PType.Seq)
            {
                return;
            }

            this.Index++;
            this.SkipWhiteSpaceTokens();
            if (this.Index == this.RewrittenStmtTokens.Count ||
                this.RewrittenStmtTokens[this.Index] == null ||
                this.RewrittenStmtTokens[this.Index].Type != TokenType.RemoveOp)
            {
                this.Index = seqIndex;
                return;
            }

            this.Index++;
            this.SkipWhiteSpaceTokens();
            if (this.Index == this.RewrittenStmtTokens.Count ||
                this.RewrittenStmtTokens[this.Index] == null)
            {
                this.Index = seqIndex;
                return;
            }

            var removeIndex = this.RewrittenStmtTokens[this.Index].TextUnit.Text;

            while (this.Index > seqIndex)
            {
                this.RewrittenStmtTokens.RemoveAt(this.Index);
                this.Index--;
            }

            int line = this.RewrittenStmtTokens[this.Index].TextUnit.Line;
            var text = ".RemoveAt(" + removeIndex + ")";
            this.RewrittenStmtTokens.Insert(this.Index + 1, new Token(new TextUnit(text, line, position)));
            position += text.Length;
            this.Index = seqIndex;
        }

        /// <summary>
        /// Rewrites the non-deterministic choice.
        /// </summary>
        /// <param name="position">Position</param>
        protected void RewriteNonDeterministicChoice(ref int position)
        {
            int line = this.RewrittenStmtTokens[this.Index].TextUnit.Line;
            var text = "Microsoft.PSharp.Model.Havoc.Boolean";
            this.RewrittenStmtTokens[this.Index] = new Token(new TextUnit(text, line, position));
            position += text.Length;
        }

        #endregion

        #region private API

        /// <summary>
        /// Rewrites the next token.
        /// </summary>
        /// <param name="position">Position</param>
        private void RewriteNextToken(ref int position)
        {
            if (this.Index == this.RewrittenStmtTokens.Count)
            {
                return;
            }
            
            var token = this.RewrittenStmtTokens[this.Index];
            if (token == null)
            {
                this.RewriteReceivedPayload(ref position);
            }
            else if (token.Type == TokenType.MachineDecl)
            {
                this.RewriteMachineType(ref position);
            }
            else if (token.Type == TokenType.This)
            {
                this.RewriteThis(ref position);
            }
            else if (token.Type == TokenType.SizeOf)
            {
                this.RewriteSizeOf(ref position);
            }
            else if (token.Type == TokenType.AssignOp)
            {
                this.RewriteTupleAssignment(ref position);
            }
            else if (token.Type == TokenType.NonDeterministic)
            {
                this.RewriteNonDeterministicChoice(ref position);
            }
            else if (token.Type == TokenType.Identifier)
            {
                this.RewriteInsertElementAtSeq(ref position);
                this.RewriteRemoveElementOfSeq(ref position);
                this.RewriteTupleIndexIdentifier(ref position);
                this.RewriteMemberIdentifier(ref position);
            }

            this.Index++;
            this.RewriteNextToken(ref position);
        }

        /// <summary>
        /// Rewrites the received payload.
        /// </summary>
        /// param name="position">Position</param>
        private void RewriteReceivedPayload(ref int position)
        {
            var payload = this.PendingPayloads[0];
            this.PendingPayloads.RemoveAt(0);

            payload.GenerateTextUnit();
            payload.Rewrite(ref position);
            var text = payload.GetRewrittenText();
            var line = payload.TextUnit.Line;

            this.RewrittenStmtTokens[this.Index] = new Token(new TextUnit(text, line, position));
            position += text.Length;
        }

        /// <summary>
        /// Skips whitespace tokens.
        /// </summary>
        private void SkipWhiteSpaceTokens()
        {
            while (this.Index < this.RewrittenStmtTokens.Count &&
                this.RewrittenStmtTokens[this.Index] != null &&
                (this.RewrittenStmtTokens[this.Index].Type == TokenType.WhiteSpace ||
                this.RewrittenStmtTokens[this.Index].Type == TokenType.NewLine))
            {
                this.Index++;
            }

            return;
        }

        #endregion
    }
}
