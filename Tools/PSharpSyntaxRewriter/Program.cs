﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.PSharp.LanguageServices.Compilation;
using Microsoft.PSharp.LanguageServices.Parsing;
using Microsoft.PSharp.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using Microsoft.Build.Framework;

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using VSLangProj80;
using Microsoft.VisualStudio.OLE.Interop;

namespace PSharpSyntaxRewriter
{
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: PSharpSyntaxRewriter.exe file.psharp");
                return;
            }

            // Get input file as string
            var input_string = "";
            try
            {
                input_string = System.IO.File.ReadAllText(args[0]);
            }
            catch (System.IO.IOException e)
            {
                Console.WriteLine("Error: {0}", e.Message);
                return;
            }

            // Translate and print on console
            Console.WriteLine("{0}", Translate(input_string));
        }

        public static string Translate(string text)
        {
            //System.Diagnostics.Debugger.Launch();
            var configuration = Configuration.Create();
            configuration.Verbose = 2;

            var solution = GetSolution(text);
            var context = CompilationContext.Create(configuration).LoadSolution(solution);

            try
            {
                ParsingEngine.Create(context).Run();
                RewritingEngine.Create(context).Run();

                var syntaxTree = context.GetProjects()[0].PSharpPrograms[0].GetSyntaxTree();

                return syntaxTree.ToString();
            }
            catch (ParsingException)
            {
                return null;
            }
            catch (RewritingException)
            {
                return null;
            }
        }

        static Solution GetSolution(string text, string suffix = "psharp")
        {
            var workspace = new AdhocWorkspace();
            var solutionInfo = SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Create());
            var solution = workspace.AddSolution(solutionInfo);
            var project = workspace.AddProject("Test", "C#");

            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Microsoft.PSharp.Machine).Assembly.Location)
            };

            project = project.AddMetadataReferences(references);
            workspace.TryApplyChanges(project.Solution);

            var sourceText = SourceText.From(text);
            var doc = project.AddDocument("Program", sourceText, null, "Program." + suffix);

            return doc.Project.Solution;
        }
    }

    [ComVisible(true)]
    [Guid(GuidList.guidSimpleFileGeneratorString)]
    [ProvideObject(typeof(PSharpCodeGenerator))]
    [CodeGeneratorRegistration(typeof(PSharpCodeGenerator), "PSharpCodeGenerator", vsContextGuids.vsContextGuidVCSProject, GeneratesDesignTimeSource = true)]
    [CodeGeneratorRegistration(typeof(PSharpCodeGenerator), "PSharpCodeGenerator", vsContextGuids.vsContextGuidVBProject, GeneratesDesignTimeSource = true)]
    public class PSharpCodeGenerator : IVsSingleFileGenerator, IObjectWithSite
    {
        //internal static string name = "PSharpCodeGenerator";

        public int DefaultExtension(out string pbstrDefaultExtension)
        {
            pbstrDefaultExtension = ".cs";
            return VSConstants.S_OK;
        }

        public int Generate(string wszInputFilePath, string bstrInputFileContents, string wszDefaultNamespace, IntPtr[] rgbOutputFileContents, out uint pcbOutput, IVsGeneratorProgress pGenerateProgress)
        {
            if (bstrInputFileContents == null)
                throw new ArgumentException(bstrInputFileContents);

            var bytes = GenerateCode(bstrInputFileContents);

            if (bytes == null)
            {
                rgbOutputFileContents[0] = IntPtr.Zero;
                pcbOutput = 0;
            }
            else
            {
                rgbOutputFileContents[0] = Marshal.AllocCoTaskMem(bytes.Length);
                Marshal.Copy(bytes, 0, rgbOutputFileContents[0], bytes.Length);
                pcbOutput = (uint)bytes.Length;
            }
            return VSConstants.S_OK;
        }

        private object site = null;

        public void GetSite(ref Guid riid, out IntPtr ppvSite)
        {
            if (site == null)
                Marshal.ThrowExceptionForHR(VSConstants.E_NOINTERFACE);

            // Query for the interface using the site object initially passed to the generator
            IntPtr punk = Marshal.GetIUnknownForObject(site);
            int hr = Marshal.QueryInterface(punk, ref riid, out ppvSite);
            Marshal.Release(punk);
            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(hr);
        }

        public void SetSite(object pUnkSite)
        {
            // Save away the site object for later use
            site = pUnkSite;

            // These are initialized on demand via our private CodeProvider and SiteServiceProvider properties
            //codeDomProvider = null;
            //serviceProvider = null;
        }

        byte[] GenerateCode(string input)
        {
            var output = Program.Translate(input);
            if (output == null) return null;

            return Encoding.UTF8.GetBytes(output);
            /*
            using (System.IO.StringWriter writer = new System.IO.StringWriter(new StringBuilder()))
            {
                writer.WriteLine("{0}", output);

                //Get the Encoding used by the writer. We're getting the WindowsCodePage encoding, 
                //which may not work with all languages
                var enc = Encoding.GetEncoding(writer.Encoding.WindowsCodePage);

                //Get the preamble (byte-order mark) for our encoding
                byte[] preamble = enc.GetPreamble();
                int preambleLength = preamble.Length;

                //Convert the writer contents to a byte array
                byte[] body = enc.GetBytes(writer.ToString());

                //Prepend the preamble to body (store result in resized preamble array)
                Array.Resize<byte>(ref preamble, preambleLength + body.Length);
                Array.Copy(body, 0, preamble, preambleLength, body.Length);

                //Return the combined byte array
                return preamble;
            }
            */
        }

    }

    static class GuidList
    {
        public const string guidSimpleFileGeneratorString = "FBB82BF8-A8BF-442A-8060-159042C0EFFF";
        public static readonly Guid guidSimpleFileGenerator = new Guid(guidSimpleFileGeneratorString);
    }

    public class Rewriter : ITask
    {
        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }

        public ITaskItem[] InputFiles { get; set; }

        [Output]
        public ITaskItem[] OutputFiles { get; set; }

        public bool Execute()
        {
            for (int i = 0; i < InputFiles.Length; i++)
            {
                var inp = System.IO.File.ReadAllText(InputFiles[i].ItemSpec);
                var outp = Program.Translate(inp);
                if (outp == null) return false;
                System.IO.File.WriteAllText(OutputFiles[i].ItemSpec, outp);
            }
            return true;
        }

    }
}
