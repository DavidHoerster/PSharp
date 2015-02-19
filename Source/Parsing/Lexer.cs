﻿//-----------------------------------------------------------------------
// <copyright file="Lexer.cs">
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
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.PSharp.Tooling;

namespace Microsoft.PSharp.Parsing
{
    /// <summary>
    /// The P# lexer.
    /// </summary>
    internal class Lexer
    {
        #region fields

        /// <summary>
        /// Lines of tokens.
        /// </summary>
        protected List<Token> Tokens;

        /// <summary>
        /// Lines of the original text.
        /// </summary>
        protected List<string> Lines;

        /// <summary>
        /// The current line index.
        /// </summary>
        protected int LineIndex;

        /// <summary>
        /// The current index.
        /// </summary>
        protected int Index;

        /// <summary>
        /// The name of the currently parsed machine.
        /// </summary>
        protected string CurrentMachine;

        #endregion

        #region public API

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="text">Text</param>
        public Lexer(string text)
        {
            this.Tokens = new List<Token>();
            this.Lines = new List<string>();
            this.LineIndex = 1;
            this.Index = 0;
            this.CurrentMachine = "";

            var textUnits = new List<TextUnit>();
            using (StringReader sr = new StringReader(text))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    this.Lines.Add(line);
                    var split = this.SplitText(line);
                    foreach (var tok in split)
                    {
                        if (tok.Equals(""))
                        {
                            continue;
                        }

                        textUnits.Add(new TextUnit(tok));
                    }

                    textUnits.Add(new TextUnit(true));
                }
            }

