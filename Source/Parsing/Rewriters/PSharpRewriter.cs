﻿//-----------------------------------------------------------------------
// <copyright file="PSharpRewriter.cs">
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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using Microsoft.PSharp.Parsing.Syntax;

namespace Microsoft.PSharp.Parsing
{
    /// <summary>
    /// An abstract P# program rewriter.
    /// </summary>
    internal abstract class PSharpRewriter
    {
        #region fields

        /// <summary>
        /// The P# project.
        /// </summary>
        protected PSharpProject Project;

        #endregion

        #region protected API

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="project">PSharpProject</param>
        protected PSharpRewriter(PSharpProject project)
        {
            this.Project = project;
        }

        /// <summary>
        /// Returns the next statement.
        /// </summary>
        /// <param name="node">SyntaxNode</param>
        /// <returns>SyntaxNode</returns>
        protected SyntaxNode GetNextStatement(SyntaxNode node)
        {
            SyntaxNode next = null;
            var relatives = node.Parent.ChildNodes().ToList();
            for (int idx = 0; idx < relatives.Count; idx++)
            {
                if (relatives[idx].Equals(node) &&
                    idx < relatives.Count - 1)
                {
                    next = relatives[idx + 1];
                    break;
                }
            }

            return next;
        }

        /// <summary>
        /// True if the given syntax node is in the scope of a state.
        /// </summary>
        /// <param name="node">SyntaxNode</param>
        /// <returns>Boolean value</returns>
        protected bool IsInStateScope(SyntaxNode node)
        {
            var result = false;

            var ancestors = node.Ancestors().OfType<ClassDeclarationSyntax>().ToList();
            foreach (var ancestor in ancestors)
            {
                result = this.Project.PSharpPrograms.
                    SelectMany(p => p.NamespaceDeclarations).
                    SelectMany(n => n.MachineDeclarations).
                    SelectMany(m => m.StateDeclarations).
                    Any(s => s.Identifier.TextUnit.Text.Equals(ancestor.Identifier.ValueText));

                if (result)
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Tries to return the parent machine identifier, if any.
        /// </summary>
        /// <param name="node">SyntaxNode</param>
        /// <param name="identifier">Machine identifier</param>
        /// <returns>Boolean value</returns>
        protected bool TryGetParentMachine(SyntaxNode node, out string identifier)
        {
            var result = false;
            identifier = "";

            var ancestors = node.Ancestors().OfType<ClassDeclarationSyntax>().ToList();
            foreach (var ancestor in ancestors)
            {
                var machine = this.Project.PSharpPrograms.
                    SelectMany(p => p.NamespaceDeclarations).
                    SelectMany(n => n.MachineDeclarations).
                    FirstOrDefault(s => s.Identifier.TextUnit.Text.Equals(ancestor.Identifier.ValueText));

                if (machine != null)
                {
                    identifier = machine.Identifier.TextUnit.Text;
                    result = true;
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Updates the syntax tree.
        /// </summary>
        /// <param name="tree">SyntaxTree</param>
        /// <param name="text">Text</param>
        /// <returns>SyntaxTree</returns>
        protected SyntaxTree UpdateSyntaxTree(SyntaxTree tree, string text)
        {
            var source = SourceText.From(text);
            return tree.WithChangedText(source);
        }

        #endregion
    }
}
