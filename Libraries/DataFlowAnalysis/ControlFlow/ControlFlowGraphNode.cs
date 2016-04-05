﻿//-----------------------------------------------------------------------
// <copyright file="ControlFlowGraphNode.cs">
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
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.CodeAnalysis.CSharp.DataFlowAnalysis
{
    /// <summary>
    /// A control-flow graph node.
    /// </summary>
    public sealed class ControlFlowGraphNode
    {
        #region fields

        /// <summary>
        /// The analysis context.
        /// </summary>
        private AnalysisContext AnalysisContext;

        /// <summary>
        /// The unique ID of the node.
        /// </summary>
        public int Id;

        /// <summary>
        /// Handle to the summary of the method which owns this node.
        /// </summary>
        private MethodSummary Summary;

        /// <summary>
        /// List of statements.
        /// </summary>
        public List<Statement> Statements;

        /// <summary>
        /// Set of the immediate predecessors of the node.
        /// </summary>
        private HashSet<ControlFlowGraphNode> IPredecessors;

        /// <summary>
        /// Set of the immediate successors of the node.
        /// </summary>
        private HashSet<ControlFlowGraphNode> ISuccessors;

        /// <summary>
        /// The node after exiting the loop.
        /// </summary>
        private ControlFlowGraphNode LoopExitNode;

        /// <summary>
        /// True if the node is a jump node. False by default.
        /// </summary>
        public bool IsJumpNode;

        /// <summary>
        /// True if the node is a loop head node. False by default.
        /// </summary>
        public bool IsLoopHeadNode;

        /// <summary>
        /// The description of the node.
        /// </summary>
        private string Description;

        /// <summary>
        /// A counter for creating unique IDs.
        /// </summary>
        private static int IdCounter;

        #endregion

        #region constructors

        /// <summary>
        /// Static constructor.
        /// </summary>
        static ControlFlowGraphNode()
        {
            ControlFlowGraphNode.IdCounter = 0;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">AnalysisContext</param>
        /// <param name="summary">MethodSummary</param>
        private ControlFlowGraphNode(AnalysisContext context, MethodSummary summary)
        {
            this.AnalysisContext = context;
            this.Id = ControlFlowGraphNode.IdCounter++;
            this.Summary = summary;
            this.Statements = new List<Statement>();
            this.IPredecessors = new HashSet<ControlFlowGraphNode>();
            this.ISuccessors = new HashSet<ControlFlowGraphNode>();
            this.IsJumpNode = false;
            this.IsLoopHeadNode = false;
            this.Description = "";
        }

        #endregion

        #region public API

        /// <summary>
        /// Creates a new control-flow graph node.
        /// </summary>
        /// <param name="context">AnalysisContext</param>
        /// <param name="summary">MethodSummary</param>
        /// <returns>ControlFlowGraphNode</returns>
        public static ControlFlowGraphNode Create(AnalysisContext context, MethodSummary summary)
        {
            return new ControlFlowGraphNode(context, summary);
        }

        /// <summary>
        /// Constructs the control-flow graph of the given method.
        /// </summary>
        /// <param name="method">Method</param>
        public void Construct(BaseMethodDeclarationSyntax method)
        {
            this.Statements.Add(Statement.Create(method.ParameterList, this));

            var bodyNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
            bodyNode.Construct(this.GetStatements(method.Body), 0, null);

            this.ISuccessors.Add(bodyNode);
            bodyNode.IPredecessors.Add(this);
        }

        /// <summary>
        /// Returns the method summary that contains this
        /// control-flow graph node.
        /// </summary>
        /// <returns>MethodSummary</returns>
        public MethodSummary GetMethodSummary()
        {
            return this.Summary;
        }

        /// <summary>
        /// Returns the immediate predecessors of this
        /// control-flow graph node.
        /// </summary>
        /// <returns>Predecessors</returns>
        public IEnumerable<ControlFlowGraphNode> GetImmediatePredecessors()
        {
            return this.IPredecessors.ToList();
        }

        /// <summary>
        /// Returns the immediate successors of this
        /// control-flow graph node.
        /// </summary>
        /// <returns>Successors</returns>
        public IEnumerable<ControlFlowGraphNode> GetImmediateSuccessors()
        {
            return this.ISuccessors.ToList();
        }

        /// <summary>
        /// Returns true if the node is a predecessor of the given node.
        /// Returns false if not.
        /// </summary>
        /// <param name="node">ControlFlowGraphNode</param>
        /// <returns>Boolean</returns>
        public bool IsPredecessorOf(ControlFlowGraphNode node)
        {
            return this.IsPredecessorOf(node, new HashSet<ControlFlowGraphNode>());
        }

        /// <summary>
        /// Returns true if the node is a successor of the given node.
        /// Returns false if not.
        /// </summary>
        /// <param name="node">ControlFlowGraphNode</param>
        /// <returns>Boolean</returns>
        public bool IsSuccessorOf(ControlFlowGraphNode node)
        {
            return this.IsSuccessorOf(node, new HashSet<ControlFlowGraphNode>());
        }

        /// <summary>
        /// Returns all exit nodes in the control-flow graph.
        /// </summary>
        /// <returns>Set of exit nodes</returns>
        internal HashSet<ControlFlowGraphNode> GetExitNodes()
        {
            return this.GetExitNodes(new HashSet<ControlFlowGraphNode>());
        }

        /// <summary>
        /// Cleans empty successors.
        /// </summary>
        internal void CleanEmptySuccessors()
        {
            this.CleanEmptySuccessors(new HashSet<ControlFlowGraphNode>());
        }

        #endregion

        #region private methods

        /// <summary>
        /// Constructs the node from the given list of statements starting
        /// at the given index.
        /// </summary>
        /// <param name="stmtList">List of statements</param>
        /// <param name="index">Statement index</param>
        /// <param name="successor">Successor</param>
        private void Construct(List<StatementSyntax> stmtList, int index, ControlFlowGraphNode successor)
        {
            for (int idx = index; idx < stmtList.Count; idx++)
            {
                ControlFlowGraphNode jumpNode = null;
                ControlFlowGraphNode succNode = null;
                
                if (stmtList[idx] is ExpressionStatementSyntax ||
                    stmtList[idx] is LocalDeclarationStatementSyntax)
                {
                    this.Statements.Add(Statement.Create(stmtList[idx], this));
                    continue;
                }
                else if (stmtList[idx] is BreakStatementSyntax)
                {
                    if (successor != null && successor.LoopExitNode != null)
                    {
                        this.ISuccessors.Add(successor.LoopExitNode);
                        successor.LoopExitNode.IPredecessors.Add(this);
                    }

                    return;
                }
                else if (stmtList[idx] is ContinueStatementSyntax)
                {
                    if (successor != null)
                    {
                        this.ISuccessors.Add(successor);
                        successor.IPredecessors.Add(this);
                    }

                    return;
                }
                else if (stmtList[idx] is ReturnStatementSyntax)
                {
                    this.Statements.Add(Statement.Create(stmtList[idx], this));
                    continue;
                }

                if (this.Statements.Count == 0)
                {
                    jumpNode = this;
                }
                else
                {
                    jumpNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
                    this.ISuccessors.Add(jumpNode);
                    jumpNode.IPredecessors.Add(this);
                }

                if (stmtList[idx] is IfStatementSyntax)
                {
                    if (idx < stmtList.Count - 1)
                    {
                        succNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
                        jumpNode.HandleIfStatement(stmtList[idx] as IfStatementSyntax, succNode);
                        succNode.Construct(stmtList, idx + 1, successor);
                        return;
                    }
                    else
                    {
                        jumpNode.HandleIfStatement(stmtList[idx] as IfStatementSyntax, successor);
                    }
                }
                else if (stmtList[idx] is ForStatementSyntax)
                {
                    if (idx < stmtList.Count - 1)
                    {
                        succNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
                        jumpNode.HandleForStatement(stmtList[idx] as ForStatementSyntax, succNode);
                        succNode.Construct(stmtList, idx + 1, successor);
                        return;
                    }
                    else
                    {
                        jumpNode.HandleForStatement(stmtList[idx] as ForStatementSyntax, successor);
                    }
                }
                else if (stmtList[idx] is WhileStatementSyntax)
                {
                    if (idx < stmtList.Count - 1)
                    {
                        succNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
                        jumpNode.HandleWhileStatement(stmtList[idx] as WhileStatementSyntax, succNode);
                        succNode.Construct(stmtList, idx + 1, successor);
                        return;
                    }
                    else
                    {
                        jumpNode.HandleWhileStatement(stmtList[idx] as WhileStatementSyntax, successor);
                    }
                }
                else if (stmtList[idx] is DoStatementSyntax)
                {
                    if (idx < stmtList.Count - 1)
                    {
                        succNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
                        jumpNode.HandleDoStatement(stmtList[idx] as DoStatementSyntax, succNode);
                        succNode.Construct(stmtList, idx + 1, successor);
                        return;
                    }
                    else
                    {
                        jumpNode.HandleDoStatement(stmtList[idx] as DoStatementSyntax, successor);
                    }
                }
                else if (stmtList[idx] is ForEachStatementSyntax)
                {
                    if (idx < stmtList.Count - 1)
                    {
                        succNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
                        jumpNode.HandleForeachStatement(stmtList[idx] as ForEachStatementSyntax, succNode);
                        succNode.Construct(stmtList, idx + 1, successor);
                        return;
                    }
                    else
                    {
                        jumpNode.HandleForeachStatement(stmtList[idx] as ForEachStatementSyntax, successor);
                    }
                }
                else if (stmtList[idx] is SwitchStatementSyntax)
                {
                    if (idx < stmtList.Count - 1)
                    {
                        succNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
                        jumpNode.HandleSwitchStatement(stmtList[idx] as SwitchStatementSyntax, succNode);
                        succNode.Construct(stmtList, idx + 1, successor);
                        return;
                    }
                    else
                    {
                        jumpNode.HandleSwitchStatement(stmtList[idx] as SwitchStatementSyntax, successor);
                    }
                }
                else if (stmtList[idx] is TryStatementSyntax)
                {
                    if (idx < stmtList.Count - 1)
                    {
                        succNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
                        jumpNode.HandleTryStatement(stmtList[idx] as TryStatementSyntax, succNode);
                        succNode.Construct(stmtList, idx + 1, successor);
                        return;
                    }
                    else
                    {
                        jumpNode.HandleTryStatement(stmtList[idx] as TryStatementSyntax, successor);
                    }
                }
                else if (stmtList[idx] is UsingStatementSyntax)
                {
                    if (idx < stmtList.Count - 1)
                    {
                        succNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
                        jumpNode.HandleUsingStatement(stmtList[idx] as UsingStatementSyntax, succNode);
                        succNode.Construct(stmtList, idx + 1, successor);
                        return;
                    }
                    else
                    {
                        jumpNode.HandleUsingStatement(stmtList[idx] as UsingStatementSyntax, successor);
                    }
                }
            }

            if (successor != null && (this.IsJumpNode ||
                (!this.IsJumpNode && this.ISuccessors.Count == 0)))
            {
                this.ISuccessors.Add(successor);
                successor.IPredecessors.Add(this);
            }

            if (this.Statements.Count == 0)
            {
                foreach (var pred in this.IPredecessors)
                {
                    pred.ISuccessors.Remove(this);
                }
            }
        }

        /// <summary>
        /// Handles the given if statement.
        /// </summary>
        /// <param name="stmt">Statement</param>
        /// <param name="successor">Successor</param>
        private void HandleIfStatement(IfStatementSyntax stmt, ControlFlowGraphNode successor)
        {
            this.Statements.Add(Statement.Create(stmt.Condition, this));
            this.IsJumpNode = true;
            this.Description = "Jump";

            var ifNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
            this.ISuccessors.Add(ifNode);
            ifNode.IPredecessors.Add(this);

            ifNode.Construct(this.GetStatements(stmt.Statement), 0, successor);

            if (stmt.Else != null)
            {
                var elseNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
                this.ISuccessors.Add(elseNode);
                elseNode.IPredecessors.Add(this);

                if (stmt.Else.Statement is IfStatementSyntax)
                {
                    elseNode.HandleIfStatement(stmt.Else.Statement as IfStatementSyntax, successor);
                }
                else
                {
                    elseNode.Construct(this.GetStatements(stmt.Else.Statement), 0, successor);
                }
            }
            else if (successor != null)
            {
                this.ISuccessors.Add(successor);
                successor.IPredecessors.Add(this);
            }
        }

        /// <summary>
        /// Handles the given for statement.
        /// </summary>
        /// <param name="stmt">Statement</param>
        /// <param name="successor">Successor</param>
        private void HandleForStatement(ForStatementSyntax stmt, ControlFlowGraphNode successor)
        {
            this.Statements.Add(Statement.Create(stmt.Condition, this));
            this.IsLoopHeadNode = true;
            this.Description = "LoopHead";

            if (successor != null)
            {
                this.ISuccessors.Add(successor);
                successor.IPredecessors.Add(this);
                this.LoopExitNode = successor;
            }

            var forNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
            this.ISuccessors.Add(forNode);
            forNode.IPredecessors.Add(this);

            forNode.Construct(this.GetStatements(stmt.Statement), 0, this);
        }

        /// <summary>
        /// Handles the given while statement.
        /// </summary>
        /// <param name="stmt">Statement</param>
        /// <param name="successor">Successor</param>
        private void HandleWhileStatement(WhileStatementSyntax stmt, ControlFlowGraphNode successor)
        {
            this.Statements.Add(Statement.Create(stmt.Condition, this));
            this.IsLoopHeadNode = true;
            this.Description = "LoopHead";

            if (successor != null)
            {
                this.ISuccessors.Add(successor);
                successor.IPredecessors.Add(this);
                this.LoopExitNode = successor;
            }

            var whileNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
            this.ISuccessors.Add(whileNode);
            whileNode.IPredecessors.Add(this);

            whileNode.Construct(this.GetStatements(stmt.Statement), 0, this);
        }

        /// <summary>
        /// Handles the given do statement.
        /// </summary>
        /// <param name="stmt">Statement</param>
        /// <param name="successor">Successor</param>
        private void HandleDoStatement(DoStatementSyntax stmt, ControlFlowGraphNode successor)
        {
            this.Statements.Add(Statement.Create(stmt.Condition, this));
            this.IsLoopHeadNode = true;
            this.Description = "LoopHead";

            if (successor != null)
            {
                this.ISuccessors.Add(successor);
                successor.IPredecessors.Add(this);
                this.LoopExitNode = successor;
            }

            var doNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
            this.ISuccessors.Add(doNode);
            doNode.IPredecessors.Add(this);

            doNode.Construct(this.GetStatements(stmt.Statement), 0, this);
        }

        /// <summary>
        /// Handles the given foreach statement.
        /// </summary>
        /// <param name="stmt">Statement</param>
        /// <param name="successor">Successor</param>
        private void HandleForeachStatement(ForEachStatementSyntax stmt, ControlFlowGraphNode successor)
        {
            this.Statements.Add(Statement.Create(stmt.Expression, this));
            this.IsLoopHeadNode = true;
            this.Description = "LoopHead";

            if (successor != null)
            {
                this.ISuccessors.Add(successor);
                successor.IPredecessors.Add(this);
                this.LoopExitNode = successor;
            }

            var foreachNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
            this.ISuccessors.Add(foreachNode);
            foreachNode.IPredecessors.Add(this);

            foreachNode.Construct(this.GetStatements(stmt.Statement), 0, this);
        }

        /// <summary>
        /// Handles the given switch statement.
        /// </summary>
        /// <param name="stmt">Statement</param>
        /// <param name="successor">Successor</param>
        private void HandleSwitchStatement(SwitchStatementSyntax stmt, ControlFlowGraphNode successor)
        {
            this.Statements.Add(Statement.Create(stmt.Expression, this));
            this.IsJumpNode = true;
            this.Description = "Jump";

            if (stmt.Sections.Count == 0 &&
                successor != null)
            {
                this.ISuccessors.Add(successor);
                successor.IPredecessors.Add(this);
                return;
            }

            for (int idx = 0; idx < stmt.Sections.Count; idx++)
            {
                var statements = stmt.Sections[idx].Statements.ToList();
                bool containsBreak = false;
                foreach (var s in statements)
                {
                    if (s is BreakStatementSyntax)
                    {
                        containsBreak = true;
                        break;
                    }
                }

                var switchNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
                this.ISuccessors.Add(switchNode);
                switchNode.IPredecessors.Add(this);

                if (containsBreak || idx == stmt.Sections.Count - 1)
                {
                    switchNode.Construct(statements, 0, successor);
                }
                else
                {
                    switchNode.Construct(statements, 0, null);
                }
            }
        }

        /// <summary>
        /// Handles the given try statement.
        /// </summary>
        /// <param name="stmt">Statement</param>
        /// <param name="successor">Successor</param>
        private void HandleTryStatement(TryStatementSyntax stmt, ControlFlowGraphNode successor)
        {
            this.Construct(this.GetStatements(stmt.Block), 0, successor);
        }

        /// <summary>
        /// Handles the given using statement.
        /// </summary>
        /// <param name="stmt">Statement</param>
        /// <param name="successor">Successor</param>
        private void HandleUsingStatement(UsingStatementSyntax stmt, ControlFlowGraphNode successor)
        {
            this.Statements.Add(Statement.Create(stmt.Declaration, this));
            this.IsJumpNode = true;
            this.Description = "Jump";

            var usingNode = ControlFlowGraphNode.Create(this.AnalysisContext, this.Summary);
            this.ISuccessors.Add(usingNode);
            usingNode.IPredecessors.Add(this);

            usingNode.Construct(this.GetStatements(stmt.Statement), 0, successor);
        }

        /// <summary>
        /// Returns a list containing all statements in the given block.
        /// </summary>
        /// <param name="statement">StatementSyntax</param>
        /// <returns>SyntaxList</returns>
        private List<StatementSyntax> GetStatements(StatementSyntax statement)
        {
            if (statement is BlockSyntax)
            {
                return (statement as BlockSyntax).Statements.ToList();
            }

            return new List<StatementSyntax> { statement };
        }

        /// <summary>
        /// Returns true if the node is a predecessor of the given node.
        /// Returns false if not.
        /// </summary>
        /// <param name="node">ControlFlowGraphNode</param>
        /// <param name="visited">Already visited cfgNodes</param>
        /// <returns>Boolean</returns>
        private bool IsPredecessorOf(ControlFlowGraphNode node, HashSet<ControlFlowGraphNode> visited)
        {
            visited.Add(this);

            if (this.ISuccessors.Contains(node))
            {
                return true;
            }

            foreach (var successor in this.ISuccessors.Where(v => !visited.Contains(v)))
            {
                if (successor.IsPredecessorOf(node, visited))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if the node is a successor of the given node.
        /// Returns false if not.
        /// </summary>
        /// <param name="node">ControlFlowGraphNode</param>
        /// <param name="visited">Already visited cfgNodes</param>
        /// <returns>Boolean</returns>
        private bool IsSuccessorOf(ControlFlowGraphNode node, HashSet<ControlFlowGraphNode> visited)
        {
            visited.Add(this);

            if (this.IPredecessors.Contains(node))
            {
                return true;
            }

            foreach (var predecessor in this.IPredecessors.Where(v => !visited.Contains(v)))
            {
                if (predecessor.IsSuccessorOf(node, visited))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns all exit nodes in the control-flow graph.
        /// </summary>
        /// <param name="visited">Already visited cfgNodes</param>
        /// <returns>Set of exit nodes</returns>
        private HashSet<ControlFlowGraphNode> GetExitNodes(HashSet<ControlFlowGraphNode> visited)
        {
            visited.Add(this);

            var exitNodes = new HashSet<ControlFlowGraphNode>();
            if (this.ISuccessors.Count == 0)
            {
                exitNodes.Add(this);
            }
            else
            {
                foreach (var successor in this.ISuccessors.Where(v => !visited.Contains(v)))
                {
                    var nodes = successor.GetExitNodes(visited);
                    foreach (var node in nodes)
                    {
                        exitNodes.Add(node);
                    }
                }
            }

            return exitNodes;
        }

        /// <summary>
        /// Cleans empty successors.
        /// </summary>
        /// <param name="visited">Already visited cfgNodes</param>
        private void CleanEmptySuccessors(HashSet<ControlFlowGraphNode> visited)
        {
            visited.Add(this);

            var toRemove = new List<ControlFlowGraphNode>();
            foreach (var successor in this.ISuccessors)
            {
                if (successor.Statements.Count == 0)
                {
                    toRemove.Add(successor);
                }
            }

            foreach (var successor in toRemove)
            {
                this.ISuccessors.Remove(successor);
            }

            foreach (var successor in this.ISuccessors.Where(v => !visited.Contains(v)))
            {
                successor.CleanEmptySuccessors(visited);
            }
        }

        #endregion

        #region control-flow summary printing methods

        /// <summary>
        /// Prints the control-flow graph nodes.
        /// </summary>
        internal void PrintControlFlowGraphNodes()
        {
            Console.WriteLine("... |");
            Console.WriteLine("... | . List of CFG nodes");
            this.PrintControlFlowGraphNodes(null);
        }

        /// <summary>
        /// Prints the control-flow graph successor nodes.
        /// </summary>
        internal void PrintCFGSuccessors()
        {
            Console.WriteLine("... |");
            Console.WriteLine("... | . Successor CFG nodes");
            this.PrintCFGSuccessors(null);
        }

        /// <summary>
        /// Prints the control-flow graph predecessor nodes.
        /// </summary>
        internal void PrintCFGPredecessors()
        {
            Console.WriteLine("... |");
            Console.WriteLine("... | . Predecessor CFG nodes");
            this.PrintCFGPredecessors(null);
        }

        /// <summary>
        /// Prints the control-flow graph nodes.
        /// </summary>
        /// <param name="visited">Set of visited CFG nodes</param>
        private void PrintControlFlowGraphNodes(HashSet<ControlFlowGraphNode> visited)
        {
            if (visited == null)
            {
                visited = new HashSet<ControlFlowGraphNode> { this };
            }
            else if (visited.Contains(this))
            {
                return;
            }
            else
            {
                visited.Add(this);
            }

            if (this.Description.Length > 0)
            {
                Console.WriteLine("... | ... [{0}] '{1}'", this.Description, this.Id);
            }
            else
            {
                Console.WriteLine("... | ... '{0}'", this.Id);
            }

            foreach (var node in this.Statements)
            {
                Console.WriteLine("... | ..... '{0}'", node);
            }

            foreach (var node in this.ISuccessors)
            {
                node.PrintControlFlowGraphNodes(visited);
            }
        }

        /// <summary>
        /// Prints the control-flow graph successor nodes.
        /// </summary>
        /// <param name="visited">Set of visited CFG nodes</param>
        private void PrintCFGSuccessors(HashSet<ControlFlowGraphNode> visited)
        {
            if (visited == null)
            {
                visited = new HashSet<ControlFlowGraphNode> { this };
            }
            else if (visited.Contains(this))
            {
                return;
            }
            else
            {
                visited.Add(this);
            }

            Console.Write("... | ... cfg '{0}': ", this.Id);
            foreach (var node in this.ISuccessors)
            {
                Console.Write(" '{0}'", node.Id);
            }

            Console.WriteLine("");
            foreach (var node in this.ISuccessors)
            {
                node.PrintCFGSuccessors(visited);
            }
        }

        /// <summary>
        /// Prints the control-flow graph predecessor nodes.
        /// </summary>
        /// <param name="visited">Set of visited CFG nodes</param>
        private void PrintCFGPredecessors(HashSet<ControlFlowGraphNode> visited)
        {
            if (visited == null)
            {
                visited = new HashSet<ControlFlowGraphNode> { this };
            }
            else if (visited.Contains(this))
            {
                return;
            }
            else
            {
                visited.Add(this);
            }

            Console.Write("... | ... cfg '{0}': ", this.Id);
            foreach (var node in this.IPredecessors)
            {
                Console.Write(" '{0}'", node.Id);
            }

            Console.WriteLine("");
            foreach (var node in this.ISuccessors)
            {
                node.PrintCFGPredecessors(visited);
            }
        }

        #endregion
    }
}