            this.Tokenize(textUnits);
        }

        /// <summary>
        /// Returns the tokens.
        /// </summary>
        public List<Token> GetTokens()
        {
            return this.Tokens;
        }

        #endregion

        #region private API

        /// <summary>
        /// Splits the given text using a regex pattern and returns the split text.
        /// </summary>
        /// <param name="text">Text</param>
        /// <returns>Tokenized text</returns>
        private string[] SplitText(string text)
        {
            return Regex.Split(text, this.GetPattern());
        }

        /// <summary>
        /// Tokenizes the text units.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void Tokenize(List<TextUnit> textUnits)
        {
            while (this.Index < textUnits.Count)
            {
                this.TokenizeWhiteSpaceOrComments(textUnits);

                switch (textUnits[this.Index].Text)
                {
                    case "//":
                    case "#":
                        this.TryTokenizeLineComment(textUnits);
                        break;

                    case "/*":
                        this.TryTokenizeMultiLineComment(textUnits);
                        break;

                    case "using":
                        this.TokenizeUsingDirective(textUnits);
                        break;

                    case "namespace":
                        this.TokenizeNamespace(textUnits);
                        break;

                    case "]":
                    case "(":
                    case ")":
                    case "{":
                    case "*/":
                        this.ReportParsingError("Invalid use of \"" + textUnits[this.Index].Text + "\".");
                        break;

                    default:
                        Console.WriteLine(" >> " + textUnits[this.Index].Text);
                        this.ReportParsingError("Must be declared inside a namespace.");
                        break;
                }
            }
        }

        /// <summary>
        /// Tokenizes the given using directive.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeUsingDirective(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Using));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            while (!textUnits[this.Index].Text.Equals(";"))
            {
                if (!Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text));
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("."))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Dot));
                    this.Index++;
                }
                else
                {
                    this.ReportParsingError("Invalid use of the \"using\" directive.");
                }

                this.TokenizeWhiteSpaceOrComments(textUnits);
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Semicolon));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
        }

        /// <summary>
        /// Tokenizes the given namespace.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeNamespace(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.NamespaceDecl));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
            {
                this.ReportParsingError("Expected identifier.");
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (!textUnits[this.Index].Text.Equals("{"))
            {
                this.ReportParsingError("Missing \"{\".");
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.LeftCurlyBracket));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);

            bool end = false;
            while (this.Index < textUnits.Count && !end)
            {
                switch (textUnits[this.Index].Text)
                {
                    case "//":
                    case "#":
                        this.TryTokenizeLineComment(textUnits);
                        break;

                    case "/*":
                        this.TryTokenizeMultiLineComment(textUnits);
                        break;

                    case "private":
                    case "protected":
                    case "internal":
                    case "public":
                        this.TokenizeTopLevelAccessModifier(textUnits);
                        this.TokenizeTopLevelAbstractModifier(textUnits);
                        break;

                    case "abstract":
                        this.TokenizeTopLevelAbstractModifier(textUnits);
                        this.TokenizeTopLevelAccessModifier(textUnits);
                        break;

                    case "[":
                        this.TokenizeAttributeList(textUnits);
                        break;

                    case "}":
                        this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.RightCurlyBracket));
                        end = true;
                        this.Index++;
                        this.TokenizeWhiteSpaceOrComments(textUnits);
                        break;

                    case "]":
                    case "(":
                    case ")":
                    case "{":
                    case "*/":
                        this.ReportParsingError("Invalid use of \"" + textUnits[this.Index].Text + "\".");
                        break;

                    default:
                        this.ReportParsingError("Unexpected declaration.");
                        break;
                }
            }
        }

        /// <summary>
        /// Tokenizes a top level access modifier.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeTopLevelAccessModifier(List<TextUnit> textUnits)
        {
            if (textUnits[this.Index].Text.Equals("private"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Private));
            }
            else if (textUnits[this.Index].Text.Equals("protected"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Protected));
            }
            else if (textUnits[this.Index].Text.Equals("internal"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Internal));
            }
            else if (textUnits[this.Index].Text.Equals("public"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Public));
            }
            else
            {
                return;
            }

            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (textUnits[this.Index].Text.Equals("private") ||
                textUnits[this.Index].Text.Equals("protected") ||
                textUnits[this.Index].Text.Equals("internal") ||
                textUnits[this.Index].Text.Equals("public"))
            {
                this.ReportParsingError("More than one access modifier.");
            }

            if (textUnits[this.Index].Text.Equals("event"))
            {
                this.TokenizeEventDeclaration(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals("machine"))
            {
                this.TokenizeMachineDeclaration(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals("class"))
            {
                this.TokenizeClassDeclaration(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals("struct"))
            {
                this.TokenizeStructDeclaration(textUnits);
            }
        }

        /// <summary>
        /// Tokenizes a top level abstract modifier.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeTopLevelAbstractModifier(List<TextUnit> textUnits)
        {
            if (!textUnits[this.Index].Text.Equals("abstract"))
            {
                return;
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Abstract));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (textUnits[this.Index].Text.Equals("abstract"))
            {
                this.ReportParsingError("Duplicate \"abstract\" modifier.");
            }

            if (textUnits[this.Index].Text.Equals("event"))
            {
                this.TokenizeEventDeclaration(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals("machine"))
            {
                this.TokenizeMachineDeclaration(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals("class"))
            {
                this.TokenizeClassDeclaration(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals("struct"))
            {
                this.TokenizeStructDeclaration(textUnits);
            }
        }

        /// <summary>
        /// Tokenizes an event declaration.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeEventDeclaration(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.EventDecl));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
            {
                this.ReportParsingError("Expected event identifier.");
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.EventIdentifier));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (!textUnits[this.Index].Text.Equals(";"))
            {
                this.ReportParsingError("Missing \";\".");
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Semicolon));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
        }

        /// <summary>
        /// Tokenizes a machine declaration.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeMachineDeclaration(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.MachineDecl));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
            {
                this.ReportParsingError("Expected machine identifier.");
            }

            this.CurrentMachine = textUnits[this.Index].Text;

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.MachineIdentifier));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (textUnits[this.Index].Text.Equals(":"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Doublecolon));
                this.Index++;

                this.TokenizeWhiteSpaceOrComments(textUnits);
                if (Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
                {
                    this.ReportParsingError("Expected identifier.");
                }

                this.Tokens.Add(new Token(textUnits[this.Index].Text));
                this.Index++;

                this.TokenizeWhiteSpaceOrComments(textUnits);
            }

            if (textUnits[this.Index].Text.Equals("{"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.MachineLeftCurlyBracket));
                this.Index++;
            }
            else
            {
                this.ReportParsingError("Expected \"{\".");
            }

            this.TokenizeWhiteSpaceOrComments(textUnits);

            bool end = false;
            while (this.Index < textUnits.Count && !end)
            {
                switch (textUnits[this.Index].Text)
                {
                    case "//":
                    case "#":
                        this.TryTokenizeLineComment(textUnits);
                        break;

                    case "/*":
                        this.TryTokenizeMultiLineComment(textUnits);
                        break;

                    case "private":
                    case "protected":
                        this.TokenizeMachineLevelAccessModifier(textUnits);
                        break;

                    case "internal":
                    case "public":
                        this.ReportParsingError("Machine fields, states or actions must be private or protected.");
                        break;

                    case "abstract":
                        this.ReportParsingError("Machine fields, states or actions cannot be abstract.");
                        break;

                    case "[":
                        this.TokenizeAttributeList(textUnits);
                        break;

                    case "}":
                        this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.MachineRightCurlyBracket));
                        end = true;
                        this.Index++;
                        this.TokenizeWhiteSpaceOrComments(textUnits);
                        break;

                    case "]":
                    case "(":
                    case ")":
                    case "{":
                    case "*/":
                        this.ReportParsingError("Invalid use of \"" + textUnits[this.Index].Text + "\".");
                        break;

                    default:
                        this.ReportParsingError("Unexpected declaration.");
                        break;
                }
            }
        }

        /// <summary>
        /// Tokenizes a machine level access modifier.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeMachineLevelAccessModifier(List<TextUnit> textUnits)
        {
            if (textUnits[this.Index].Text.Equals("private"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Private));
            }
            else if (textUnits[this.Index].Text.Equals("protected"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Protected));
            }
            else
            {
                return;
            }

            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (textUnits[this.Index].Text.Equals("private") ||
                textUnits[this.Index].Text.Equals("protected") ||
                textUnits[this.Index].Text.Equals("internal") ||
                textUnits[this.Index].Text.Equals("public"))
            {
                this.ReportParsingError("More than one access modifier.");
            }
            else if (textUnits[this.Index].Text.Equals("abstract"))
            {
                this.ReportParsingError("Machine fields, states or actions cannot be abstract.");
            }

            if (textUnits[this.Index].Text.Equals("override"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Override));
                this.Index++;

                this.TokenizeWhiteSpaceOrComments(textUnits);
            }

            if (textUnits[this.Index].Text.Equals("state"))
            {
                this.TokenizeStateDeclaration(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals("action"))
            {
                this.TokenizeActionDeclaration(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals("class"))
            {
                this.ReportParsingError("Classes cannot be declared inside a machine.");
            }
            else if (textUnits[this.Index].Text.Equals("struct"))
            {
                this.ReportParsingError("Structs cannot be declared inside a machine.");
            }
            else if (textUnits[this.Index].Text.Equals("override"))
            {
                this.ReportParsingError("Duplicate \"override\" modifier.");
            }
            else if (!Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text));
                this.Index++;

                this.TokenizeWhiteSpaceOrComments(textUnits);
                if (textUnits[this.Index].Text.Equals("<"))
                {
                    this.TokenizeLessThanOperator(textUnits);
                }

                if (Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
                {
                    this.ReportParsingError("Expected identifier.");
                }

                if (!ParsingEngine.MachineFieldsAndMethods.ContainsKey(this.CurrentMachine))
                {
                    ParsingEngine.MachineFieldsAndMethods.Add(this.CurrentMachine,
                        new HashSet<string>());
                }

                ParsingEngine.MachineFieldsAndMethods[this.CurrentMachine].Add(textUnits[this.Index].Text);

                while (this.Index < textUnits.Count &&
                    !textUnits[this.Index].Text.Equals(";") &&
                    !textUnits[this.Index].Text.Equals("("))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text));
                    this.Index++;
                    this.TokenizeWhiteSpaceOrComments(textUnits);
                }

                if (textUnits[this.Index].Text.Equals("("))
                {
                    this.TokenizeMethodDeclaration(textUnits);
                }
                else if (textUnits[this.Index].Text.Equals(";"))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Semicolon));
                    this.Index++;
                }
            }
            else
            {
                this.ReportParsingError("Expected identifier.");
            }

            this.TokenizeWhiteSpaceOrComments(textUnits);
        }

        /// <summary>
        /// Tokenizes a state declaration.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeStateDeclaration(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.StateDecl));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
            {
                this.ReportParsingError("Expected state identifier.");
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.StateIdentifier));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (textUnits[this.Index].Text.Equals("{"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.StateLeftCurlyBracket));
                this.Index++;
            }
            else
            {
                this.ReportParsingError("Expected \"{\".");
            }

            this.TokenizeWhiteSpaceOrComments(textUnits);

            bool end = false;
            while (this.Index < textUnits.Count && !end)
            {
                switch (textUnits[this.Index].Text)
                {
                    case "//":
                    case "#":
                        this.TryTokenizeLineComment(textUnits);
                        break;

                    case "/*":
                        this.TryTokenizeMultiLineComment(textUnits);
                        break;

                    case "entry":
                    case "exit":
                        this.TokenizeStateEntryOrExitDeclaration(textUnits);
                        break;

                    case "on":
                        this.TokenizeStateActionDeclaration(textUnits);
                        break;

                    case "defer":
                        this.TokenizeDeferEventsDeclaration(textUnits);
                        break;

                    case "ignore":
                        this.TokenizeIgnoreEventsDeclaration(textUnits);
                        break;

                    case "private":
                    case "protected":
                    case "internal":
                    case "public":
                        this.ReportParsingError("State actions cannot have modifiers.");
                        break;

                    case "abstract":
                        this.ReportParsingError("State actions cannot be abstract.");
                        break;

                    case "[":
                        this.TokenizeAttributeList(textUnits);
                        break;

                    case "}":
                        this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.StateRightCurlyBracket));
                        end = true;
                        this.Index++;
                        this.TokenizeWhiteSpaceOrComments(textUnits);
                        break;

                    case "]":
                    case "(":
                    case ")":
                    case "{":
                    case "*/":
                        this.ReportParsingError("Invalid use of \"" + textUnits[this.Index].Text + "\".");
                        break;

                    default:
                        this.ReportParsingError("Unexpected declaration.");
                        break;
                }
            }
        }

        /// <summary>
        /// Tokenizes a state entry or exit declaration.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeStateEntryOrExitDeclaration(List<TextUnit> textUnits)
        {
            if (textUnits[this.Index].Text.Equals("entry"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Entry));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("exit"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Exit));
                this.Index++;
            }

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (!textUnits[this.Index].Text.Equals("{"))
            {
                this.ReportParsingError("Expected \"{\".");
            }

            this.TokenizeCodeRegion(textUnits);
        }

        /// <summary>
        /// Tokenizes a state action declaration.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeStateActionDeclaration(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.OnAction));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (!Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.EventIdentifier));
                this.Index++;
            }
            else
            {
                this.ReportParsingError("Expected event identifier.");
            }

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (textUnits[this.Index].Text.Equals("do"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.DoAction));
                this.Index++;

                this.TokenizeWhiteSpaceOrComments(textUnits);
                if (Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
                {
                    this.ReportParsingError("Expected action identifier.");
                }

                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.ActionIdentifier));
                this.Index++;

                this.TokenizeWhiteSpaceOrComments(textUnits);
                if (!textUnits[this.Index].Text.Equals(";"))
                {
                    this.ReportParsingError("Expected \";\".");
                }

                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Semicolon));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("goto"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.GotoState));
                this.Index++;

                this.TokenizeWhiteSpaceOrComments(textUnits);
                if (Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
                {
                    this.ReportParsingError("Expected state identifier.");
                }

                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.StateIdentifier));
                this.Index++;

                this.TokenizeWhiteSpaceOrComments(textUnits);
                if (!textUnits[this.Index].Text.Equals(";"))
                {
                    this.ReportParsingError("Expected \";\".");
                }

                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Semicolon));
                this.Index++;
            }
            else
            {
                this.ReportParsingError("Expected \"do\" or \"goto\".");
            }

            this.TokenizeWhiteSpaceOrComments(textUnits);
        }

        /// <summary>
        /// Tokenizes a defer events declaration.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeDeferEventsDeclaration(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.DeferEvent));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            while (this.Index < textUnits.Count && !textUnits[this.Index].Text.Equals(";"))
            {
                if (!Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.EventIdentifier));
                }
                else if (textUnits[this.Index].Text.Equals(","))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Comma));
                }
                else
                {
                    this.ReportParsingError("Expected event identifier.");
                }

                this.Index++;
                this.TokenizeWhiteSpaceOrComments(textUnits, true);
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Semicolon));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
        }

        /// <summary>
        /// Tokenizes an ignore events declaration.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeIgnoreEventsDeclaration(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.IgnoreEvent));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            while (this.Index < textUnits.Count && !textUnits[this.Index].Text.Equals(";"))
            {
                if (!Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.EventIdentifier));
                }
                else if (textUnits[this.Index].Text.Equals(","))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Comma));
                }
                else
                {
                    this.ReportParsingError("Expected event identifier.");
                }

                this.Index++;
                this.TokenizeWhiteSpaceOrComments(textUnits, true);
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Semicolon));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
        }

        /// <summary>
        /// Tokenizes an action declaration.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeActionDeclaration(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.ActionDecl));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
            {
                this.ReportParsingError("Expected action identifier.");
            }

            if (!ParsingEngine.MachineFieldsAndMethods.ContainsKey(this.CurrentMachine))
            {
                ParsingEngine.MachineFieldsAndMethods.Add(this.CurrentMachine,
                    new HashSet<string>());
            }

            ParsingEngine.MachineFieldsAndMethods[this.CurrentMachine].Add(textUnits[this.Index].Text);

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.ActionIdentifier));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (textUnits[this.Index].Text.Equals("{"))
            {
                this.TokenizeCodeRegion(textUnits);
            }
            else
            {
                this.ReportParsingError("Expected \"{\".");
            }
        }

        /// <summary>
        /// Tokenizes a class declaration.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeClassDeclaration(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.ClassDecl));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            while (this.Index < textUnits.Count && !textUnits[this.Index].Text.Equals("{"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text));
                this.Index++;
                this.TokenizeWhiteSpaceOrComments(textUnits);
            }

            this.TokenizeGenericCodeRegion(textUnits);
        }

        /// <summary>
        /// Tokenizes a struct declaration.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeStructDeclaration(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.StructDecl));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            while (this.Index < textUnits.Count && !textUnits[this.Index].Text.Equals("{"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text));
                this.Index++;
                this.TokenizeWhiteSpaceOrComments(textUnits);
            }

            this.TokenizeGenericCodeRegion(textUnits);
        }

        /// <summary>
        /// Tokenizes a method declaration.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeMethodDeclaration(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.LeftParenthesis));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            while (this.Index < textUnits.Count && !textUnits[this.Index].Text.Equals(")"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text));
                this.Index++;
                this.TokenizeWhiteSpaceOrComments(textUnits);
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.RightParenthesis));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);

            if (textUnits[this.Index].Text.Equals(";"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Semicolon));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("{"))
            {
                this.TokenizeCodeRegion(textUnits);
            }
            else
            {
                this.ReportParsingError("Expected \";\" or \"{\".");
            }
        }

        /// <summary>
        /// Tokenizes a code region surrounded by curly brackets.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeCodeRegion(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.LeftCurlyBracket));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);

            int bracketCounter = 1;
            while (this.Index < textUnits.Count && bracketCounter > 0)
            {
                if (textUnits[this.Index].Text.Equals("{"))
                {
                    bracketCounter++;
                }
                else if (textUnits[this.Index].Text.Equals("}"))
                {
                    bracketCounter--;
                }

                this.TokenizeNextTextUnit(textUnits);
            }
        }

        /// <summary>
        /// Tokenizes a new statement.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeNewStatement(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.New));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            while (this.Index < textUnits.Count && !textUnits[this.Index].Text.Equals("("))
            {
                if (!Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.TypeIdentifier));
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("."))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Dot));
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("<"))
                {
                    this.TokenizeLessThanOperator(textUnits);
                }
                else
                {
                    this.ReportParsingError("Expected type identifier.");
                }
            }
        }

        /// <summary>
        /// Tokenizes a create statement.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeCreateStatement(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.CreateMachine));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            while (this.Index < textUnits.Count && (!textUnits[this.Index].Text.Equals("{") &&
                !textUnits[this.Index].Text.Equals(";")))
            {
                if (textUnits[this.Index].Text.Equals("."))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Dot));
                }
                else if (!Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.MachineIdentifier));
                }
                else if (!String.IsNullOrWhiteSpace(textUnits[this.Index].Text))
                {
                    this.ReportParsingError("Expected machine identifier.");
                }

                this.Index++;
            }

            if (textUnits[this.Index].Text.Equals(";"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Semicolon));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("{"))
            {
                this.TokenizeArgumentsList(textUnits);
            }
            else
            {
                this.ReportParsingError("Expected \";\" or \"{\".");
            }
        }

        /// <summary>
        /// Tokenizes a raise statement.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeRaiseStatement(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.RaiseEvent));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
            {
                this.ReportParsingError("Expected event identifier.");
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.EventIdentifier));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (textUnits[this.Index].Text.Equals("{"))
            {
                this.TokenizeArgumentsList(textUnits);
            }

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (!textUnits[this.Index].Text.Equals(";"))
            {
                this.ReportParsingError("Expected \";\".");
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Semicolon));
            this.Index++;
        }

        /// <summary>
        /// Tokenizes a send statement.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeSendStatement(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.SendEvent));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
            {
                this.ReportParsingError("Expected event identifier.");
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.EventIdentifier));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (textUnits[this.Index].Text.Equals("{"))
            {
                this.TokenizeArgumentsList(textUnits);
            }

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (!textUnits[this.Index].Text.Equals("to"))
            {
                this.ReportParsingError("Expected \"to\".");
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.ToMachine));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);

            while (this.Index < textUnits.Count && !textUnits[this.Index].Text.Equals(";"))
            {
                if (textUnits[this.Index].Text.Equals("."))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Dot));
                }
                else if (textUnits[this.Index].Text.Equals("this"))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.This));
                }
                else if (!Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Identifier));
                }
                else if (!String.IsNullOrWhiteSpace(textUnits[this.Index].Text))
                {
                    this.ReportParsingError("Expected identifier.");
                }

                this.Index++;
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Semicolon));
            this.Index++;
        }

        /// <summary>
        /// Tokenizes a delete statement.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeDeleteStatement(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.DeleteMachine));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            if (!textUnits[this.Index].Text.Equals(";"))
            {
                this.ReportParsingError("Expected \";\".");
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Semicolon));
            this.Index++;
        }

        /// <summary>
        /// Tokenizes the next text unit.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeNextTextUnit(List<TextUnit> textUnits)
        {
            if (textUnits[this.Index].Text.Equals("{"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.LeftCurlyBracket));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("}"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.RightCurlyBracket));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("("))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.LeftParenthesis));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals(")"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.RightParenthesis));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals(","))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Comma));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals(";"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Semicolon));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals(":"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Doublecolon));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("."))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Dot));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("&"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.AndOperator));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("|"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.OrOperator));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("!"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.NotOperator));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("="))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.EqualOperator));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("<"))
            {
                this.TokenizeLessThanOperator(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals(">"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.GreaterThanOperator));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("+"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.PlusOperator));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("-"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.MinusOperator));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("*"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.MultiplyOperator));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("/"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.DivideOperator));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("%"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.ModOperator));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("new"))
            {
                this.TokenizeNewStatement(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals("as"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.As));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("for"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.ForLoop));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("while"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.WhileLoop));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("do"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.DoLoop));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("if"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.IfCondition));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("else"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.ElseCondition));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("break"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Break));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("continue"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Continue));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("return"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Return));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("this"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.This));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("base"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Base));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("payload"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Payload));
                this.Index++;
            }
            else if (textUnits[this.Index].Text.Equals("create"))
            {
                this.TokenizeCreateStatement(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals("raise"))
            {
                this.TokenizeRaiseStatement(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals("send"))
            {
                this.TokenizeSendStatement(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals("delete"))
            {
                this.TokenizeDeleteStatement(textUnits);
            }
            else if (textUnits[this.Index].Text.Equals("assert"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Assert));
                this.Index++;
            }
            else
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Identifier));
                this.Index++;
            }

            this.TokenizeWhiteSpaceOrComments(textUnits);
        }

        /// <summary>
        /// Tokenizes an attribute list.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeAttributeList(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.LeftSquareBracket));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            while (this.Index < textUnits.Count && !textUnits[this.Index].Text.Equals("]"))
            {
                this.Tokens.Add(new Token(textUnits[this.Index].Text));
                this.Index++;
                this.TokenizeWhiteSpaceOrComments(textUnits);
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.RightSquareBracket));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
        }

        /// <summary>
        /// Tokenizes an argument list.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeArgumentsList(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.LeftCurlyBracket));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits, true);
            while (this.Index < textUnits.Count && !textUnits[this.Index].Text.Equals("}"))
            {
                if (textUnits[this.Index].Text.Equals(","))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Comma));
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("."))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Dot));
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("!"))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.NotOperator));
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("+"))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.PlusOperator));
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("-"))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.MinusOperator));
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("*"))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.MultiplyOperator));
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("/"))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.DivideOperator));
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("%"))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.ModOperator));
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("new"))
                {
                    this.TokenizeNewStatement(textUnits);
                }
                else if (textUnits[this.Index].Text.Equals("this"))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.This));
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("base"))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Base));
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("payload"))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Payload));
                    this.Index++;
                }
                else if (!Regex.IsMatch(textUnits[this.Index].Text, this.GetPattern()))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.Identifier));
                    this.Index++;
                }
                else if (!String.IsNullOrWhiteSpace(textUnits[this.Index].Text))
                {
                    this.ReportParsingError("Unexpected \"" + textUnits[this.Index].Text + "\".");
                }
                else
                {
                    this.Index++;
                }
            }

            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.RightCurlyBracket));
            this.Index++;
        }

        /// <summary>
        /// Tokenizes the less than operator.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeLessThanOperator(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.LessThanOperator));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);
            this.TryTokenizeGenericList(textUnits);
        }

        /// <summary>
        /// Tries to tokenizes a generic list, if any.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TryTokenizeGenericList(List<TextUnit> textUnits)
        {
            var tokens = new List<Token>();
            var currentIdx = this.Index;

            while (currentIdx < textUnits.Count && !textUnits[currentIdx].Text.Equals(">"))
            {
                if (textUnits[currentIdx].Text.Equals(";"))
                {
                    return;
                }

                tokens.Add(new Token(textUnits[currentIdx].Text));
                currentIdx++;

                this.TokenizeWhiteSpaceOrComments(textUnits);
            }

            tokens.Add(new Token(textUnits[currentIdx].Text, TokenType.GreaterThanOperator));
            currentIdx++;

            this.Tokens.AddRange(tokens);
            this.Index = currentIdx;

            this.TokenizeWhiteSpaceOrComments(textUnits);
        }

        /// <summary>
        /// Tokenizes a generic region of code surrounded by curly brackets.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private void TokenizeGenericCodeRegion(List<TextUnit> textUnits)
        {
            this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.LeftCurlyBracket));
            this.Index++;

            this.TokenizeWhiteSpaceOrComments(textUnits);

            int bracketCounter = 1;
            while (this.Index < textUnits.Count && bracketCounter > 0)
            {
                if (textUnits[this.Index].Text.Equals("{"))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.LeftCurlyBracket));
                    bracketCounter++;
                    this.Index++;
                }
                else if (textUnits[this.Index].Text.Equals("}"))
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.RightCurlyBracket));
                    bracketCounter--;
                    this.Index++;
                }
                else
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text));
                    this.Index++;
                }

                this.TokenizeWhiteSpaceOrComments(textUnits);
            }
        }

        /// <summary>
        /// Tokenizes white space or comments, if any.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        /// <param name="skip">True to skip white space</param>
        private void TokenizeWhiteSpaceOrComments(List<TextUnit> textUnits, bool skip = false)
        {
            while (this.Index < textUnits.Count)
            {
                var repeat = this.TryTokenizeLineComment(textUnits);
                repeat = repeat || this.TryTokenizeMultiLineComment(textUnits);
                repeat = repeat || this.TryTokenizeWhiteSpace(textUnits, skip);

                if (!repeat)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Skip a line-wide comment, if any.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private bool TryTokenizeLineComment(List<TextUnit> textUnits)
        {
            if (!textUnits[this.Index].Text.Equals("//") &&
                !textUnits[this.Index].Text.Equals("#"))
            {
                return false;
            }

            while (this.Index < textUnits.Count && !textUnits[this.Index].IsEndOfLine)
            {
                this.Index++;
            }

            return true;
        }

        /// <summary>
        /// Skip a multi-line comment, if any.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        private bool TryTokenizeMultiLineComment(List<TextUnit> textUnits)
        {
            if (!textUnits[this.Index].Text.Equals("/*"))
            {
                return false;
            }

            while (this.Index < textUnits.Count && !textUnits[this.Index].Text.Equals("*/"))
            {
                this.Index++;
            }

            return true;
        }

        /// <summary>
        /// Tries to tokenize white space, if any.
        /// </summary>
        /// <param name="textUnits">Text units</param>
        /// <param name="skip">True to skip the white space</param>
        private bool TryTokenizeWhiteSpace(List<TextUnit> textUnits, bool skip = false)
        {
            if (!String.IsNullOrWhiteSpace(textUnits[this.Index].Text))
            {
                return false;
            }

            while (this.Index < textUnits.Count &&
                String.IsNullOrWhiteSpace(textUnits[this.Index].Text))
            {
                if (textUnits[this.Index].IsEndOfLine && !skip)
                {
                    this.Tokens.Add(new Token("\n", TokenType.NewLine));
                    this.LineIndex++;
                }
                else if (!skip)
                {
                    this.Tokens.Add(new Token(textUnits[this.Index].Text, TokenType.WhiteSpace));
                }

                this.Index++;
            }

            return true;
        }

        #endregion

        #region helper methods

        /// <summary>
        /// Returns the regex pattern.
        /// </summary>
        /// <returns></returns>
        private string GetPattern()
        {
            var pattern = @"(//|/\*|\*/|;|{|}|:|,|\.|\(|\)|\[|\]|#|\s+|" +
                @"&|\||!|=|<|>|\+|-|\*|/|%|" +
                @"\busing\b|\bnamespace\b|\bclass\b|\bstruct\b|" +
                @"\bmachine\b|\bstate\b|\bevent\b|" +
                @"\bon\b|\bdo\b|\bgoto\b|\bdefer\b|\bignore\b|\bto\b|\bentry\b|\bexit\b|" +
                @"\bcreate\b|\braise\b|\bsend\b|" +
                @"\bprivate\b|\bprotected\b|\binternal\b|\bpublic\b|\babstract\b|\bvirtual\b|\boverride\b|" +
                @"\bnew\b|\bas\b)";
            return pattern;
        }

        /// <summary>
        /// Reports a parting error.
        /// </summary>
        /// <param name="error">Error</param>
        private void ReportParsingError(string error)
        {
            error += " In line " + this.LineIndex + ":\n";
            error += this.Lines[this.LineIndex - 1];
            ErrorReporter.ReportErrorAndExit(error);
        }

        #endregion
    }
}
