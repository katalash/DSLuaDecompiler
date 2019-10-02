# DSLuaDecompiler
This is a work in progress decompiler for Lua 5.0.2. Specifically, it is designed and intended to decompile Lua files found in Dark Souls, Dark Souls 3, Bloodborne, and Sekiro. These scripts are primarily used to implement AI logic in the games. Support for Havok Script, which is used to implement part of DS3, BB, and Sekiro's character animation systems, will also be looked into.

This decompiler is not yet complete and not usable by the end-user yet, but it is making rapid progress and able to perfectly structure the control flows of many AI files in DS3 so far.

Some of the design decisions that differentiate this decompiler from other Lua decompilers are:
1. Designed to run without any debug information in the Lua file, as debug information is generally stripped in the games
2. Uses Single-Static Analysis (SSA) for the majority of the data-flow analysis, type inference, and constant/expression propogation
3. Uses a control-flow graph (CFG) and structural analysis to recover high level control flow constructs (if/else, while, for, etc).
4. Will use Dark Souls specific research to automatically name and annotate decompiled files.
