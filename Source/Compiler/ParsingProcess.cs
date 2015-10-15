﻿//-----------------------------------------------------------------------
// <copyright file="ParsingProcess.cs">
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

using Microsoft.PSharp.LanguageServices.Compilation;
using Microsoft.PSharp.LanguageServices.Parsing;
using Microsoft.PSharp.Utilities;

namespace Microsoft.PSharp
{
    /// <summary>
    /// A P# parsing process.
    /// </summary>
    internal sealed class ParsingProcess
    {
        #region fields

        /// <summary>
        /// The compilation context.
        /// </summary>
        private CompilationContext CompilationContext;

        #endregion

        #region API

        /// <summary>
        /// Creates a P# parsing process.
        /// </summary>
        /// <param name="context">CompilationContext</param>
        /// <returns>ParsingProcess</returns>
        public static ParsingProcess Create(CompilationContext context)
        {
            return new ParsingProcess(context);
        }

        /// <summary>
        /// Starts the P# parsing process.
        /// </summary>
        public void Start()
        {
            Output.PrintLine(". Parsing");

            foreach (var target in this.CompilationContext.Configuration.CompilationTargets)
            {
                // Creates and runs a P# parsing engine.
                ParsingEngine.Create(this.CompilationContext).Run();
            }
        }

        #endregion

        #region private methods

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="context">CompilationContext</param>
        private ParsingProcess(CompilationContext context)
        {
            this.CompilationContext = context;
        }

        #endregion
    }
}
