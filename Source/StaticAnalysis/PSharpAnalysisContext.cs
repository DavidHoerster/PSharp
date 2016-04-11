//-----------------------------------------------------------------------
// <copyright file="PSharpAnalysisContext.cs">
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
using Microsoft.CodeAnalysis.FindSymbols;

using Microsoft.PSharp.LanguageServices;
using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp.StaticAnalysis
{
    /// <summary>
    /// The P# static analysis context.
    /// </summary>
    public sealed class PSharpAnalysisContext : AnalysisContext
    {
        #region fields

        /// <summary>
        /// Configuration.
        /// </summary>
        internal Configuration Configuration;

        /// <summary>
        /// Set of state-machines in the project.
        /// </summary>
        internal HashSet<StateMachine> Machines;

        /// <summary>
        /// Set of abstract state-machines in the project.
        /// </summary>
        internal HashSet<StateMachine> AbstractMachines;

        /// <summary>
        /// Dictionary of state transition graphs in the project.
        /// </summary>
        internal Dictionary<StateMachine, StateTransitionGraphNode> StateTransitionGraphs;

        /// <summary>
        /// Dictionary containing machine inheritance information.
        /// </summary>
        internal Dictionary<StateMachine, HashSet<StateMachine>> MachineInheritanceMap;

        #endregion

        #region public API

        /// <summary>
        /// Create a new state-machine static analysis context.
        /// </summary>
        /// <param name="configuration">Configuration</param>
        /// <param name="project">Project</param>
        /// <returns>StateMachineAnalysisContext</returns>
        public static PSharpAnalysisContext Create(Configuration configuration, Project project)
        {
            return new PSharpAnalysisContext(configuration, project);
        }

        /// <summary>
        /// Returns true if the given type is passed by value or is immutable.
        /// </summary>
        /// <param name="type">Type</param>
        /// <returns>Boolean</returns>
        public override bool IsTypePassedByValueOrImmutable(ITypeSymbol type)
        {
            if (base.IsTypePassedByValueOrImmutable(type))
            {
                return true;
            }

            var typeName = type.ContainingNamespace?.ToString() + "." + type.Name;
            if (typeName.Equals(typeof(Microsoft.PSharp.MachineId).FullName))
            {
                return true;
            }

            return false;
        }

        #endregion

        #region constructors

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="configuration">Configuration</param>
        /// <param name="project">Project</param>
        private PSharpAnalysisContext(Configuration configuration, Project project)
            : base(project)
        {
            this.Configuration = configuration;

            this.Machines = new HashSet<StateMachine>();
            this.AbstractMachines = new HashSet<StateMachine>();
            this.StateTransitionGraphs = new Dictionary<StateMachine, StateTransitionGraphNode>();
            this.MachineInheritanceMap = new Dictionary<StateMachine, HashSet<StateMachine>>();

            this.FindAllStateMachines();
            this.FindStateMachineInheritanceInformation();
        }

        #endregion

        #region private methods

        /// <summary>
        /// Finds all state-machines in the project.
        /// </summary>
        private void FindAllStateMachines()
        {
            // Iterate the syntax trees for each project file.
            foreach (var tree in base.Compilation.SyntaxTrees)
            {
                if (!base.IsProgramSyntaxTree(tree))
                {
                    continue;
                }
                
                // Get the tree's semantic model.
                var model = base.Compilation.GetSemanticModel(tree);

                // Get the tree's root node compilation unit.
                var root = (CompilationUnitSyntax)tree.GetRoot();

                // Iterate the class declerations only if they are machines.
                foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
                {
                    if (Querying.IsMachine(base.Compilation, classDecl))
                    {
                        if (classDecl.Modifiers.Any(SyntaxKind.AbstractKeyword))
                        {
                            this.AbstractMachines.Add(new StateMachine(classDecl, this));
                        }
                        else
                        {
                            this.Machines.Add(new StateMachine(classDecl, this));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Finds state-machine inheritance information for all
        /// state-machines in the project.
        /// </summary>
        private void FindStateMachineInheritanceInformation()
        {
            foreach (var machine in this.Machines)
            {
                var inheritedMachines = new HashSet<StateMachine>();

                IList<INamedTypeSymbol> baseTypes = base.GetBaseTypes(machine.Declaration);
                foreach (var type in baseTypes)
                {
                    if (type.ToString().Equals(typeof(Machine).FullName))
                    {
                        break;
                    }

                    var availableMachines = new List<StateMachine>(this.Machines);
                    availableMachines.AddRange(this.AbstractMachines);
                    var inheritedMachine = availableMachines.FirstOrDefault(m
                        => base.GetFullClassName(m.Declaration).Equals(type.ToString()));
                    if (inheritedMachine == null)
                    {
                        break;
                    }

                    inheritedMachines.Add(inheritedMachine);
                }

                if (inheritedMachines.Count > 0)
                {
                    this.MachineInheritanceMap.Add(machine, inheritedMachines);
                }
            }
        }

        #endregion
    }
}
