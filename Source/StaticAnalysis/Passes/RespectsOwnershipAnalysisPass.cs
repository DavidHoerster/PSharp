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
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp.StaticAnalysis
{
    /// <summary>
    /// This analysis checks that all methods in each machine of a P#
    /// program respect given up ownerships. The analysis uses data
    /// computed by the 'GivesUpOwnershipAnalysis'.
    /// 
    /// In more detail:
    /// 
    /// - The analysis checks that no object can be accessed by a method
    /// after the call to another method, if the callee has given up
    /// ownership for that object.
    /// 
    /// - The ananlysis is performed in a modular fashion and does not
    /// focus specifically on 'send' operations, but more generally on
    /// operations that give up ownership.
    /// </summary>
    public sealed class RespectsOwnershipAnalysisPass : AnalysisPass
    {
        #region public API

        /// <summary>
        /// Creates a new respects ownership analysis pass.
        /// </summary>
        /// <param name="context">AnalysisContext</param>
        /// <returns>RespectsOwnershipAnalysisPass</returns>
        public static RespectsOwnershipAnalysisPass Create(AnalysisContext context)
        {
            return new RespectsOwnershipAnalysisPass(context);
        }

        /// <summary>
        /// Runs the analysis.
        /// </summary>
        /// <returns>RespectsOwnershipAnalysisPass</returns>
        public RespectsOwnershipAnalysisPass Run()
        {
            // Starts profiling the data-flow analysis.
            if (this.AnalysisContext.Configuration.ShowROARuntimeResults &&
                !this.AnalysisContext.Configuration.ShowRuntimeResults &&
                !this.AnalysisContext.Configuration.ShowDFARuntimeResults)
            {
                Profiler.StartMeasuringExecutionTime();
            }

            foreach (var machine in this.AnalysisContext.Machines)
            {
                this.AnalyseMethodsInMachine(machine);
            }

            // Stops profiling the data-flow analysis.
            if (this.AnalysisContext.Configuration.ShowROARuntimeResults &&
                !this.AnalysisContext.Configuration.ShowRuntimeResults &&
                !this.AnalysisContext.Configuration.ShowDFARuntimeResults)
            {
                Profiler.StopMeasuringExecutionTime();
            }

            return this;
        }

        #endregion

        #region private methods

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">AnalysisContext</param>
        private RespectsOwnershipAnalysisPass(AnalysisContext context)
            : base(context)
        {

        }

        /// <summary>
        /// Analyses the methods of the given machine to check if each method
        /// respects given up ownerships.
        /// </summary>
        /// <param name="machine">Machine</param>
        /// <param name="machineToAnalyse">Machine to analyse</param>
        private void AnalyseMethodsInMachine(ClassDeclarationSyntax machine,
            ClassDeclarationSyntax machineToAnalyse = null)
        {
            if (machineToAnalyse == null)
            {
                machineToAnalyse = machine;
            }

            foreach (var method in machineToAnalyse.ChildNodes().OfType<MethodDeclarationSyntax>())
            {
                if (this.AnalysisContext.IsEntryPointMethod(method, machineToAnalyse) &&
                    !method.Modifiers.Any(SyntaxKind.AbstractKeyword))
                {
                    this.AnalyseMethod(method, machineToAnalyse, null, machine);
                }
            }

            if (this.AnalysisContext.MachineInheritance.ContainsKey(machineToAnalyse))
            {
                this.AnalyseMethodsInMachine(machine, this.AnalysisContext.
                    MachineInheritance[machineToAnalyse]);
            }
        }

        /// <summary>
        /// Analyses the given method to check if it respects the given up
        /// ownerships.
        /// </summary>
        /// <param name="method">Method</param>
        /// <param name="machine">Machine</param>
        /// <param name="state">State</param>
        /// <param name="originalMachine">Original machine</param>
        private void AnalyseMethod(MethodDeclarationSyntax method, ClassDeclarationSyntax machine,
            ClassDeclarationSyntax state, ClassDeclarationSyntax originalMachine)
        {
            MethodSummary summary = MethodSummary.Create(this.AnalysisContext, method);
            foreach (var givesUpNode in summary.GivesUpOwnershipNodes)
            {
                var givesUpSource = givesUpNode.SyntaxNodes[0].DescendantNodesAndSelf().
                    OfType<InvocationExpressionSyntax>().First();
                this.AnalyseSendExpression(givesUpSource, givesUpNode,
                    machine, state, originalMachine);
                this.AnalyseCreateExpression(givesUpSource, givesUpNode,
                    machine, state, originalMachine);
                this.AnalyseGenericCallExpression(givesUpSource, givesUpNode,
                    machine, state, originalMachine);
            }
        }

        #endregion

        #region give up ownership source analysis methods

        /// <summary>
        /// Analyse the given 'Send' expression to check if it respects the
        /// given up ownerships.
        /// </summary>
        /// <param name="send">Send call</param>
        /// <param name="givesUpNode">Gives up node</param>
        /// <param name="machine">Machine</param>
        /// <param name="state">State</param>
        /// <param name="originalMachine">Original machine</param>
        private void AnalyseSendExpression(InvocationExpressionSyntax send, ControlFlowGraphNode givesUpNode,
            ClassDeclarationSyntax machine, ClassDeclarationSyntax state, ClassDeclarationSyntax originalMachine)
        {
            if (!((send.Expression is MemberAccessExpressionSyntax) ||
                (send.Expression is IdentifierNameSyntax)))
            {
                return;
            }

            if (((send.Expression is MemberAccessExpressionSyntax) &&
                !(send.Expression as MemberAccessExpressionSyntax).
                Name.Identifier.ValueText.Equals("Send")) ||
                ((send.Expression is IdentifierNameSyntax) &&
                !(send.Expression as IdentifierNameSyntax).
                Identifier.ValueText.Equals("Send")))
            {
                return;
            }

            if (send.ArgumentList.Arguments[1].Expression is ObjectCreationExpressionSyntax)
            {
                var objCreation = send.ArgumentList.Arguments[1].Expression
                    as ObjectCreationExpressionSyntax;
                foreach (var arg in objCreation.ArgumentList.Arguments)
                {
                    this.AnalyseArgumentSyntax(arg.Expression,
                        send, givesUpNode, machine, state, originalMachine);
                }
            }
            else if (send.ArgumentList.Arguments[1].Expression is BinaryExpressionSyntax &&
                send.ArgumentList.Arguments[1].Expression.IsKind(SyntaxKind.AsExpression))
            {
                var binExpr = send.ArgumentList.Arguments[1].Expression
                    as BinaryExpressionSyntax;
                if ((binExpr.Left is IdentifierNameSyntax) || (binExpr.Left is MemberAccessExpressionSyntax))
                {
                    this.AnalyseArgumentSyntax(binExpr.Left,
                        send, givesUpNode, machine, state, originalMachine);
                }
                else if (binExpr.Left is InvocationExpressionSyntax)
                {
                    var invocation = binExpr.Left as InvocationExpressionSyntax;
                    for (int i = 1; i < invocation.ArgumentList.Arguments.Count; i++)
                    {
                        this.AnalyseArgumentSyntax(invocation.ArgumentList.Arguments[i].
                            Expression, send, givesUpNode, machine, state, originalMachine);
                    }
                }
            }
        }

        /// <summary>
        /// Analyse the given 'Create' expression to check if it respects the
        /// given up ownerships.
        /// </summary>
        /// <param name="send">Create call</param>
        /// <param name="givesUpNode">Gives up node</param>
        /// <param name="machine">Machine</param>
        /// <param name="state">State</param>
        /// <param name="originalMachine">Original machine</param>
        private void AnalyseCreateExpression(InvocationExpressionSyntax create, ControlFlowGraphNode givesUpNode,
            ClassDeclarationSyntax machine, ClassDeclarationSyntax state, ClassDeclarationSyntax originalMachine)
        {
            if (!((create.Expression is MemberAccessExpressionSyntax) ||
                (create.Expression is IdentifierNameSyntax)))
            {
                return;
            }

            if (((create.Expression is MemberAccessExpressionSyntax) &&
                !(create.Expression as MemberAccessExpressionSyntax).
                Name.Identifier.ValueText.Equals("CreateMachine")) ||
                ((create.Expression is IdentifierNameSyntax) &&
                !(create.Expression as IdentifierNameSyntax).
                Identifier.ValueText.Equals("CreateMachine")))
            {
                return;
            }

            if (create.ArgumentList.Arguments.Count == 0)
            {
                return;
            }

            if (create.ArgumentList.Arguments[0].Expression is ObjectCreationExpressionSyntax)
            {
                var objCreation = create.ArgumentList.Arguments[0].Expression
                    as ObjectCreationExpressionSyntax;
                foreach (var arg in objCreation.ArgumentList.Arguments)
                {
                    this.AnalyseArgumentSyntax(arg.Expression,
                        create, givesUpNode, machine, state, originalMachine);
                }
            }
            else if (create.ArgumentList.Arguments[0].Expression is BinaryExpressionSyntax &&
                create.ArgumentList.Arguments[0].Expression.IsKind(SyntaxKind.AsExpression))
            {
                var binExpr = create.ArgumentList.Arguments[0].Expression
                    as BinaryExpressionSyntax;
                if ((binExpr.Left is IdentifierNameSyntax) || (binExpr.Left is MemberAccessExpressionSyntax))
                {
                    this.AnalyseArgumentSyntax(binExpr.Left,
                        create, givesUpNode, machine, state, originalMachine);
                }
                else if (binExpr.Left is InvocationExpressionSyntax)
                {
                    var invocation = binExpr.Left as InvocationExpressionSyntax;
                    for (int i = 1; i < invocation.ArgumentList.Arguments.Count; i++)
                    {
                        this.AnalyseArgumentSyntax(invocation.ArgumentList.Arguments[i].
                            Expression, create, givesUpNode, machine, state, originalMachine);
                    }
                }
            }
            else if ((create.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax) ||
                (create.ArgumentList.Arguments[0].Expression is MemberAccessExpressionSyntax))
            {
                this.AnalyseArgumentSyntax(create.ArgumentList.Arguments[0].
                    Expression, create, givesUpNode, machine, state, originalMachine);
            }
        }

        /// <summary>
        /// Analyse the given call expression to check if it respects the
        /// given up ownerships.
        /// </summary>
        /// <param name="call">Call</param>
        /// <param name="givesUpNode">Gives up node</param>
        /// <param name="machine">Machine</param>
        /// <param name="state">State</param>
        /// <param name="originalMachine">Original machine</param>
        private void AnalyseGenericCallExpression(InvocationExpressionSyntax call, ControlFlowGraphNode givesUpNode,
            ClassDeclarationSyntax machine, ClassDeclarationSyntax state, ClassDeclarationSyntax originalMachine)
        {
            if (!((call.Expression is MemberAccessExpressionSyntax) ||
                (call.Expression is IdentifierNameSyntax)))
            {
                return;
            }

            var model = this.AnalysisContext.Compilation.GetSemanticModel(call.SyntaxTree);

            if (call.Expression is MemberAccessExpressionSyntax)
            {
                var callStmt = call.Expression as MemberAccessExpressionSyntax;
                if (callStmt.Name.Identifier.ValueText.Equals("Send") ||
                    callStmt.Name.Identifier.ValueText.Equals("CreateMachine"))
                {
                    return;
                }
            }
            else if (call.Expression is IdentifierNameSyntax)
            {
                var callStmt = call.Expression as IdentifierNameSyntax;
                if (callStmt.Identifier.ValueText.Equals("Send") ||
                    callStmt.Identifier.ValueText.Equals("CreateMachine"))
                {
                    return;
                }
            }

            if (call.ArgumentList.Arguments.Count == 0)
            {
                return;
            }

            var callSymbol = model.GetSymbolInfo(call).Symbol;
            var definition = SymbolFinder.FindSourceDefinitionAsync(callSymbol,
                this.AnalysisContext.Solution).Result;
            var calleeMethod = definition.DeclaringSyntaxReferences.First().GetSyntax()
                as BaseMethodDeclarationSyntax;
            var calleeSummary = MethodSummary.Create(this.AnalysisContext, calleeMethod);

            foreach (int idx in calleeSummary.GivesUpSet)
            {
                if (call.ArgumentList.Arguments[idx].Expression is ObjectCreationExpressionSyntax)
                {
                    var objCreation = call.ArgumentList.Arguments[idx].Expression
                        as ObjectCreationExpressionSyntax;
                    foreach (var arg in objCreation.ArgumentList.Arguments)
                    {
                        this.AnalyseArgumentSyntax(arg.Expression,
                            call, givesUpNode, machine, state, originalMachine);
                    }
                }
                else if (call.ArgumentList.Arguments[idx].Expression is BinaryExpressionSyntax &&
                    call.ArgumentList.Arguments[idx].Expression.IsKind(SyntaxKind.AsExpression))
                {
                    var binExpr = call.ArgumentList.Arguments[idx].Expression
                        as BinaryExpressionSyntax;
                    if ((binExpr.Left is IdentifierNameSyntax) || (binExpr.Left is MemberAccessExpressionSyntax))
                    {
                        this.AnalyseArgumentSyntax(binExpr.Left,
                            call, givesUpNode, machine, state, originalMachine);
                    }
                    else if (binExpr.Left is InvocationExpressionSyntax)
                    {
                        var invocation = binExpr.Left as InvocationExpressionSyntax;
                        for (int i = 1; i < invocation.ArgumentList.Arguments.Count; i++)
                        {
                            this.AnalyseArgumentSyntax(invocation.ArgumentList.Arguments[i].
                                Expression, call, givesUpNode, machine, state, originalMachine);
                        }
                    }
                }
                else if ((call.ArgumentList.Arguments[idx].Expression is IdentifierNameSyntax) ||
                    (call.ArgumentList.Arguments[idx].Expression is MemberAccessExpressionSyntax))
                {
                    this.AnalyseArgumentSyntax(call.ArgumentList.Arguments[idx].
                        Expression, call, givesUpNode, machine, state, originalMachine);
                }
            }
        }

        /// <summary>
        /// Analyse the given argument to see if it respects the given up ownerships.
        /// </summary>
        /// <param name="arg">Argument</param>
        /// <param name="call">Call</param>
        /// <param name="givesUpNode">Gives up node</param>
        /// <param name="machine">Machine</param>
        /// <param name="state">State</param>
        /// <param name="originalMachine">Original machine</param>
        private void AnalyseArgumentSyntax(ExpressionSyntax arg, InvocationExpressionSyntax call,
            ControlFlowGraphNode givesUpNode, ClassDeclarationSyntax machine, ClassDeclarationSyntax state,
            ClassDeclarationSyntax originalMachine)
        {
            var model = this.AnalysisContext.Compilation.GetSemanticModel(arg.SyntaxTree);
            
            if (arg is MemberAccessExpressionSyntax || arg is IdentifierNameSyntax)
            {
                TraceInfo trace = new TraceInfo(givesUpNode.Summary.Method as MethodDeclarationSyntax, machine, state, arg);
                trace.AddErrorTrace(call.ToString(), call.SyntaxTree.FilePath, call.SyntaxTree.
                    GetLineSpan(call.Span).StartLinePosition.Line + 1);

                if (this.IsArgumentSafeToAccess(arg, givesUpNode, model, trace, true))
                {
                    return;
                }
                
                ISymbol argSymbol = null;
                if (arg is IdentifierNameSyntax)
                {
                    argSymbol = model.GetSymbolInfo(arg as IdentifierNameSyntax).Symbol;
                }
                else if (arg is MemberAccessExpressionSyntax)
                {
                    argSymbol = model.GetSymbolInfo((arg as MemberAccessExpressionSyntax).Name).Symbol;
                }
                
                this.DetectGivenUpFieldOwnershipInControlFlowGraph(givesUpNode,
                    givesUpNode, argSymbol, call, new HashSet<ControlFlowGraphNode>(),
                    originalMachine, model, trace);
                this.DetectPotentialDataRacesInControlFlowGraph(givesUpNode,
                    givesUpNode, argSymbol, call, new HashSet<ControlFlowGraphNode>(),
                    originalMachine, model, trace);

                var aliases = DataFlowQuerying.GetAliases(argSymbol, givesUpNode.SyntaxNodes.First(), givesUpNode);
                foreach (var alias in aliases)
                {
                    trace.Payload = alias.Name;
                    this.DetectGivenUpFieldOwnershipInControlFlowGraph(givesUpNode,
                        givesUpNode, alias, call, new HashSet<ControlFlowGraphNode>(),
                        originalMachine, model, trace);
                    this.DetectPotentialDataRacesInControlFlowGraph(givesUpNode,
                        givesUpNode, alias, call, new HashSet<ControlFlowGraphNode>(),
                        originalMachine, model, trace);
                }
            }
            else if (arg is ObjectCreationExpressionSyntax)
            {
                var payload = arg as ObjectCreationExpressionSyntax;
                foreach (var item in payload.ArgumentList.Arguments)
                {
                    this.AnalyseArgumentSyntax(item.Expression,
                        call, givesUpNode, machine, state, originalMachine);
                }
            }
        }

        #endregion

        #region predecessor analysis methods

        /// <summary>
        /// Analyses the given control-flow graph node to find if it gives up ownership of
        /// data from a machine field.
        /// </summary>
        /// <param name="cfgNode">Control flow graph node</param>
        /// <param name="givesUpNode">Gives up node</param>
        /// <param name="target">Target</param>
        /// <param name="giveUpSource">Give up source</param>
        /// <param name="visited">Already visited cfgNodes</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        private void DetectGivenUpFieldOwnershipInControlFlowGraph(ControlFlowGraphNode cfgNode,
            ControlFlowGraphNode givesUpNode, ISymbol target, InvocationExpressionSyntax giveUpSource,
            HashSet<ControlFlowGraphNode> visited, ClassDeclarationSyntax originalMachine,
            SemanticModel model, TraceInfo trace)
        {
            if (!cfgNode.IsJumpNode && !cfgNode.IsLoopHeadNode &&
                visited.Contains(givesUpNode))
            {
                foreach (var syntaxNode in cfgNode.SyntaxNodes)
                {
                    var stmt = syntaxNode as StatementSyntax;
                    var localDecl = stmt.DescendantNodesAndSelf().OfType<LocalDeclarationStatementSyntax>().FirstOrDefault();
                    var expr = stmt.DescendantNodesAndSelf().OfType<ExpressionStatementSyntax>().FirstOrDefault();

                    if (localDecl != null)
                    {
                        var varDecl = localDecl.Declaration;
                        foreach (var variable in varDecl.Variables.Where(v => v.Initializer != null))
                        {
                            var rightSymbols = new HashSet<ISymbol>();
                            if (variable.Initializer.Value is IdentifierNameSyntax ||
                                variable.Initializer.Value is MemberAccessExpressionSyntax)
                            {
                                if (DataFlowQuerying.FlowsIntoTarget(variable, target, syntaxNode, cfgNode,
                                    givesUpNode.SyntaxNodes.First(), givesUpNode, model))
                                {
                                    IdentifierNameSyntax identifier = null;
                                    if (variable.Initializer.Value is IdentifierNameSyntax)
                                    {
                                        identifier = variable.Initializer.Value as IdentifierNameSyntax;
                                    }
                                    else if (variable.Initializer.Value is MemberAccessExpressionSyntax)
                                    {
                                        identifier = this.AnalysisContext.GetFirstNonMachineIdentifier(
                                            variable.Initializer.Value, model);
                                    }

                                    if (identifier != null)
                                    {
                                        var rightSymbol = model.GetSymbolInfo(identifier).Symbol;
                                        rightSymbols.Add(rightSymbol);
                                    }
                                }
                            }
                            else if (variable.Initializer.Value is InvocationExpressionSyntax)
                            {
                                var invocation = variable.Initializer.Value as InvocationExpressionSyntax;
                                trace.InsertCall(cfgNode.Summary.Method, invocation);
                                var returnSymbols = this.DetectGivenUpFieldOwnershipInInvocation(
                                    invocation, target, syntaxNode, cfgNode, givesUpNode.SyntaxNodes.First(),
                                    givesUpNode, originalMachine, model, trace);

                                if (DataFlowQuerying.FlowsIntoTarget(variable, target, syntaxNode, cfgNode,
                                    givesUpNode.SyntaxNodes.First(), givesUpNode, model))
                                {
                                    rightSymbols = returnSymbols;
                                }
                            }
                            else if (variable.Initializer.Value is ObjectCreationExpressionSyntax)
                            {
                                var objCreation = variable.Initializer.Value as ObjectCreationExpressionSyntax;
                                trace.InsertCall(cfgNode.Summary.Method, objCreation);
                                var returnSymbols = this.DetectGivenUpFieldOwnershipInObjectCreation(
                                    objCreation, target, syntaxNode, cfgNode, givesUpNode.SyntaxNodes.First(),
                                    givesUpNode, model, trace);

                                if (DataFlowQuerying.FlowsIntoTarget(variable, target, syntaxNode, cfgNode,
                                    givesUpNode.SyntaxNodes.First(), givesUpNode, model))
                                {
                                    rightSymbols = returnSymbols;
                                }
                            }

                            foreach (var rightSymbol in rightSymbols)
                            {
                                if (!rightSymbol.Equals(target) && !variable.Initializer.Value.
                                    ToString().Equals(target.ToString()))
                                {
                                    var rightDef = SymbolFinder.FindSourceDefinitionAsync(rightSymbol,
                                        this.AnalysisContext.Solution).Result;
                                    var type = model.GetTypeInfo(variable.Initializer.Value).Type;
                                    if (rightDef != null && rightDef.Kind == SymbolKind.Field &&
                                        this.AnalysisContext.DoesFieldBelongToMachine(rightDef, cfgNode.Summary) &&
                                        !this.AnalysisContext.IsTypeAllowedToBeSend(type) &&
                                        !this.AnalysisContext.IsExprEnum(variable.Initializer.Value, model) &&
                                        !DataFlowQuerying.DoesResetInSuccessorCFGNodes(rightSymbol,
                                        target, syntaxNode, cfgNode) &&
                                        FieldUsageAnalysis.IsAccessedBeforeBeingReset(rightDef,
                                        cfgNode.Summary, this.AnalysisContext))
                                    {
                                        TraceInfo newTrace = new TraceInfo();
                                        newTrace.Merge(trace);
                                        newTrace.AddErrorTrace(syntaxNode.ToString(), syntaxNode.SyntaxTree.FilePath,
                                            syntaxNode.SyntaxTree.GetLineSpan(syntaxNode.Span).StartLinePosition.Line + 1);
                                        AnalysisErrorReporter.ReportGivenUpFieldOwnershipError(newTrace);
                                    }
                                }
                            }
                        }
                    }
                    else if (expr != null)
                    {
                        if (expr.Expression is BinaryExpressionSyntax)
                        {
                            var binaryExpr = expr.Expression as BinaryExpressionSyntax;
                            if (this.IsPayloadIllegallyAccessed(binaryExpr, stmt, model, trace))
                            {
                                continue;
                            }

                            ISymbol leftSymbol = null;
                            if (binaryExpr.Left is IdentifierNameSyntax)
                            {
                                leftSymbol = model.GetSymbolInfo(binaryExpr.Left
                                    as IdentifierNameSyntax).Symbol;
                            }
                            else if (binaryExpr.Left is MemberAccessExpressionSyntax)
                            {
                                var leftIdentifier = this.AnalysisContext.GetFirstNonMachineIdentifier(
                                    binaryExpr.Left, model);
                                if (leftIdentifier != null)
                                {
                                    leftSymbol = model.GetSymbolInfo(leftIdentifier).Symbol;
                                }
                            }

                            var rightSymbols = new HashSet<ISymbol>();
                            if (binaryExpr.Right is IdentifierNameSyntax ||
                                binaryExpr.Right is MemberAccessExpressionSyntax)
                            {
                                if (DataFlowQuerying.FlowsIntoTarget(binaryExpr.Left, target, syntaxNode,
                                    cfgNode, givesUpNode.SyntaxNodes.First(), givesUpNode,
                                    model, this.AnalysisContext))
                                {
                                    IdentifierNameSyntax rightIdentifier = null;
                                    if (binaryExpr.Right is IdentifierNameSyntax)
                                    {
                                        rightIdentifier = binaryExpr.Right as IdentifierNameSyntax;
                                    }
                                    else if (binaryExpr.Right is MemberAccessExpressionSyntax)
                                    {
                                        rightIdentifier = this.AnalysisContext.GetFirstNonMachineIdentifier(
                                            binaryExpr.Right, model);
                                    }

                                    if (rightIdentifier != null)
                                    {
                                        var rightSymbol = model.GetSymbolInfo(rightIdentifier).Symbol;
                                        rightSymbols.Add(rightSymbol);
                                    }
                                }
                            }
                            else if (binaryExpr.Right is InvocationExpressionSyntax)
                            {
                                var invocation = binaryExpr.Right as InvocationExpressionSyntax;
                                trace.InsertCall(cfgNode.Summary.Method, invocation);
                                var returnSymbols = this.DetectGivenUpFieldOwnershipInInvocation(
                                    invocation, target, syntaxNode, cfgNode, givesUpNode.SyntaxNodes.First(),
                                    givesUpNode, originalMachine, model, trace);

                                if (DataFlowQuerying.FlowsIntoTarget(binaryExpr.Left, target, syntaxNode,
                                    cfgNode, givesUpNode.SyntaxNodes.First(), givesUpNode,
                                    model, this.AnalysisContext))
                                {
                                    rightSymbols = returnSymbols;
                                    if (rightSymbols.Count == 0 && leftSymbol != null)
                                    {
                                        rightSymbols.Add(leftSymbol);
                                    }
                                }
                            }
                            else if (binaryExpr.Right is ObjectCreationExpressionSyntax)
                            {
                                var objCreation = binaryExpr.Right as ObjectCreationExpressionSyntax;
                                trace.InsertCall(cfgNode.Summary.Method, objCreation);
                                var returnSymbols = this.DetectGivenUpFieldOwnershipInObjectCreation(
                                    objCreation, target, syntaxNode, cfgNode, givesUpNode.SyntaxNodes.First(),
                                    givesUpNode, model, trace);

                                if (DataFlowQuerying.FlowsIntoTarget(binaryExpr.Left, target, syntaxNode,
                                    cfgNode, givesUpNode.SyntaxNodes.First(), givesUpNode,
                                    model, this.AnalysisContext))
                                {
                                    rightSymbols = returnSymbols;
                                    if (rightSymbols.Count == 0 && leftSymbol != null)
                                    {
                                        rightSymbols.Add(leftSymbol);
                                    }
                                }
                            }

                            foreach (var rightSymbol in rightSymbols)
                            {
                                if (target.Kind == SymbolKind.Field &&
                                    rightSymbol.Equals(leftSymbol))
                                {
                                    continue;
                                }

                                var rightDef = SymbolFinder.FindSourceDefinitionAsync(rightSymbol,
                                    this.AnalysisContext.Solution).Result;
                                var rightType = model.GetTypeInfo(binaryExpr.Right).Type;
                                if (rightDef != null && rightDef.Kind == SymbolKind.Field &&
                                    this.AnalysisContext.DoesFieldBelongToMachine(rightDef, cfgNode.Summary) &&
                                    !this.AnalysisContext.IsTypeAllowedToBeSend(rightType) &&
                                    !this.AnalysisContext.IsExprEnum(binaryExpr.Right, model) &&
                                    !DataFlowQuerying.DoesResetInSuccessorCFGNodes(rightSymbol,
                                    target, syntaxNode, cfgNode) &&
                                    FieldUsageAnalysis.IsAccessedBeforeBeingReset(rightDef,
                                    cfgNode.Summary, this.AnalysisContext))
                                {
                                    TraceInfo newTrace = new TraceInfo();
                                    newTrace.Merge(trace);
                                    newTrace.AddErrorTrace(stmt.ToString(), stmt.SyntaxTree.FilePath, stmt.SyntaxTree.
                                        GetLineSpan(stmt.Span).StartLinePosition.Line + 1);
                                    AnalysisErrorReporter.ReportGivenUpFieldOwnershipError(newTrace);
                                }

                                if (leftSymbol != null && !rightSymbol.Equals(leftSymbol))
                                {
                                    if (DataFlowQuerying.FlowsIntoTarget(rightSymbol, target, syntaxNode,
                                        cfgNode, givesUpNode.SyntaxNodes.First(), givesUpNode))
                                    {
                                        var leftDef = SymbolFinder.FindSourceDefinitionAsync(leftSymbol,
                                            this.AnalysisContext.Solution).Result;
                                        var leftType = model.GetTypeInfo(binaryExpr.Left).Type;
                                        if (leftDef != null && leftDef.Kind == SymbolKind.Field &&
                                            !this.AnalysisContext.IsTypeAllowedToBeSend(leftType) &&
                                            !this.AnalysisContext.IsExprEnum(binaryExpr.Left, model) &&
                                            !DataFlowQuerying.DoesResetInSuccessorCFGNodes(leftSymbol,
                                            target, syntaxNode, cfgNode) &&
                                            FieldUsageAnalysis.IsAccessedBeforeBeingReset(leftDef,
                                            cfgNode.Summary, this.AnalysisContext))
                                        {
                                            TraceInfo newTrace = new TraceInfo();
                                            newTrace.Merge(trace);
                                            newTrace.AddErrorTrace(stmt.ToString(), stmt.SyntaxTree.FilePath, stmt.SyntaxTree.
                                                GetLineSpan(stmt.Span).StartLinePosition.Line + 1);
                                            AnalysisErrorReporter.ReportGivenUpFieldOwnershipError(newTrace);
                                        }
                                    }
                                }
                            }
                        }
                        else if (expr.Expression is InvocationExpressionSyntax)
                        {
                            var invocation = expr.Expression as InvocationExpressionSyntax;
                            trace.InsertCall(cfgNode.Summary.Method, invocation);
                            this.DetectGivenUpFieldOwnershipInInvocation(invocation,
                                target, syntaxNode, cfgNode, givesUpNode.SyntaxNodes.First(), givesUpNode,
                                originalMachine, model, trace);
                        }
                    }
                }
            }

            if (visited.Contains(cfgNode))
            {
                return;
            }

            visited.Add(cfgNode);

            if (givesUpNode != null)
            {
                foreach (var predecessor in cfgNode.IPredecessors)
                {
                    this.DetectGivenUpFieldOwnershipInControlFlowGraph(predecessor,
                        givesUpNode, target, giveUpSource, visited, originalMachine, model, trace);
                }
            }
            else
            {
                foreach (var successor in cfgNode.ISuccessors)
                {
                    this.DetectGivenUpFieldOwnershipInControlFlowGraph(successor,
                        givesUpNode, target, giveUpSource, visited, originalMachine, model, trace);
                }
            }
        }

        /// <summary>
        /// Analyses the summary of the given object creation to find if it gives up ownership
        /// of data from a machine field.
        /// </summary>
        /// <param name="call">Call</param>
        /// <param name="target">Target</param>
        /// <param name="syntaxNode">SyntaxNode</param>
        /// <param name="cfgNode">ControlFlowGraphNode</param>
        /// <param name="givesUpSyntaxNode">Gives up syntaxNode</param>
        /// <param name="givesUpCfgNode">Gives up controlFlowGraphNode</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        /// <returns>Set of return symbols</returns>
        private HashSet<ISymbol> DetectGivenUpFieldOwnershipInObjectCreation(ObjectCreationExpressionSyntax call,
            ISymbol target, SyntaxNode syntaxNode, ControlFlowGraphNode cfgNode, SyntaxNode givesUpSyntaxNode,
            ControlFlowGraphNode givesUpCfgNode, SemanticModel model, TraceInfo trace)
        {
            TraceInfo callTrace = new TraceInfo();
            callTrace.Merge(trace);
            callTrace.AddErrorTrace(call.ToString(), call.SyntaxTree.FilePath, call.SyntaxTree.
                GetLineSpan(call.Span).StartLinePosition.Line + 1);

            var callSymbol = model.GetSymbolInfo(call).Symbol;
            if (callSymbol == null)
            {
                if (call.ArgumentList != null && call.ArgumentList.Arguments.Count > 0)
                {
                    AnalysisErrorReporter.ReportUnknownInvocation(callTrace);
                }
                
                return new HashSet<ISymbol>();
            }

            var definition = SymbolFinder.FindSourceDefinitionAsync(callSymbol,
                this.AnalysisContext.Solution).Result;
            if (definition == null || definition.DeclaringSyntaxReferences.IsEmpty)
            {
                if (call.ArgumentList != null && call.ArgumentList.Arguments.Count > 0)
                {
                    AnalysisErrorReporter.ReportUnknownInvocation(callTrace);
                }

                return new HashSet<ISymbol>();
            }

            var constructorCall = definition.DeclaringSyntaxReferences.First().GetSyntax()
                as ConstructorDeclarationSyntax;
            var constructorSummary = MethodSummary.Create(this.AnalysisContext, constructorCall);
            var arguments = call.ArgumentList.Arguments;

            for (int idx = 0; idx < arguments.Count; idx++)
            {
                if (DataFlowQuerying.FlowsIntoTarget(arguments[idx].Expression, target,
                    syntaxNode, cfgNode, givesUpSyntaxNode, givesUpCfgNode,
                    model, this.AnalysisContext))
                {
                    if (constructorSummary.SideEffects.Any(
                        v => v.Value.Contains(idx) &&
                        this.AnalysisContext.DoesFieldBelongToMachine(v.Key, cfgNode.Summary) &&
                        FieldUsageAnalysis.IsAccessedBeforeBeingReset(v.Key,
                        cfgNode.Summary, this.AnalysisContext)))
                    {
                        AnalysisErrorReporter.ReportGivenUpFieldOwnershipError(callTrace);
                    }
                }
            }

            return constructorSummary.GetResolvedReturnSymbols(call.ArgumentList, model);
        }

        /// <summary>
        /// Analyses the summary of the given invocation to find if it gives up ownership
        /// of data from a machine field.
        /// </summary>
        /// <param name="call">Call</param>
        /// <param name="target">Target</param>
        /// <param name="syntaxNode">SyntaxNode</param>
        /// <param name="cfgNode">ControlFlowGraphNode</param>
        /// <param name="givesUpSyntaxNode">Gives up syntaxNode</param>
        /// <param name="givesUpCfgNode">Gives up controlFlowGraphNode</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        /// <returns>Set of return symbols</returns>
        private HashSet<ISymbol> DetectGivenUpFieldOwnershipInInvocation(InvocationExpressionSyntax call,
            ISymbol target, SyntaxNode syntaxNode, ControlFlowGraphNode cfgNode, SyntaxNode givesUpSyntaxNode,
            ControlFlowGraphNode givesUpCfgNode, ClassDeclarationSyntax originalMachine, SemanticModel model,
            TraceInfo trace)
        {
            TraceInfo callTrace = new TraceInfo();
            callTrace.Merge(trace);
            callTrace.AddErrorTrace(call.ToString(), call.SyntaxTree.FilePath, call.SyntaxTree.
                GetLineSpan(call.Span).StartLinePosition.Line + 1);

            var callSymbol = model.GetSymbolInfo(call).Symbol;
            if (callSymbol.ContainingType.ToString().Equals("Microsoft.PSharp.Machine") ||
                callSymbol.ContainingType.ToString().Equals("Microsoft.PSharp.MachineState"))
            {
                return new HashSet<ISymbol>();
            }

            var definition = SymbolFinder.FindSourceDefinitionAsync(callSymbol,
                this.AnalysisContext.Solution).Result;
            if (definition == null || definition.DeclaringSyntaxReferences.IsEmpty)
            {
                AnalysisErrorReporter.ReportUnknownInvocation(callTrace);
                return new HashSet<ISymbol>();
            }

            HashSet<MethodDeclarationSyntax> potentialCalls = new HashSet<MethodDeclarationSyntax>();
            var invocationCall = definition.DeclaringSyntaxReferences.First().GetSyntax()
                as MethodDeclarationSyntax;
            if ((invocationCall.Modifiers.Any(SyntaxKind.AbstractKeyword) &&
                !originalMachine.Modifiers.Any(SyntaxKind.AbstractKeyword)) ||
                invocationCall.Modifiers.Any(SyntaxKind.VirtualKeyword) ||
                invocationCall.Modifiers.Any(SyntaxKind.OverrideKeyword))
            {
                HashSet<MethodDeclarationSyntax> overriders = null;
                if (!InheritanceAnalysis.TryGetPotentialMethodOverriders(out overriders,
                    call, syntaxNode, cfgNode, originalMachine, model, this.AnalysisContext))
                {
                    AnalysisErrorReporter.ReportUnknownVirtualCall(callTrace);
                }

                foreach (var overrider in overriders)
                {
                    potentialCalls.Add(overrider);
                }
            }

            if (potentialCalls.Count == 0)
            {
                potentialCalls.Add(invocationCall);
            }

            HashSet<ISymbol> potentialReturnSymbols = new HashSet<ISymbol>();
            foreach (var potentialCall in potentialCalls)
            {
                var invocationSummary = MethodSummary.Create(this.AnalysisContext, potentialCall);
                var arguments = call.ArgumentList.Arguments;

                for (int idx = 0; idx < arguments.Count; idx++)
                {
                    if (DataFlowQuerying.FlowsIntoTarget(arguments[idx].Expression, target,
                        syntaxNode, cfgNode, givesUpSyntaxNode, givesUpCfgNode,
                        model, this.AnalysisContext))
                    {
                        if (invocationSummary.SideEffects.Any(
                            v => v.Value.Contains(idx) &&
                            this.AnalysisContext.DoesFieldBelongToMachine(v.Key, cfgNode.Summary) &&
                            FieldUsageAnalysis.IsAccessedBeforeBeingReset(v.Key,
                            cfgNode.Summary, this.AnalysisContext)))
                        {
                            AnalysisErrorReporter.ReportGivenUpFieldOwnershipError(callTrace);
                        }
                    }
                }

                var resolvedReturnSymbols = invocationSummary.GetResolvedReturnSymbols(
                    call.ArgumentList, model);
                foreach (var rrs in resolvedReturnSymbols)
                {
                    potentialReturnSymbols.Add(rrs);
                }
            }

            return potentialReturnSymbols;
        }

        #endregion

        #region successor analysis methods

        /// <summary>
        /// Analyses the given method to find if it respects the given up ownerships
        /// and reports any potential data races.
        /// </summary>
        /// <param name="cfgNode">Control flow graph node</param>
        /// <param name="givesUpNode">Gives up node</param>
        /// <param name="target">Target</param>
        /// <param name="giveUpSource">Give up source</param>
        /// <param name="visited">Already visited cfgNodes</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        private void DetectPotentialDataRacesInControlFlowGraph(ControlFlowGraphNode cfgNode,
            ControlFlowGraphNode givesUpNode, ISymbol target, InvocationExpressionSyntax giveUpSource,
            HashSet<ControlFlowGraphNode> visited, ClassDeclarationSyntax originalMachine,
            SemanticModel model, TraceInfo trace)
        {
            if (!cfgNode.IsJumpNode && !cfgNode.IsLoopHeadNode &&
                visited.Contains(givesUpNode))
            {
                foreach (var syntaxNode in cfgNode.SyntaxNodes)
                {
                    var stmt = syntaxNode as StatementSyntax;
                    var localDecl = stmt.DescendantNodesAndSelf().OfType<LocalDeclarationStatementSyntax>().FirstOrDefault();
                    var expr = stmt.DescendantNodesAndSelf().OfType<ExpressionStatementSyntax>().FirstOrDefault();

                    if (localDecl != null)
                    {
                        var varDecl = (stmt as LocalDeclarationStatementSyntax).Declaration;
                        foreach (var variable in varDecl.Variables.Where(v => v.Initializer != null))
                        {
                            if (variable.Initializer.Value is MemberAccessExpressionSyntax)
                            {
                                if (DataFlowQuerying.FlowsFromTarget(variable.Initializer.Value, target,
                                    syntaxNode, cfgNode, givesUpNode.SyntaxNodes.First(), givesUpNode,
                                    model, this.AnalysisContext))
                                {
                                    TraceInfo newTrace = new TraceInfo();
                                    newTrace.Merge(trace);
                                    newTrace.AddErrorTrace(stmt.ToString(), stmt.SyntaxTree.FilePath, stmt.SyntaxTree.
                                        GetLineSpan(stmt.Span).StartLinePosition.Line + 1);
                                    AnalysisErrorReporter.ReportPotentialDataRace(newTrace);
                                }
                            }
                            else if (variable.Initializer.Value is InvocationExpressionSyntax)
                            {
                                var invocation = variable.Initializer.Value as InvocationExpressionSyntax;
                                trace.InsertCall(cfgNode.Summary.Method, invocation);
                                this.DetectPotentialDataRaceInInvocation(invocation,
                                    target, syntaxNode, cfgNode, givesUpNode.SyntaxNodes.First(),
                                    givesUpNode, originalMachine, model, trace);
                            }
                            else if (variable.Initializer.Value is ObjectCreationExpressionSyntax)
                            {
                                var objCreation = variable.Initializer.Value as ObjectCreationExpressionSyntax;
                                trace.InsertCall(cfgNode.Summary.Method, objCreation);
                                this.DetectPotentialDataRaceInObjectCreation(objCreation, target,
                                    syntaxNode, cfgNode, givesUpNode.SyntaxNodes.First(), givesUpNode, model, trace);
                            }
                        }
                    }
                    else if (expr != null)
                    {
                        if (expr.Expression is BinaryExpressionSyntax)
                        {
                            var binaryExpr = expr.Expression as BinaryExpressionSyntax;
                            if (this.IsPayloadIllegallyAccessed(binaryExpr, stmt, model, trace))
                            {
                                continue;
                            }

                            if (binaryExpr.Right is IdentifierNameSyntax &&
                                DataFlowQuerying.FlowsFromTarget(binaryExpr.Right, target, syntaxNode, cfgNode,
                                givesUpNode.SyntaxNodes.First(), givesUpNode, model, this.AnalysisContext))
                            {
                                ISymbol leftSymbol = null;
                                if (binaryExpr.Left is IdentifierNameSyntax)
                                {
                                    leftSymbol = model.GetSymbolInfo(binaryExpr.Left
                                        as IdentifierNameSyntax).Symbol;
                                }
                                else if (binaryExpr.Left is MemberAccessExpressionSyntax)
                                {
                                    leftSymbol = model.GetSymbolInfo((binaryExpr.Left
                                        as MemberAccessExpressionSyntax).Name).Symbol;
                                }

                                var leftDef = SymbolFinder.FindSourceDefinitionAsync(leftSymbol,
                                    this.AnalysisContext.Solution).Result;
                                var type = model.GetTypeInfo(binaryExpr.Right).Type;
                                if (leftDef != null && leftDef.Kind == SymbolKind.Field &&
                                    this.AnalysisContext.DoesFieldBelongToMachine(leftDef, cfgNode.Summary) &&
                                    !this.AnalysisContext.IsTypeAllowedToBeSend(type) &&
                                    !this.AnalysisContext.IsExprEnum(binaryExpr.Right, model))
                                {
                                    TraceInfo newTrace = new TraceInfo();
                                    newTrace.Merge(trace);
                                    newTrace.AddErrorTrace(stmt.ToString(), stmt.SyntaxTree.FilePath, stmt.SyntaxTree.
                                        GetLineSpan(stmt.Span).StartLinePosition.Line + 1);
                                    AnalysisErrorReporter.ReportGivenUpOwnershipFieldAssignment(newTrace);
                                }

                                continue;
                            }
                            else if (binaryExpr.Right is MemberAccessExpressionSyntax &&
                                DataFlowQuerying.FlowsFromTarget(binaryExpr.Right, target, syntaxNode, cfgNode,
                                givesUpNode.SyntaxNodes.First(), givesUpNode, model, this.AnalysisContext))
                            {
                                TraceInfo newTrace = new TraceInfo();
                                newTrace.Merge(trace);
                                newTrace.AddErrorTrace(stmt.ToString(), stmt.SyntaxTree.FilePath, stmt.SyntaxTree.
                                    GetLineSpan(stmt.Span).StartLinePosition.Line + 1);
                                AnalysisErrorReporter.ReportPotentialDataRace(newTrace);
                                continue;
                            }
                            else if (binaryExpr.Right is InvocationExpressionSyntax)
                            {
                                var invocation = binaryExpr.Right as InvocationExpressionSyntax;
                                trace.InsertCall(cfgNode.Summary.Method, invocation);
                                this.DetectPotentialDataRaceInInvocation(invocation,
                                    target, syntaxNode, cfgNode, givesUpNode.SyntaxNodes.First(),
                                    givesUpNode, originalMachine, model, trace);
                            }
                            else if (binaryExpr.Right is ObjectCreationExpressionSyntax)
                            {
                                var objCreation = binaryExpr.Right as ObjectCreationExpressionSyntax;
                                trace.InsertCall(cfgNode.Summary.Method, objCreation);
                                this.DetectPotentialDataRaceInObjectCreation(objCreation, target,
                                    syntaxNode, cfgNode, givesUpNode.SyntaxNodes.First(), givesUpNode, model, trace);
                            }

                            if (binaryExpr.Left is MemberAccessExpressionSyntax)
                            {
                                if (!this.AnalysisContext.IsExprNonMachineMemberAccess(binaryExpr.Left, model) &&
                                    DataFlowQuerying.FlowsFromTarget(binaryExpr.Left, target, syntaxNode, cfgNode,
                                    givesUpNode.SyntaxNodes.First(), givesUpNode, model, this.AnalysisContext))
                                {
                                    TraceInfo newTrace = new TraceInfo();
                                    newTrace.Merge(trace);
                                    newTrace.AddErrorTrace(stmt.ToString(), stmt.SyntaxTree.FilePath, stmt.SyntaxTree.
                                        GetLineSpan(stmt.Span).StartLinePosition.Line + 1);
                                    AnalysisErrorReporter.ReportPotentialDataRace(newTrace);
                                }
                            }
                        }
                        else if (expr.Expression is InvocationExpressionSyntax)
                        {
                            var invocation = expr.Expression as InvocationExpressionSyntax;
                            trace.InsertCall(cfgNode.Summary.Method, invocation);
                            this.DetectPotentialDataRaceInInvocation(invocation,
                                target, syntaxNode, cfgNode, givesUpNode.SyntaxNodes.First(),
                                givesUpNode, originalMachine, model, trace);
                        }
                    }
                }
            }

            if (visited.Contains(cfgNode))
            {
                return;
            }

            visited.Add(cfgNode);

            foreach (var successor in cfgNode.ISuccessors)
            {
                this.DetectPotentialDataRacesInControlFlowGraph(successor,
                    givesUpNode, target, giveUpSource, visited, originalMachine, model, trace);
            }
        }

        /// <summary>
        /// Analyses the summary of the given object creation to find if it respects the
        /// given up ownerships and reports any potential data races.
        /// </summary>
        /// <param name="invocation">Invocation</param>
        /// <param name="target">Target</param>
        /// <param name="syntaxNode">SyntaxNode</param>
        /// <param name="cfgNode">ControlFlowGraphNode</param>
        /// <param name="givesUpSyntaxNode">Gives up syntaxNode</param>
        /// <param name="givesUpCfgNode">Gives up controlFlowGraphNode</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        private void DetectPotentialDataRaceInObjectCreation(ObjectCreationExpressionSyntax call,
            ISymbol target, SyntaxNode syntaxNode, ControlFlowGraphNode cfgNode, SyntaxNode givesUpSyntaxNode,
            ControlFlowGraphNode givesUpCfgNode, SemanticModel model, TraceInfo trace)
        {
            TraceInfo callTrace = new TraceInfo();
            callTrace.Merge(trace);
            callTrace.AddErrorTrace(call.ToString(), call.SyntaxTree.FilePath, call.SyntaxTree.
                GetLineSpan(call.Span).StartLinePosition.Line + 1);

            var callSymbol = model.GetSymbolInfo(call).Symbol;
            var definition = SymbolFinder.FindSourceDefinitionAsync(callSymbol,
                this.AnalysisContext.Solution).Result;
            if (definition == null || definition.DeclaringSyntaxReferences.IsEmpty)
            {
                AnalysisErrorReporter.ReportUnknownInvocation(callTrace);
                return;
            }

            var constructorCall = definition.DeclaringSyntaxReferences.First().GetSyntax()
                as ConstructorDeclarationSyntax;
            var constructorSummary = MethodSummary.Create(this.AnalysisContext, constructorCall);
            var arguments = call.ArgumentList.Arguments;

            for (int idx = 0; idx < arguments.Count; idx++)
            {
                if (!this.AnalysisContext.IsExprEnum(arguments[idx].Expression, model) &&
                    DataFlowQuerying.FlowsFromTarget(arguments[idx].Expression, target, syntaxNode,
                    cfgNode, givesUpSyntaxNode, givesUpCfgNode, model, this.AnalysisContext) &&
                    !DataFlowQuerying.DoesResetInLoop(arguments[idx].Expression, syntaxNode, cfgNode,
                    givesUpSyntaxNode, givesUpCfgNode, model, this.AnalysisContext))
                {
                    if (constructorSummary.AccessSet.ContainsKey(idx))
                    {
                        foreach (var access in constructorSummary.AccessSet[idx])
                        {
                            TraceInfo newTrace = new TraceInfo();
                            newTrace.Merge(callTrace);
                            newTrace.AddErrorTrace(access.ToString(), access.SyntaxTree.FilePath, access.SyntaxTree.
                                GetLineSpan(access.Span).StartLinePosition.Line + 1);
                            AnalysisErrorReporter.ReportPotentialDataRace(newTrace);
                        }
                    }

                    if (constructorSummary.SideEffects.Any(
                        v => v.Value.Contains(idx) &&
                        this.AnalysisContext.DoesFieldBelongToMachine(v.Key, cfgNode.Summary)))
                    {
                        AnalysisErrorReporter.ReportGivenUpOwnershipFieldAssignment(callTrace);
                    }

                    if (constructorSummary.GivesUpSet.Contains(idx))
                    {
                        AnalysisErrorReporter.ReportGivenUpOwnershipSending(callTrace);
                    }
                }
            }

            foreach (var fieldAccess in constructorSummary.FieldAccessSet)
            {
                if (DataFlowQuerying.FlowsFromTarget(fieldAccess.Key, target, syntaxNode,
                    cfgNode, givesUpSyntaxNode, givesUpCfgNode))
                {
                    foreach (var access in fieldAccess.Value)
                    {
                        TraceInfo newTrace = new TraceInfo();
                        newTrace.Merge(callTrace);
                        newTrace.AddErrorTrace(access.ToString(), access.SyntaxTree.FilePath, access.SyntaxTree.
                            GetLineSpan(access.Span).StartLinePosition.Line + 1);
                        AnalysisErrorReporter.ReportPotentialDataRace(newTrace);
                    }
                }
            }
        }

        /// <summary>
        /// Analyses the summary of the given invocation to find if it respects the
        /// given up ownerships and reports any potential data races.
        /// </summary>
        /// <param name="invocation">Invocation</param>
        /// <param name="target">Target</param>
        /// <param name="syntaxNode">SyntaxNode</param>
        /// <param name="cfgNode">ControlFlowGraphNode</param>
        /// <param name="givesUpSyntaxNode">Gives up syntaxNode</param>
        /// <param name="givesUpCfgNode">Gives up controlFlowGraphNode</param>
        /// <param name="originalMachine">Original machine</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        private void DetectPotentialDataRaceInInvocation(InvocationExpressionSyntax call,
            ISymbol target, SyntaxNode syntaxNode, ControlFlowGraphNode cfgNode, SyntaxNode givesUpSyntaxNode,
            ControlFlowGraphNode givesUpCfgNode, ClassDeclarationSyntax originalMachine, SemanticModel model,
            TraceInfo trace)
        {
            TraceInfo callTrace = new TraceInfo();
            callTrace.Merge(trace);
            callTrace.AddErrorTrace(call.ToString(), call.SyntaxTree.FilePath, call.SyntaxTree.
                GetLineSpan(call.Span).StartLinePosition.Line + 1);
            
            var callSymbol = model.GetSymbolInfo(call).Symbol;
            if (callSymbol.ContainingType.ToString().Equals("Microsoft.PSharp.Machine") ||
                callSymbol.ContainingType.ToString().Equals("Microsoft.PSharp.MachineState"))
            {
                this.DetectPotentialDataRaceInGivesUpOperation(call, target,
                    syntaxNode, cfgNode, givesUpSyntaxNode, givesUpCfgNode, model, callTrace);
                return;
            }
            
            var definition = SymbolFinder.FindSourceDefinitionAsync(callSymbol,
                this.AnalysisContext.Solution).Result;
            if (definition == null || definition.DeclaringSyntaxReferences.IsEmpty)
            {
                if (call.Expression is MemberAccessExpressionSyntax)
                {
                    var callee = (call.Expression as MemberAccessExpressionSyntax).Expression;
                    if (DataFlowQuerying.FlowsFromTarget(callee, target, syntaxNode, cfgNode,
                        givesUpSyntaxNode, givesUpCfgNode, model, this.AnalysisContext) &&
                        !DataFlowQuerying.DoesResetInLoop(callee, syntaxNode, cfgNode,
                        givesUpSyntaxNode, givesUpCfgNode, model, this.AnalysisContext))
                    {
                        var typeSymbol = model.GetTypeInfo(callee).Type;
                        if (typeSymbol.ContainingNamespace.ToString().Equals("System.Collections.Generic"))
                        {
                            TraceInfo newTrace = new TraceInfo();
                            newTrace.Merge(callTrace);
                            newTrace.AddErrorTrace(callee.ToString(), callee.SyntaxTree.FilePath, callee.SyntaxTree.
                                GetLineSpan(callee.Span).StartLinePosition.Line + 1);
                            AnalysisErrorReporter.ReportPotentialDataRace(newTrace);
                            return;
                        }
                    }
                }

                AnalysisErrorReporter.ReportUnknownInvocation(callTrace);
                return;
            }

            HashSet<MethodDeclarationSyntax> potentialCalls = new HashSet<MethodDeclarationSyntax>();
            var invocationCall = definition.DeclaringSyntaxReferences.First().GetSyntax()
                as MethodDeclarationSyntax;
            if ((invocationCall.Modifiers.Any(SyntaxKind.AbstractKeyword) &&
                !originalMachine.Modifiers.Any(SyntaxKind.AbstractKeyword)) ||
                invocationCall.Modifiers.Any(SyntaxKind.VirtualKeyword) ||
                invocationCall.Modifiers.Any(SyntaxKind.OverrideKeyword))
            {
                HashSet<MethodDeclarationSyntax> overriders = null;
                if (!InheritanceAnalysis.TryGetPotentialMethodOverriders(out overriders,
                    call, syntaxNode, cfgNode, originalMachine, model, this.AnalysisContext))
                {
                    AnalysisErrorReporter.ReportUnknownVirtualCall(callTrace);
                }

                foreach (var overrider in overriders)
                {
                    potentialCalls.Add(overrider);
                }
            }

            if (potentialCalls.Count == 0)
            {
                potentialCalls.Add(invocationCall);
            }

            foreach (var potentialCall in potentialCalls)
            {
                var invocationSummary = MethodSummary.Create(this.AnalysisContext, potentialCall);
                var arguments = call.ArgumentList.Arguments;

                for (int idx = 0; idx < arguments.Count; idx++)
                {
                    if (!this.AnalysisContext.IsExprEnum(arguments[idx].Expression, model) &&
                        DataFlowQuerying.FlowsFromTarget(arguments[idx].Expression, target, syntaxNode,
                        cfgNode, givesUpSyntaxNode, givesUpCfgNode, model, this.AnalysisContext) &&
                        !DataFlowQuerying.DoesResetInLoop(arguments[idx].Expression, syntaxNode, cfgNode,
                        givesUpSyntaxNode, givesUpCfgNode, model, this.AnalysisContext))
                    {
                        if (invocationSummary.AccessSet.ContainsKey(idx))
                        {
                            foreach (var access in invocationSummary.AccessSet[idx])
                            {
                                TraceInfo newTrace = new TraceInfo();
                                newTrace.Merge(callTrace);
                                newTrace.AddErrorTrace(access.ToString(), access.SyntaxTree.FilePath, access.SyntaxTree.
                                    GetLineSpan(access.Span).StartLinePosition.Line + 1);
                                AnalysisErrorReporter.ReportPotentialDataRace(newTrace);
                            }
                        }

                        if (invocationSummary.SideEffects.Any(
                            v => v.Value.Contains(idx) &&
                            this.AnalysisContext.DoesFieldBelongToMachine(v.Key, cfgNode.Summary)))
                        {
                            AnalysisErrorReporter.ReportGivenUpOwnershipFieldAssignment(callTrace);
                        }

                        if (invocationSummary.GivesUpSet.Contains(idx))
                        {
                            AnalysisErrorReporter.ReportGivenUpOwnershipSending(callTrace);
                        }
                    }
                }

                foreach (var fieldAccess in invocationSummary.FieldAccessSet)
                {
                    if (DataFlowQuerying.FlowsFromTarget(fieldAccess.Key, target, syntaxNode,
                        cfgNode, givesUpSyntaxNode, givesUpCfgNode))
                    {
                        foreach (var access in fieldAccess.Value)
                        {
                            TraceInfo newTrace = new TraceInfo();
                            newTrace.Merge(callTrace);
                            newTrace.AddErrorTrace(access.ToString(), access.SyntaxTree.FilePath, access.SyntaxTree.
                                GetLineSpan(access.Span).StartLinePosition.Line + 1);
                            AnalysisErrorReporter.ReportPotentialDataRace(newTrace);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Analyses the arguments of the gives up operation to find if it respects
        /// the given up ownerships and reports any potential data races.
        /// </summary>
        /// <param name="operation">Send-related operation</param>
        /// <param name="target">Target</param>
        /// <param name="syntaxNode">SyntaxNode</param>
        /// <param name="cfgNode">ControlFlowGraphNode</param>
        /// <param name="givesUpSyntaxNode">Gives up syntaxNode</param>
        /// <param name="givesUpCfgNode">Gives up controlFlowGraphNode</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        private void DetectPotentialDataRaceInGivesUpOperation(InvocationExpressionSyntax operation,
            ISymbol target, SyntaxNode syntaxNode, ControlFlowGraphNode cfgNode, SyntaxNode givesUpSyntaxNode,
            ControlFlowGraphNode givesUpCfgNode, SemanticModel model, TraceInfo trace)
        {
            List<ExpressionSyntax> arguments = new List<ExpressionSyntax>();
            var opSymbol = model.GetSymbolInfo(operation).Symbol;

            if ((opSymbol.ContainingType.ToString().Equals("Microsoft.PSharp.Machine") ||
                opSymbol.ContainingType.ToString().Equals("Microsoft.PSharp.MachineState")) &&
                opSymbol.Name.Equals("Send"))
            {
                if (operation.ArgumentList.Arguments[1].Expression is ObjectCreationExpressionSyntax)
                {
                    var objCreation = operation.ArgumentList.Arguments[1].Expression
                        as ObjectCreationExpressionSyntax;
                    foreach (var arg in objCreation.ArgumentList.Arguments)
                    {
                        arguments.Add(arg.Expression);
                    }
                }
                else if (operation.ArgumentList.Arguments[1].Expression is BinaryExpressionSyntax &&
                    operation.ArgumentList.Arguments[1].Expression.IsKind(SyntaxKind.AsExpression))
                {
                    var binExpr = operation.ArgumentList.Arguments[1].Expression
                        as BinaryExpressionSyntax;
                    if ((binExpr.Left is IdentifierNameSyntax) ||
                        (binExpr.Left is MemberAccessExpressionSyntax))
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
            }
            else if ((opSymbol.ContainingType.ToString().Equals("Microsoft.PSharp.Machine") ||
                opSymbol.ContainingType.ToString().Equals("Microsoft.PSharp.MachineState")) &&
                opSymbol.Name.Equals("CreateMachine"))
            {
                if (operation.ArgumentList.Arguments.Count == 0)
                {
                    return;
                }

                if (operation.ArgumentList.Arguments[0].Expression is ObjectCreationExpressionSyntax)
                {
                    var objCreation = operation.ArgumentList.Arguments[0].Expression
                        as ObjectCreationExpressionSyntax;
                    foreach (var arg in objCreation.ArgumentList.Arguments)
                    {
                        arguments.Add(arg.Expression);
                    }
                }
                else if (operation.ArgumentList.Arguments[0].Expression is BinaryExpressionSyntax &&
                    operation.ArgumentList.Arguments[0].Expression.IsKind(SyntaxKind.AsExpression))
                {
                    var binExpr = operation.ArgumentList.Arguments[0].Expression
                        as BinaryExpressionSyntax;
                    if ((binExpr.Left is IdentifierNameSyntax) || (binExpr.Left is MemberAccessExpressionSyntax))
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
                else if ((operation.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax) ||
                    (operation.ArgumentList.Arguments[0].Expression is MemberAccessExpressionSyntax))
                {
                    arguments.Add(operation.ArgumentList.Arguments[0].Expression);
                }
            }

            var extractedArgs = this.ExtractArguments(arguments);

            foreach (var arg in extractedArgs)
            {
                if (!this.AnalysisContext.IsExprEnum(arg, model) &&
                    DataFlowQuerying.FlowsFromTarget(arg, target, syntaxNode, cfgNode,
                    givesUpSyntaxNode, givesUpCfgNode, model, this.AnalysisContext) &&
                    !DataFlowQuerying.DoesResetInLoop(arg, syntaxNode, cfgNode,
                    givesUpSyntaxNode, givesUpCfgNode, model, this.AnalysisContext))
                {
                    AnalysisErrorReporter.ReportGivenUpOwnershipSending(trace);
                    return;
                }
            }
        }

        #endregion

        #region helper methods

        /// <summary>
        /// Returns true if the argument can be accessed after it is sent.
        /// Returns false if it cannot be accessed.
        /// </summary>
        /// <param name="arg">Argument</param>
        /// <param name="givesUpNode">Gives up node</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        /// <param name="shouldReportError">Should report error</param>
        /// <returns>Boolean</returns>
        private bool IsArgumentSafeToAccess(ExpressionSyntax arg, ControlFlowGraphNode givesUpNode,
            SemanticModel model, TraceInfo trace, bool shouldReportError)
        {
            ISymbol symbol = null;
            ITypeSymbol typeSymbol = null;
            if (arg is MemberAccessExpressionSyntax)
            {
                symbol = model.GetSymbolInfo((arg as MemberAccessExpressionSyntax).Name).Symbol;
                typeSymbol = model.GetTypeInfo(arg).Type;
            }
            else if (arg is IdentifierNameSyntax)
            {
                symbol = model.GetSymbolInfo(arg).Symbol;
                typeSymbol = model.GetTypeInfo(arg).Type;
            }
            
            if (symbol.ToString().Equals("Microsoft.PSharp.Machine.Payload") ||
                symbol.ToString().Equals("Microsoft.PSharp.MachineState.Payload"))
            {
                return false;
            }
            else if (typeSymbol.ToString().Equals("Microsoft.PSharp.Machine") ||
                symbol.ToString().Equals("Microsoft.PSharp.Machine") ||
                symbol.ToString().Equals("Microsoft.PSharp.MachineState.Machine"))
            {
                return true;
            }
            else if (symbol.ContainingType.Name.Equals("Tuple"))
            {
                var str = (arg as MemberAccessExpressionSyntax).Name.ToString();
                int idx = Int32.Parse(str.Substring(4));
                return this.AnalysisContext.IsTypeAllowedToBeSend(
                    symbol.ContainingType.TypeArguments[idx - 1]);
            }
            else
            {
                var definition = SymbolFinder.FindSourceDefinitionAsync(symbol,
                    this.AnalysisContext.Solution).Result;
                if (definition == null)
                {
                    return false;
                }
                
                if (definition.DeclaringSyntaxReferences.First().GetSyntax().Parent is ParameterListSyntax)
                {
                    var parameterList = definition.DeclaringSyntaxReferences.First().
                        GetSyntax().Parent as ParameterListSyntax;
                    var parameter = parameterList.Parameters.First(v =>
                        v.Identifier.ValueText.Equals(arg.ToString()));
                    return this.AnalysisContext.IsTypeAllowedToBeSend(parameter.Type, model);
                }
                else if (definition.DeclaringSyntaxReferences.First().GetSyntax().Parent is VariableDeclarationSyntax)
                {
                    var varDecl = definition.DeclaringSyntaxReferences.First().GetSyntax().Parent
                        as VariableDeclarationSyntax;
                    if (shouldReportError && definition != null && definition.Kind == SymbolKind.Field &&
                        !this.AnalysisContext.IsTypeAllowedToBeSend(varDecl.Type, model) &&
                        !this.AnalysisContext.IsExprEnum(arg, model) &&
                        !DataFlowQuerying.DoesResetInSuccessorCFGNodes(symbol, symbol,
                        givesUpNode.SyntaxNodes.First(), givesUpNode) &&
                        FieldUsageAnalysis.IsAccessedBeforeBeingReset(definition,
                        givesUpNode.Summary, this.AnalysisContext))
                    {
                        AnalysisErrorReporter.ReportGivenUpFieldOwnershipError(trace);
                    }
                    
                    return this.AnalysisContext.IsTypeAllowedToBeSend(varDecl.Type, model);
                }
            }

            return true;
        }

        /// <summary>
        /// Returns true and reports an error if the payload was illegally accessed.
        /// </summary>
        /// <param name="expr">Expression</param>
        /// <param name="stmt">Statement</param>
        /// <param name="model">SemanticModel</param>
        /// <param name="trace">TraceInfo</param>
        /// <returns></returns>
        private bool IsPayloadIllegallyAccessed(BinaryExpressionSyntax expr,
            StatementSyntax stmt, SemanticModel model, TraceInfo trace)
        {
            ISymbol payloadSymbol = null;
            if (expr.Right is MemberAccessExpressionSyntax)
            {
                payloadSymbol = model.GetSymbolInfo((expr.Right as MemberAccessExpressionSyntax).
                    Name).Symbol;
            }
            else if (expr.Right is IdentifierNameSyntax)
            {
                payloadSymbol = model.GetSymbolInfo(expr.Right).Symbol;
            }
            else
            {
                return false;
            }

            if (payloadSymbol.ToString().Equals("Microsoft.PSharp.Machine.Payload") ||
                payloadSymbol.ToString().Equals("Microsoft.PSharp.MachineState.Payload"))
            {
                ISymbol leftSymbol = null;
                if (expr.Left is IdentifierNameSyntax)
                {
                    leftSymbol = model.GetSymbolInfo(expr.Left
                        as IdentifierNameSyntax).Symbol;
                }
                else if (expr.Left is MemberAccessExpressionSyntax)
                {
                    leftSymbol = model.GetSymbolInfo((expr.Left
                        as MemberAccessExpressionSyntax).Name).Symbol;
                }

                var leftDef = SymbolFinder.FindSourceDefinitionAsync(leftSymbol,
                    this.AnalysisContext.Solution).Result;
                if (leftDef != null && leftDef.Kind == SymbolKind.Field)
                {
                    TraceInfo newTrace = new TraceInfo();
                    newTrace.Merge(trace);
                    newTrace.AddErrorTrace(stmt.ToString(), stmt.SyntaxTree.FilePath, stmt.SyntaxTree.
                        GetLineSpan(stmt.Span).StartLinePosition.Line + 1);
                    AnalysisErrorReporter.ReportPayloadFieldAssignment(newTrace);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Extracts arguments from the given list of arguments.
        /// </summary>
        /// <param name="arguments">List of arguments</param>
        /// <returns>List of arguments</returns>
        private List<ExpressionSyntax> ExtractArguments(List<ExpressionSyntax> arguments)
        {
            var args = new List<ExpressionSyntax>();

            foreach (var arg in arguments)
            {
                if (arg is ObjectCreationExpressionSyntax)
                {
                    var objCreation = arg as ObjectCreationExpressionSyntax;
                    List<ExpressionSyntax> argExprs = new List<ExpressionSyntax>();
                    foreach (var objCreationArg in objCreation.ArgumentList.Arguments)
                    {
                        argExprs.Add(objCreationArg.Expression);
                    }

                    var objCreationArgs = this.ExtractArguments(argExprs);
                    foreach (var objCreationArg in objCreationArgs)
                    {
                        args.Add(objCreationArg);
                    }
                }
                else if (arg is InvocationExpressionSyntax)
                {
                    var invocation = arg as InvocationExpressionSyntax;
                    List<ExpressionSyntax> argExprs = new List<ExpressionSyntax>();
                    foreach (var invocationArg in invocation.ArgumentList.Arguments)
                    {
                        argExprs.Add(invocationArg.Expression);
                    }

                    var invocationArgs = this.ExtractArguments(argExprs);
                    foreach (var invocationArg in invocationArgs)
                    {
                        args.Add(invocationArg);
                    }
                }
                else
                {
                    args.Add(arg);
                }
            }

            return args;
        }

        #endregion
    }
}
