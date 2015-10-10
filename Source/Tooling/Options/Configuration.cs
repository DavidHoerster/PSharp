﻿//-----------------------------------------------------------------------
// <copyright file="Configuration.cs">
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

namespace Microsoft.PSharp.Tooling
{
    public abstract class Configuration
    {
        #region core options

        /// <summary>
        /// The path to the solution file.
        /// </summary>
        public string SolutionFilePath;

        /// <summary>
        /// The output path.
        /// </summary>
        public string OutputFilePath;

        /// <summary>
        /// The name of the project to analyse.
        /// </summary>
        public string ProjectName;

        /// <summary>
        /// Verbosity level.
        /// </summary>
        public int Verbose;

        /// <summary>
        /// Timeout.
        /// </summary>
        public int Timeout;

        /// <summary>
        /// True if interoperation is enabled.
        /// </summary>
        public bool InteroperationEnabled;

        #endregion

        #region constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        protected Configuration()
        {
            this.SolutionFilePath = "";
            this.OutputFilePath = "";
            this.ProjectName = "";

            this.Verbose = 1;
            this.Timeout = 0;

            this.InteroperationEnabled = true;
        }

        #endregion

        #region methods

        /// <summary>
        /// Updates the configuration with verbose output enabled
        /// and returns it.
        /// </summary>
        /// <param name="level">Verbosity level</param>
        /// <returns>Configuration</returns>
        public Configuration WithVerbosityEnabled(int level)
        {
            this.Verbose = level;
            return this;
        }

        /// <summary>
        /// Updates the configuration with debugging information enabled
        /// or disabled and returns it.
        /// </summary>
        /// <param name="level">Verbosity level</param>
        /// <returns>Configuration</returns>
        public Configuration WithDebuggingEnabled(bool value = true)
        {
            Output.Debugging = value;
            return this;
        }

        #endregion
    }
}
