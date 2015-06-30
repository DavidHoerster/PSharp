P#
====================
P# is a new language for high-reliability asynchronous .NET programming, *co-designed* with a static data race analysis and testing infrastructure. The co-design aspect of P# allows us to combine language design, analysis and testing in a unique way: the state-machine structure of a P# program enables us to create a more precise and scalable static analysis; while the race-freedom guarantees, provided by our analysis, contribute to the feasibility of systematically exploring a P# program to find bugs (e.g. assertion failures and unhandled exceptions).

## Build instructions
1. Get Visual Studio 2015 Preview (required for Microsoft Roslyn).
2. Clone this project and compile using VS2015.
3. Get the [Visual Studio 2015 SDK](https://www.microsoft.com/en-us/download/details.aspx?id=46850) to be able to compile the P# visual studio extension (syntax highlighting).

## How to use
A good way to start is by reading the [manual](https://cdn.rawgit.com/p-org/PSharp/master/Docs/Manual/out/manual.pdf).

P# extends the C# language with state machines, states, state transitions and actions bindings. In P#, state machines are first class citizens and live in their own separate tasks. The only way they can communicate with each other is by explicitly sending and implicitly receiving events. As P# is based on C#, almost any valid C# code can be used in a P# method body (threading and code reflection APIs are not allowed).

The P# compiler can be used to parse a program, statically analyse it for data races and finally compile it to an executable. To invoke the compiler use the following command:

```
.\PSharpCompiler.exe /s:${SOLUTION_PATH}\${SOLUTION_NAME}.sln
```

Where ${SOLUTION\_PATH} is the path to your P# solution and ${SOLUTION\_NAME} is the name of your P# solution.

To specify an output path destination use the option `/o:${OUTPUT\_PATH}`.

To compile only a specific project in the solution use the option `/p:${PROJECT_NAME}`.

## Options

To see various available command line options use the option `/?`.

To statically analyze the program for data races use the option `/analyze`.

To systematically test the program for bugs (i.e. assertion failures and exceptions) use the option `/test`. You can optionally give the number of testing iterations to perform using `/i:value`.

## Publications
- **[Asynchronous Programming, Analysis and Testing with State Machines](https://dl.acm.org/citation.cfm?id=2737996)**. Pantazis Deligiannis, Alastair F. Donaldson, Jeroen Ketema, Akash Lal and Paul Thomson. In the *ACM SIGPLAN Conference on Programming Language Design and Implementation* (PLDI), 2015.
