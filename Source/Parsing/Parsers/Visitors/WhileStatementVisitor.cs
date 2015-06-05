﻿//-----------------------------------------------------------------------
// <copyright file="WhileStatementVisitor.cs">
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

using Microsoft.PSharp.Parsing.Syntax;

namespace Microsoft.PSharp.Parsing
{
    /// <summary>
    /// The P# while statement parsing visitor.
    /// </summary>
    internal sealed class WhileStatementVisitor : BaseParseVisitor
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="tokenStream">TokenStream</param>
        internal WhileStatementVisitor(TokenStream tokenStream)
            : base(tokenStream)
        {

        }

        /// <summary>
        /// Visits the syntax node.
        /// </summary>
        /// <param name="parentNode">Node</param>
        internal void Visit(StatementBlockNode parentNode)
        {
            var node = new WhileStatementNode(parentNode);
            node.WhileKeyword = base.TokenStream.Peek();

            base.TokenStream.Index++;
            base.TokenStream.SkipWhiteSpaceAndCommentTokens();

            if (base.TokenStream.Done ||
                base.TokenStream.Peek().Type != TokenType.LeftParenthesis)
            {
                throw new ParsingException("Expected \"(\".",
                    new List<TokenType>
                {
                    TokenType.LeftParenthesis
                });
            }

            node.LeftParenthesisToken = base.TokenStream.Peek();

            base.TokenStream.Index++;
            base.TokenStream.SkipWhiteSpaceAndCommentTokens();

            if (base.TokenStream.IsPSharp)
            {
                var guard = new ExpressionNode(parentNode);

                int counter = 1;
                while (!base.TokenStream.Done)
                {
                    if (base.TokenStream.Peek().Type == TokenType.LeftParenthesis)
                    {
                        counter++;
                    }
                    else if (base.TokenStream.Peek().Type == TokenType.RightParenthesis)
                    {
                        counter--;
                    }

                    if (counter == 0)
                    {
                        break;
                    }

                    guard.StmtTokens.Add(base.TokenStream.Peek());
                    base.TokenStream.Index++;
                    base.TokenStream.SkipCommentTokens();
                }

                node.Guard = guard;
            }
            else
            {
                var guard = new PExpressionNode(parentNode);

                int counter = 1;
                while (!base.TokenStream.Done)
                {
                    if (base.TokenStream.Peek().Type == TokenType.Payload)
                    {
                        var payloadNode = new PPayloadReceiveNode();
                        new ReceivedPayloadVisitor(base.TokenStream).Visit(payloadNode);
                        guard.StmtTokens.Add(null);
                        guard.Payloads.Add(payloadNode);

                        if (payloadNode.RightParenthesisToken != null)
                        {
                            counter--;
                        }
                    }

                    if (base.TokenStream.Peek().Type == TokenType.LeftParenthesis)
                    {
                        counter++;
                    }
                    else if (base.TokenStream.Peek().Type == TokenType.RightParenthesis)
                    {
                        counter--;
                    }

                    if (counter == 0)
                    {
                        break;
                    }

                    guard.StmtTokens.Add(base.TokenStream.Peek());
                    base.TokenStream.Index++;
                    base.TokenStream.SkipCommentTokens();
                }

                node.Guard = guard;
            }

            if (base.TokenStream.Done ||
                base.TokenStream.Peek().Type != TokenType.RightParenthesis)
            {
                throw new ParsingException("Expected \")\".",
                    new List<TokenType>
                {
                    TokenType.RightParenthesis
                });
            }

            node.RightParenthesisToken = base.TokenStream.Peek();

            base.TokenStream.Index++;
            base.TokenStream.SkipWhiteSpaceAndCommentTokens();

            if (base.TokenStream.IsPSharp)
            {
                if (base.TokenStream.Done ||
                    (base.TokenStream.Peek().Type != TokenType.New &&
                    base.TokenStream.Peek().Type != TokenType.CreateMachine &&
                    base.TokenStream.Peek().Type != TokenType.RaiseEvent &&
                    base.TokenStream.Peek().Type != TokenType.SendEvent &&
                    base.TokenStream.Peek().Type != TokenType.Assert &&
                    base.TokenStream.Peek().Type != TokenType.IfCondition &&
                    base.TokenStream.Peek().Type != TokenType.Break &&
                    base.TokenStream.Peek().Type != TokenType.Continue &&
                    base.TokenStream.Peek().Type != TokenType.Return &&
                    base.TokenStream.Peek().Type != TokenType.This &&
                    base.TokenStream.Peek().Type != TokenType.Base &&
                    base.TokenStream.Peek().Type != TokenType.Var &&
                    base.TokenStream.Peek().Type != TokenType.Int &&
                    base.TokenStream.Peek().Type != TokenType.Bool &&
                    base.TokenStream.Peek().Type != TokenType.Identifier &&
                    base.TokenStream.Peek().Type != TokenType.LeftCurlyBracket))
                {
                    throw new ParsingException("Expected \"{\".",
                        new List<TokenType>
                    {
                            TokenType.LeftCurlyBracket
                    });
                }
            }
            else
            {
                if (base.TokenStream.Done ||
                    (base.TokenStream.Peek().Type != TokenType.New &&
                    base.TokenStream.Peek().Type != TokenType.RaiseEvent &&
                    base.TokenStream.Peek().Type != TokenType.SendEvent &&
                    base.TokenStream.Peek().Type != TokenType.Monitor &&
                    base.TokenStream.Peek().Type != TokenType.PushState &&
                    base.TokenStream.Peek().Type != TokenType.Assert &&
                    base.TokenStream.Peek().Type != TokenType.IfCondition &&
                    base.TokenStream.Peek().Type != TokenType.WhileLoop &&
                    base.TokenStream.Peek().Type != TokenType.Break &&
                    base.TokenStream.Peek().Type != TokenType.Continue &&
                    base.TokenStream.Peek().Type != TokenType.Return &&
                    base.TokenStream.Peek().Type != TokenType.Identifier &&
                    base.TokenStream.Peek().Type != TokenType.LeftCurlyBracket))
                {
                    throw new ParsingException("Expected \"{\".",
                        new List<TokenType>
                    {
                            TokenType.LeftCurlyBracket
                    });
                }
            }
            
            var blockNode = new StatementBlockNode(parentNode.Machine, parentNode.State);

            if (base.TokenStream.Peek().Type == TokenType.New)
            {
                new NewStatementVisitor(base.TokenStream).Visit(blockNode);
            }
            else if (base.TokenStream.Peek().Type == TokenType.CreateMachine)
            {
                new CreateMonitorStatementVisitor(base.TokenStream).Visit(blockNode);
            }
            else if (base.TokenStream.Peek().Type == TokenType.RaiseEvent)
            {
                new RaiseStatementVisitor(base.TokenStream).Visit(blockNode);
            }
            else if (base.TokenStream.Peek().Type == TokenType.SendEvent)
            {
                new SendStatementVisitor(base.TokenStream).Visit(blockNode);
            }
            else if (base.TokenStream.Peek().Type == TokenType.Monitor)
            {
                new MonitorStatementVisitor(base.TokenStream).Visit(blockNode);
            }
            else if (base.TokenStream.Peek().Type == TokenType.PushState)
            {
                new PushStatementVisitor(base.TokenStream).Visit(blockNode);
            }
            else if (base.TokenStream.Peek().Type == TokenType.Pop)
            {
                new PopStatementVisitor(base.TokenStream).Visit(blockNode);
            }
            else if (base.TokenStream.Peek().Type == TokenType.Assert)
            {
                new AssertStatementVisitor(base.TokenStream).Visit(blockNode);
            }
            else if (base.TokenStream.Peek().Type == TokenType.IfCondition)
            {
                new IfStatementVisitor(base.TokenStream).Visit(blockNode);
            }
            else if (base.TokenStream.Peek().Type == TokenType.WhileLoop)
            {
                new WhileStatementVisitor(base.TokenStream).Visit(blockNode);
            }
            else if (base.TokenStream.Peek().Type == TokenType.Break ||
                base.TokenStream.Peek().Type == TokenType.Continue ||
                base.TokenStream.Peek().Type == TokenType.Return ||
                base.TokenStream.Peek().Type == TokenType.This ||
                base.TokenStream.Peek().Type == TokenType.Base ||
                base.TokenStream.Peek().Type == TokenType.Var ||
                base.TokenStream.Peek().Type == TokenType.Int ||
                base.TokenStream.Peek().Type == TokenType.Bool ||
                base.TokenStream.Peek().Type == TokenType.Identifier)
            {
                new GenericStatementVisitor(base.TokenStream).Visit(blockNode);
            }
            else if (base.TokenStream.Peek().Type == TokenType.LeftCurlyBracket)
            {
                new StatementBlockVisitor(base.TokenStream).Visit(blockNode);
            }

            node.StatementBlock = blockNode;
            
            parentNode.Statements.Add(node);
            base.TokenStream.Index++;
        }
    }
}
