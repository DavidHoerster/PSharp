﻿//-----------------------------------------------------------------------
// <copyright file="RespectsOwnershipAnalysisPass.cs">
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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.DataFlowAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.PSharp.StaticAnalysis
{
    /// <summary>
    /// This analysis checks that all methods in each machine of a P#
    /// program respect given-up ownerships.
    /// </summary>
    internal sealed class RespectsOwnershipAnalysisPass : OwnershipAnalysisPass
    {
        #region internal API

        /// <summary>
        /// Creates a new respects ownership analysis pass.
        /// </summary>
        /// <param name="context">AnalysisContext</param>
        /// <returns>RespectsOwnershipAnalysisPass</returns>
        internal static RespectsOwnershipAnalysisPass Create(PSharpAnalysisContext context)
        {
            return new RespectsOwnershipAnalysisPass(context);
        }

        #endregion

        #region protected methods

        /// <summary>
        /// Analyzes the ownership of the given-up symbol
        /// in the control-flow graph.
        /// </summary>
        /// <param name="givenUpSymbol">GivenUpOwnershipSymbol</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        protected override void AnalyzeOwnershipInControlFlowGraph(GivenUpOwnershipSymbol givenUpSymbol,
            StateMachine originalMachine, SemanticModel model, TraceInfo trace)
        {
            var queue = new Queue<IControlFlowNode>();
            queue.Enqueue(givenUpSymbol.Statement.ControlFlowNode);

            var visitedNodes = new HashSet<IControlFlowNode>();
            visitedNodes.Add(givenUpSymbol.Statement.ControlFlowNode);

            bool repeatGivesUpNode = false;
            while (queue.Count > 0)
            {
                IControlFlowNode node = queue.Dequeue();
                
                var statements = new List<Statement>();
                if (!repeatGivesUpNode &&
                    node.Equals(givenUpSymbol.Statement.ControlFlowNode))
                {
                    statements.AddRange(node.Statements.SkipWhile(
                        val => !val.Equals(givenUpSymbol.Statement)));
                }
                else
                {
                    statements.AddRange(node.Statements);
                }

                foreach (var statement in statements)
                {
                    base.AnalyzeOwnershipInStatement(givenUpSymbol, statement,
                        originalMachine, model, trace);
                }

                foreach (var successor in node.ISuccessors)
                {
                    if (!repeatGivesUpNode &&
                        successor.Equals(givenUpSymbol.Statement.ControlFlowNode))
                    {
                        repeatGivesUpNode = true;
                        visitedNodes.Remove(givenUpSymbol.Statement.ControlFlowNode);
                    }

                    if (!visitedNodes.Contains(successor))
                    {
                        queue.Enqueue(successor);
                        visitedNodes.Add(successor);
                    }
                }
            }
        }

        /// <summary>
        /// Analyzes the ownership of the given-up symbol
        /// in the variable declaration.
        /// </summary>
        /// <param name="givenUpSymbol">GivenUpOwnershipSymbol</param>
        /// <param name="varDecl">VariableDeclarationSyntax</param>
        /// <param name="statement">Statement</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        protected override void AnalyzeOwnershipInLocalDeclaration(GivenUpOwnershipSymbol givenUpSymbol,
            VariableDeclarationSyntax varDecl, Statement statement, StateMachine originalMachine,
            SemanticModel model, TraceInfo trace)
        {
            foreach (var variable in varDecl.Variables.Where(v => v.Initializer != null))
            {
                this.AnalyzeOwnershipInExpression(givenUpSymbol, variable.Initializer.Value,
                    statement, originalMachine, model, trace);
            }
        }

        /// <summary>
        /// Analyzes the ownership of the given-up symbol
        /// in the assignment expression.
        /// </summary>
        /// <param name="givenUpSymbol">GivenUpOwnershipSymbol</param>
        /// <param name="assignment">AssignmentExpressionSyntax</param>
        /// <param name="statement">Statement</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        protected override void AnalyzeOwnershipInAssignment(GivenUpOwnershipSymbol givenUpSymbol,
            AssignmentExpressionSyntax assignment, Statement statement, StateMachine originalMachine,
            SemanticModel model, TraceInfo trace)
        {
            var leftIdentifier = CodeAnalysis.CSharp.DataFlowAnalysis.AnalysisContext.
                GetTopLevelIdentifier(assignment.Left);
            ISymbol leftSymbol = model.GetSymbolInfo(leftIdentifier).Symbol;
            
            if (assignment.Right is IdentifierNameSyntax)
            {
                var rightIdentifier = CodeAnalysis.CSharp.DataFlowAnalysis.AnalysisContext.
                    GetTopLevelIdentifier(assignment.Right);
                ISymbol rightSymbol = model.GetSymbolInfo(rightIdentifier).Symbol;

                if (statement.Summary.DataFlowAnalysis.FlowsIntoSymbol(rightSymbol,
                    givenUpSymbol.ContainingSymbol, statement, givenUpSymbol.Statement))
                {
                    var type = model.GetTypeInfo(assignment.Right).Type;
                    if (leftSymbol != null && leftSymbol.Kind == SymbolKind.Field &&
                        base.IsFieldAccessedBeforeBeingReset(leftSymbol, statement.Summary) &&
                        !base.AnalysisContext.IsTypePassedByValueOrImmutable(type) &&
                        !base.AnalysisContext.IsTypeEnum(type))
                    {
                        TraceInfo newTrace = new TraceInfo();
                        newTrace.Merge(trace);
                        newTrace.AddErrorTrace(statement.SyntaxNode);

                        AnalysisErrorReporter.ReportGivenUpOwnershipFieldAssignment(newTrace, leftSymbol);
                    }

                    return;
                }
            }
            else if (assignment.Right is MemberAccessExpressionSyntax ||
                assignment.Right is InvocationExpressionSyntax ||
                assignment.Right is ObjectCreationExpressionSyntax)
            {
                this.AnalyzeOwnershipInExpression(givenUpSymbol, assignment.Right,
                    statement, originalMachine, model, trace);
            }

            if (assignment.Left is MemberAccessExpressionSyntax)
            {
                ISymbol outerLeftMemberSymbol = model.GetSymbolInfo(assignment.Left).Symbol;
                if (!outerLeftMemberSymbol.Equals(leftSymbol) &&
                    //!DataFlowQuerying.DoesResetInControlFlowGraphNode(leftSymbol, syntaxNode, cfgNode) &&
                    statement.Summary.DataFlowAnalysis.FlowsIntoSymbol(givenUpSymbol.ContainingSymbol,
                    leftSymbol, givenUpSymbol.Statement, statement))
                {
                    TraceInfo newTrace = new TraceInfo();
                    newTrace.Merge(trace);
                    newTrace.AddErrorTrace(statement.SyntaxNode);

                    AnalysisErrorReporter.ReportGivenUpOwnershipAccess(newTrace);
                }
            }
        }

        /// <summary>
        /// Analyzes the ownership of the given-up symbol
        /// in the candidate callee.
        /// </summary>
        /// <param name="givenUpSymbol">GivenUpOwnershipSymbol</param>
        /// <param name="calleeSummary">MethodSummary</param>
        /// <param name="call">ExpressionSyntax</param>
        /// <param name="statement">Statement</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        protected override void AnalyzeOwnershipInCandidateCallee(GivenUpOwnershipSymbol givenUpSymbol,
            MethodSummary calleeSummary, ExpressionSyntax call, Statement statement,
            StateMachine originalMachine, SemanticModel model, TraceInfo trace)
        {
            if (statement.Equals(givenUpSymbol.Statement) &&
                !statement.ControlFlowNode.IsSuccessorOf(
                    givenUpSymbol.Statement.ControlFlowNode))
            {
                return;
            }

            var invocation = call as InvocationExpressionSyntax;
            ArgumentListSyntax argumentList = base.AnalysisContext.GetArgumentList(call);
            if (argumentList == null)
            {
                return;
            }

            for (int idx = 0; idx < argumentList.Arguments.Count; idx++)
            {
                var argType = model.GetTypeInfo(argumentList.Arguments[idx].Expression).Type;
                if (base.AnalysisContext.IsTypePassedByValueOrImmutable(argType) ||
                    base.AnalysisContext.IsTypeEnum(argType))
                {
                    continue;
                }
                
                var argIdentifier = CodeAnalysis.CSharp.DataFlowAnalysis.AnalysisContext.
                    GetTopLevelIdentifier(argumentList.Arguments[idx].Expression);
                ISymbol argSymbol = model.GetSymbolInfo(argIdentifier).Symbol;

                if (statement.Summary.DataFlowAnalysis.FlowsIntoSymbol(argSymbol,
                    givenUpSymbol.ContainingSymbol, statement, givenUpSymbol.Statement) /*&&
                    !DataFlowQuerying.DoesResetInLoop(argumentList.Arguments[idx].Expression,
                    syntaxNode, cfgNode, givenUpSymbol.SyntaxNode, givenUpSymbol.ControlFlowNode, model)*/)
                {
                    if (calleeSummary.SideEffectsInfo.ParameterAccesses.ContainsKey(idx))
                    {
                        foreach (var access in calleeSummary.SideEffectsInfo.ParameterAccesses[idx])
                        {
                            TraceInfo newTrace = new TraceInfo();
                            newTrace.Merge(trace);
                            newTrace.AddErrorTrace(access.SyntaxNode);

                            AnalysisErrorReporter.ReportGivenUpOwnershipAccess(newTrace);
                        }
                    }

                    var fieldSymbols = calleeSummary.SideEffectsInfo.FieldFlowParamIndexes.Where(
                        v => v.Value.Contains(idx)).Select(v => v.Key);
                    foreach (var fieldSymbol in fieldSymbols)
                    {
                        if (base.AnalysisContext.DoesFieldBelongToMachine(fieldSymbol, statement.Summary))
                        {
                            if (base.IsFieldAccessedBeforeBeingReset(fieldSymbol, statement.Summary))
                            {
                                AnalysisErrorReporter.ReportGivenUpOwnershipFieldAssignment(trace, fieldSymbol);
                            }
                        }
                        else
                        {
                            AnalysisErrorReporter.ReportGivenUpOwnershipFieldAssignment(trace, fieldSymbol);
                        }
                    }

                    if (calleeSummary.SideEffectsInfo.GivesUpOwnershipParamIndexes.Contains(idx))
                    {
                        AnalysisErrorReporter.ReportGivenUpOwnershipSending(trace, argSymbol);
                    }
                }
            }

            foreach (var fieldAccess in calleeSummary.SideEffectsInfo.FieldAccesses)
            {
                foreach (var access in fieldAccess.Value)
                {
                    if (statement.Summary.DataFlowAnalysis.FlowsIntoSymbol(givenUpSymbol.ContainingSymbol,
                        fieldAccess.Key, givenUpSymbol.Statement, statement))
                    {
                        TraceInfo newTrace = new TraceInfo();
                        newTrace.Merge(trace);
                        newTrace.AddErrorTrace(access.SyntaxNode);

                        AnalysisErrorReporter.ReportGivenUpOwnershipAccess(newTrace);
                    }
                }
            }
        }

        /// <summary>
        /// Analyzes the ownership of the given-up symbol
        /// in the gives-up operation.
        /// </summary>
        /// <param name="givenUpSymbol">GivenUpOwnershipSymbol</param>
        /// <param name="call">Gives-up call</param>
        /// <param name="statement">Statement</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        protected override void AnalyzeOwnershipInGivesUpCall(GivenUpOwnershipSymbol givenUpSymbol,
            InvocationExpressionSyntax call, Statement statement, SemanticModel model, TraceInfo trace)
        {
            if (statement.Equals(givenUpSymbol.Statement) &&
                !statement.ControlFlowNode.IsSuccessorOf(
                    givenUpSymbol.Statement.ControlFlowNode))
            {
                return;
            }

            var opSymbol = model.GetSymbolInfo(call).Symbol;
            if ((!opSymbol.Name.Equals("Send") &&
                !opSymbol.Name.Equals("CreateMachine")) ||
                (opSymbol.Name.Equals("CreateMachine") &&
                call.ArgumentList.Arguments.Count != 2))
            {
                return;
            }

            ExpressionSyntax argExpr = call.ArgumentList.Arguments[1].Expression;
            var arguments = new List<ExpressionSyntax>();

            if (argExpr is ObjectCreationExpressionSyntax)
            {
                var objCreation = argExpr as ObjectCreationExpressionSyntax;
                foreach (var arg in objCreation.ArgumentList.Arguments)
                {
                    arguments.Add(arg.Expression);
                }
            }
            else if (argExpr is BinaryExpressionSyntax &&
                argExpr.IsKind(SyntaxKind.AsExpression))
            {
                var binExpr = argExpr as BinaryExpressionSyntax;
                if (binExpr.Left is IdentifierNameSyntax ||
                    binExpr.Left is MemberAccessExpressionSyntax)
                {
                    arguments.Add(binExpr.Left);
                }
                else if (binExpr.Left is InvocationExpressionSyntax)
                {
                    var invocation = binExpr.Left as InvocationExpressionSyntax;
                    for (int i = 1; i < invocation.ArgumentList.Arguments.Count; i++)
                    {
                        arguments.Add(invocation.ArgumentList.Arguments[i].Expression);
                    }
                }
            }
            else if (argExpr is IdentifierNameSyntax ||
                argExpr is MemberAccessExpressionSyntax)
            {
                arguments.Add(argExpr);
            }

            var extractedArgs = base.ExtractArguments(arguments);
            foreach (var arg in extractedArgs)
            {
                IdentifierNameSyntax argIdentifier = CodeAnalysis.CSharp.DataFlowAnalysis.
                    AnalysisContext.GetTopLevelIdentifier(arg);
                ISymbol argSymbol = model.GetSymbolInfo(argIdentifier).Symbol;
                ITypeSymbol argType = model.GetTypeInfo(arg).Type;

                if (!base.AnalysisContext.IsTypeEnum(argType) &&
                    statement.Summary.DataFlowAnalysis.FlowsIntoSymbol(argSymbol,
                    givenUpSymbol.ContainingSymbol, statement, givenUpSymbol.Statement) /*&&
                    !DataFlowQuerying.DoesResetInLoop(arg, syntaxNode, cfgNode,
                    givenUpSymbol.SyntaxNode, givenUpSymbol.ControlFlowNode, model)*/)
                {
                    AnalysisErrorReporter.ReportGivenUpOwnershipSending(trace, argSymbol);
                    return;
                }
            }
        }

        #endregion

        #region private methods

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">AnalysisContext</param>
        private RespectsOwnershipAnalysisPass(PSharpAnalysisContext context)
            : base(context)
        {

        }

        /// <summary>
        /// Analyzes the ownership of the given-up symbol
        /// in the expression.
        /// </summary>
        /// <param name="givenUpSymbol">GivenUpOwnershipSymbol</param>
        /// <param name="expr">ExpressionSyntax</param>
        /// <param name="statement">Statement</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        private void AnalyzeOwnershipInExpression(GivenUpOwnershipSymbol givenUpSymbol,
            ExpressionSyntax expr, Statement statement, StateMachine originalMachine,
            SemanticModel model, TraceInfo trace)
        {
            if (expr is MemberAccessExpressionSyntax)
            {
                var identifier = CodeAnalysis.CSharp.DataFlowAnalysis.AnalysisContext.
                    GetTopLevelIdentifier(expr);
                ISymbol symbol = model.GetSymbolInfo(identifier).Symbol;
                if (statement.Summary.DataFlowAnalysis.FlowsIntoSymbol(symbol,
                    givenUpSymbol.ContainingSymbol, statement, givenUpSymbol.Statement))
                {
                    TraceInfo newTrace = new TraceInfo();
                    newTrace.Merge(trace);
                    newTrace.AddErrorTrace(statement.SyntaxNode);
                    
                    AnalysisErrorReporter.ReportGivenUpOwnershipAccess(newTrace);
                }
            }
            else if (expr is InvocationExpressionSyntax ||
                expr is ObjectCreationExpressionSyntax)
            {
                trace.InsertCall(statement.Summary.Method, expr);
                base.AnalyzeOwnershipInCall(givenUpSymbol, expr, statement,
                    originalMachine, model, trace);
            }
        }

        #endregion
    }
}
