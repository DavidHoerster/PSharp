﻿//-----------------------------------------------------------------------
// <copyright file="PSharpProgram.cs">
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

using System.Collections.Generic;

namespace Microsoft.PSharp.Parsing.Syntax
{
    /// <summary>
    /// A P# program.
    /// </summary>
    public sealed class PSharpProgram : AbstractPSharpProgram
    {
        #region fields
        
        /// <summary>
        /// List of using declarations.
        /// </summary>
        internal List<UsingDeclaration> UsingDeclarations;

        /// <summary>
        /// List of namespace declarations.
        /// </summary>
        internal List<NamespaceDeclaration> NamespaceDeclarations;

        #endregion

        #region public API

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="project">PSharpProject</param>
        /// <param name="filePath">File path</param>
        public PSharpProgram(PSharpProject project, string filePath)
            : base(project, filePath)
        {
            this.UsingDeclarations = new List<UsingDeclaration>();
            this.NamespaceDeclarations = new List<NamespaceDeclaration>();
        }

        /// <summary>
        /// Rewrites the P# program to the C#-IR.
        /// </summary>
        /// <returns>Rewritten text</returns>
        public override string Rewrite()
        {
            this.RewrittenText = "";

            foreach (var node in this.UsingDeclarations)
            {
                node.Rewrite();
                this.RewrittenText += node.TextUnit.Text;
            }

            foreach (var node in this.NamespaceDeclarations)
            {
                node.Rewrite();
                this.RewrittenText += node.TextUnit.Text;
            }

            return this.RewrittenText;
        }

        /// <summary>
        /// Models the P# program to the C#-IR.
        /// </summary>
        /// <returns>Model text</returns>
        public override string Model()
        {
            this.ModelText = "";

            foreach (var node in this.UsingDeclarations)
            {
                node.Model();
                this.ModelText += node.TextUnit.Text;
            }

            foreach (var node in this.NamespaceDeclarations)
            {
                node.Model();
                this.ModelText += node.TextUnit.Text;
            }

            return this.ModelText;
        }

        #endregion
    }
}
