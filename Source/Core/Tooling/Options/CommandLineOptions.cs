﻿//-----------------------------------------------------------------------
// <copyright file="CommandLineOptions.cs">
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

namespace Microsoft.PSharp.Tooling
{
    public class CommandLineOptions
    {
        #region fields

        protected string[] Options;

        #endregion

        #region public API

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="args">Array of arguments</param>
        public CommandLineOptions(string[] args)
        {
            this.Options = args;
        }

        /// <summary>
        /// Parses the command line options.
        /// </summary>
        public virtual void Parse()
        {
            for (int idx = 0; idx < this.Options.Length; idx++)
            {
                #region core options

                if (this.Options[idx].ToLower().Equals("/?"))
                {
                    this.ShowHelp();
                    Environment.Exit(1);
                }
                else if (this.Options[idx].ToLower().StartsWith("/s:") &&
                    this.Options[idx].Length > 3)
                {
                    Configuration.SolutionFilePath = this.Options[idx].Substring(3);
                }
                else if (this.Options[idx].ToLower().StartsWith("/p:") &&
                    this.Options[idx].Length > 3)
                {
                    Configuration.ProjectName = this.Options[idx].Substring(3);
                }
                else if (this.Options[idx].ToLower().StartsWith("/o:") &&
                    this.Options[idx].Length > 3)
                {
                    Configuration.OutputFilePath = this.Options[idx].Substring(3);
                }
                else if (this.Options[idx].ToLower().Equals("/noparsing"))
                {
                    Configuration.NoParsing = true;
                }
                else if (this.Options[idx].ToLower().Equals("/nocompile"))
                {
                    Configuration.NoCompilation = true;
                }
                else if (this.Options[idx].ToLower().StartsWith("/timeout:") &&
                    this.Options[idx].Length > 9)
                {
                    int i = 0;
                    if (!int.TryParse(this.Options[idx].Substring(9), out i) &&
                        i > 0)
                    {
                        ErrorReporter.ReportAndExit("Please give a valid timeout " +
                            "'/timeout:[x]', where [x] > 0.");
                    }

                    Configuration.AnalysisTimeout = i;
                }
                else if (this.Options[idx].ToLower().StartsWith("/v:") &&
                    this.Options[idx].Length > 3)
                {
                    int i = 0;
                    if (!int.TryParse(this.Options[idx].Substring(3), out i) &&
                        i >= 0 && i <= 3)
                    {
                        ErrorReporter.ReportAndExit("Please give a valid verbosity level " +
                            "'/v:[x]', where 0 <= [x] <= 3.");
                    }

                    Configuration.Verbose = i;
                }
                else if (this.Options[idx].ToLower().Equals("/debug"))
                {
                    Configuration.Debugging.Add(DebugType.Any);
                }
                else if (this.Options[idx].ToLower().StartsWith("/debug:") &&
                    this.Options[idx].Length > 7)
                {
                    if (this.Options[idx].Substring(7).ToLower().Equals("all"))
                    {
                        Configuration.Debugging.Add(DebugType.Any);
                    }
                    else if (this.Options[idx].Substring(7).ToLower().Equals("analysis"))
                    {
                        Configuration.Debugging.Add(DebugType.Analysis);
                    }
                    else if (this.Options[idx].Substring(7).ToLower().Equals("testing"))
                    {
                        Configuration.Debugging.Add(DebugType.Testing);
                    }
                    else if (this.Options[idx].Substring(7).ToLower().Equals("liveness"))
                    {
                        Configuration.Debugging.Add(DebugType.Liveness);
                    }
                    else
                    {
                        ErrorReporter.ReportAndExit("Please give a valid debug target '/debug:[x]', " +
                            "where [x] is 'all', 'analysis', 'testing' or 'liveness'.");
                    }
                }

                #endregion

                #region compilation options

                else if (this.Options[idx].ToLower().Equals("/distributed"))
                {
                    Configuration.CompileForDistribution = true;
                }

                #endregion

                #region static analysis options

                else if (this.Options[idx].ToLower().Equals("/analyze"))
                {
                    Configuration.RunStaticAnalysis = true;
                }
                else if (this.Options[idx].ToLower().Equals("/showwarnings"))
                {
                    Configuration.ShowWarnings = true;
                }
                else if (this.Options[idx].ToLower().Equals("/showgivesup"))
                {
                    Configuration.ShowGivesUpInformation = true;
                }
                else if (this.Options[idx].ToLower().Equals("/showstatistics") ||
                    this.Options[idx].ToLower().Equals("/stats"))
                {
                    Configuration.ShowProgramStatistics = true;
                }
                else if (this.Options[idx].ToLower().Equals("/time"))
                {
                    Configuration.ShowRuntimeResults = true;
                }
                else if (this.Options[idx].ToLower().Equals("/timedfa"))
                {
                    Configuration.ShowDFARuntimeResults = true;
                }
                else if (this.Options[idx].ToLower().Equals("/timeroa"))
                {
                    Configuration.ShowROARuntimeResults = true;
                }
                else if (this.Options[idx].ToLower().Equals("/nostatetransitionanalysis"))
                {
                    Configuration.DoStateTransitionAnalysis = false;
                }
                else if (this.Options[idx].ToLower().Equals("/analyzeexceptions"))
                {
                    Configuration.AnalyzeExceptionHandling = true;
                }

                #endregion

                #region dynamic analysis options

                else if (this.Options[idx].ToLower().Equals("/test"))
                {
                    Configuration.RunDynamicAnalysis = true;
                }
                else if (this.Options[idx].ToLower().StartsWith("/test:") &&
                    this.Options[idx].Length > 6)
                {
                    Configuration.RunDynamicAnalysis = true;
                    Configuration.ProjectName = this.Options[idx].Substring(6);
                }
                else if (this.Options[idx].ToLower().StartsWith("/sch:") &&
                    this.Options[idx].Length > 5)
                {
                    Configuration.SchedulingStrategy = this.Options[idx].Substring(5);
                }
                else if (this.Options[idx].ToLower().StartsWith("/i:") &&
                    this.Options[idx].Length > 3)
                {
                    int i = 0;
                    if (!int.TryParse(this.Options[idx].Substring(3), out i) &&
                        i > 0)
                    {
                        ErrorReporter.ReportAndExit("Please give a valid number of iterations " +
                            "'/i:[x]', where [x] > 0.");
                    }

                    Configuration.SchedulingIterations = i;
                }
                else if (this.Options[idx].ToLower().Equals("/explore"))
                {
                    Configuration.FullExploration = true;
                }


                else if (this.Options[idx].ToLower().StartsWith("/sch-seed:") &&
                    this.Options[idx].Length > 10)
                {
                    int seed;
                    if (!int.TryParse(this.Options[idx].Substring(10), out seed))
                    {
                        ErrorReporter.ReportAndExit("Please give a valid random scheduling " +
                            "seed '/sch-seed:[x]', where [x] is a signed 32-bit integer.");
                    }

                    Configuration.RandomSchedulingSeed = seed;
                }
                else if (this.Options[idx].ToLower().StartsWith("/db:") &&
                    this.Options[idx].Length > 4)
                {
                    int i = 0;
                    if (!int.TryParse(this.Options[idx].Substring(4), out i) &&
                        i >= 0)
                    {
                        ErrorReporter.ReportAndExit("Please give a valid exploration depth " +
                            "bound '/db:[x]', where [x] >= 0.");
                    }

                    Configuration.DepthBound = i;
                }
                else if (this.Options[idx].ToLower().StartsWith("/prefix:") &&
                    this.Options[idx].Length > 8)
                {
                    int i = 0;
                    if (!int.TryParse(this.Options[idx].Substring(8), out i) &&
                        i >= 0)
                    {
                        ErrorReporter.ReportAndExit("Please give a valid safety prefix " +
                            "bound '/prefix:[x]', where [x] >= 0.");
                    }

                    Configuration.SafetyPrefixBound = i;
                }
                else if (this.Options[idx].ToLower().Equals("/tpl"))
                {
                    Configuration.ScheduleIntraMachineConcurrency = true;
                }
                else if (this.Options[idx].ToLower().Equals("/liveness"))
                {
                    Configuration.CheckLiveness = true;
                }
                else if (this.Options[idx].ToLower().Equals("/printtrace"))
                {
                    Configuration.PrintTrace = true;
                }
                else if (this.Options[idx].ToLower().Equals("/nocaching"))
                {
                    Configuration.CacheProgramState = false;
                }

                #endregion

                #region error

                else
                {
                    this.ShowHelp();
                    ErrorReporter.ReportAndExit("cannot recognise command line option '" +
                        this.Options[idx] + "'.");
                }

                #endregion
            }

            this.CheckForParsingErrors();
        }

