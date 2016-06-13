P#
====================
A toolkit for **building**, **analyzing**, **systematically testing** and **debugging** asynchronous reactive software, such as web-services and distributed systems.

## Features
The P# framework provides:
- Language extensions to C# for building **event-driven asynchronous** applications, writing **test harnesses**, and specifying **safety and liveness properties**.
- A **systematic testing engine** that can capture and control all specified nondeterminism in the system, systematically explore the actual executable code to discover bugs, and report bug traces. A P# bug trace provides a global order of all communication events, and thus is easier to debug.
- Support for **replaying** bug traces, and **debugging** them using the Visual Studio debugger.

Although P# primarily targets .NET, it has also experimental support for systematically testing native C++ code.

## Documentation
The best way to build and start using P# is to read our [wiki](https://github.com/p-org/PSharp/wiki):

- [Building P#](https://github.com/p-org/PSharp/wiki/Build-Instructions)
- [P# Compiler](https://github.com/p-org/PSharp/wiki/Compilation)
- [P# Tester](https://github.com/p-org/PSharp/wiki/Testing)
- [P# Replayer/Debugger](https://github.com/p-org/PSharp/wiki/Bug-Reproduction)
- [Samples and Walkthroughs](https://github.com/p-org/PSharp/wiki/Samples-and-Walkthroughs)

You can also read the manual and available publications:

- [Manual](https://github.com/p-org/PSharp/blob/master/Docs/Manual/manual.pdf)  
- [Publications](https://github.com/p-org/PSharp/wiki/Publications)

## How to contribute

We welcome contributions to the P# project! However, before you start contributing, please read carefully the [development guidelines](https://github.com/p-org/PSharp/wiki/Contributing-Code).

## Contact us

If you would like to use P# in your project, or have any specific questions, please feel free to contact one of the following members of the P# team:
- Pantazis Deligiannis (p.deligiannis@imperial.ac.uk) [Maintainer]
- Akash Lal (akashl@microsoft.com) [Maintainer]
- Shaz Qadeer (qadeer@microsoft.com)
- Cheng Huang (cheng.huang@microsoft.com)
