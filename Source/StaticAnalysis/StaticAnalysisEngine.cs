﻿//-----------------------------------------------------------------------
// <copyright file="StaticAnalysisEngine.cs">
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

using Microsoft.PSharp.LanguageServices.Compilation;
using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp.StaticAnalysis
{
    /// <summary>
    /// A P# static analysis engine.
    /// </summary>
    public sealed class StaticAnalysisEngine
    {
        #region fields

        /// <summary>
        /// The compilation context.
        /// </summary>
        private CompilationContext CompilationContext;

        /// <summary>
        /// The overall runtime profiler.
        /// </summary>
        private Profiler Profiler;

        #endregion

        #region public API

        /// <summary>
        /// Creates a P# static analysis engine.
        /// </summary>
        /// <param name="context">CompilationContext</param>
        /// <returns>StaticAnalysisEngine</returns>
        public static StaticAnalysisEngine Create(CompilationContext context)
        {
            return new StaticAnalysisEngine(context);
        }

        /// <summary>
        /// Runs the P# static analysis engine.
        /// </summary>
        /// <returns>StaticAnalysisEngine</returns>
        public StaticAnalysisEngine Run()
        {
            // Parse the projects.
            if (this.CompilationContext.Configuration.ProjectName.Equals(""))
            {
                foreach (var project in this.CompilationContext.GetSolution().Projects)
                {
                    this.AnalyzeProject(project);
                }
            }
            else
            {
                // Find the project specified by the user.
                var targetProject = this.CompilationContext.GetSolution().Projects.Where(
                    p => p.Name.Equals(this.CompilationContext.Configuration.ProjectName)).FirstOrDefault();

                var projectDependencyGraph = this.CompilationContext.GetSolution().GetProjectDependencyGraph();
                var projectDependencies = projectDependencyGraph.GetProjectsThatThisProjectTransitivelyDependsOn(targetProject.Id);

                foreach (var project in this.CompilationContext.GetSolution().Projects)
                {
                    if (!projectDependencies.Contains(project.Id) && !project.Id.Equals(targetProject.Id))
                    {
                        continue;
                    }
                    
                    this.AnalyzeProject(project);
                }
            }

            return this;
        }

        #endregion

        #region constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">CompilationContext</param>
        private StaticAnalysisEngine(CompilationContext context)
        {
            this.Profiler = new Profiler();
            this.CompilationContext = context;
        }

        #endregion

        #region private methods

        /// <summary>
        /// Analyzes the given P# project.
        /// </summary>
        /// <param name="project">Project</param>
        private void AnalyzeProject(Project project)
        {
            // Starts profiling the analysis.
            if (this.CompilationContext.Configuration.TimeStaticAnalysis)
            {
                this.Profiler.StartMeasuringExecutionTime();
            }

            // Create a state-machine static analysis context.
            var context = PSharpAnalysisContext.Create(this.CompilationContext.Configuration, project);
            this.RegisterGivesUpOwnershipOperations(context);

            // Creates and runs an analysis pass that computes the
            // summaries for every P# machine.
            MachineSummarizationPass.Create(context).Run();

            // Creates and runs an analysis pass that finds if a machine exposes
            // any fields or methods to other machines.
            DirectAccessAnalysisPass.Create(context).Run();
            
            // Creates and runs an analysis pass that constructs the
            // state transition graph for each machine.
            if (this.CompilationContext.Configuration.DoStateTransitionAnalysis)
            {
                StateTransitionAnalysisPass.Create(context).Run();
            }

            // Creates and runs an analysis pass that detects if any method
            // in each machine is erroneously giving up ownership.
            GivesUpOwnershipAnalysisPass.Create(context).Run();

            // Creates and runs an analysis pass that detects if all methods
            // in each machine respect given up ownerships.
            RespectsOwnershipAnalysisPass.Create(context).Run();

            // Stops profiling the analysis.
            if (this.CompilationContext.Configuration.TimeStaticAnalysis)
            {
                this.Profiler.StopMeasuringExecutionTime();
                IO.PrintLine("... Total static analysis runtime: '" +
                    this.Profiler.Results() + "' seconds.");
            }
        }

        /// <summary>
        /// Registers gives-up ownership operations.
        /// </summary>
        /// <param name="context">PSharpAnalysisContext</param>
        private void RegisterGivesUpOwnershipOperations(PSharpAnalysisContext context)
        {
            context.RegisterGivesUpOwnershipMethod("Microsoft.PSharp.Send",
                new HashSet<int> { 1 });
            context.RegisterGivesUpOwnershipMethod("Microsoft.PSharp.CreateMachine",
                new HashSet<int> { 1 });
        }

        #endregion
    }
}