        #endregion

        #region private methods

        /// <summary>
        /// Checks for parsing errors.
        /// </summary>
        private void CheckForParsingErrors()
        {
            if (Configuration.SolutionFilePath.Equals(""))
            {
                ErrorReporter.ReportAndExit("Please give a valid solution path.");
            }

            if (!Configuration.SchedulingStrategy.Equals("") &&
                !Configuration.SchedulingStrategy.Equals("random") &&
                !Configuration.SchedulingStrategy.Equals("dfs") &&
                !Configuration.SchedulingStrategy.Equals("iddfs") &&
                !Configuration.SchedulingStrategy.Equals("macemc"))
            {
                ErrorReporter.ReportAndExit("Please give a valid scheduling strategy " +
                    "'/sch:[x]', where [x] is 'random', 'dfs' or 'iddfs'.");
            }
        }

        /// <summary>
        /// Shows help.
        /// </summary>
        private void ShowHelp()
        {
            string help = "\n";

            help += "--------------";
            help += "\nBasic options:";
            help += "\n--------------";
            help += "\n  /?\t\t Show this help menu";
            help += "\n  /s:[x]\t Path to a P# solution";
            help += "\n  /p:[x]\t Name of a project in the P# solution";
            help += "\n  /o:[x]\t Path for output files";
            help += "\n  /timeout:[x]\t Timeout for the tool (default is no timeout)";
            help += "\n  /v:[x]\t Enable verbose mode (values from '0' to '3')";
            help += "\n  /debug\t Enable debugging";

            help += "\n\n--------------------";
            help += "\nCompilation options:";
            help += "\n--------------------";
            help += "\n  /ditributed\t Compile the P# program using the distributed runtime";

            help += "\n\n---------------------------";
            help += "\nSystematic testing options:";
            help += "\n---------------------------";
            help += "\n  /test\t\t Enable the systematic testing mode to find bugs";
            help += "\n  /i:[x]\t Number of schedules to explore for bugs";
            help += "\n  /sch:[x]\t Choose a systematic testing strategy ('random' by default)";
            help += "\n  /db:[x]\t Depth bound to be explored ('10000' by default)";
            help += "\n  /liveness\t Enable liveness property checking";
            help += "\n  /sch-seed:[x]\t Choose a scheduling seed (signed 32-bit integer)";

            help += "\n\n---------------------------";
            help += "\nExperimental options:";
            help += "\n---------------------------";
            help += "\n  /tpl\t Enable intra-machine concurrency scheduling";

            help += "\n";

            Output.PrettyPrintLine(help);
        }

        #endregion
    }
}
