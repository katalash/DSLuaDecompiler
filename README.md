# DSLuaDecompiler
This is a work in progress decompiler for Lua 5.0.2 and HavokScript. Specifically, it is designed and intended to decompile Lua files found in Dark Souls, Dark Souls 3, Bloodborne, Sekiro, and Elden Ring. These scripts are primarily used to implement AI logic in the games. DS3, Bloodborne, Sekiro, and Elden Ring also use HavokScript, a heavily modified version of Lua 5.1, to interface the game with the Havok behavior system and much of the character animation logic is in HavokScript. This decompiler will decompile a subset of Havokscript that is used in these games.

This decompiler is not yet complete and not usable by the end-user yet, but it is making rapid progress and is able to perfectly structure the control flows of many AI files in DS3 so far. It's able to decompile DS3's c0000.hks file, which is a massive HavokScript file that implements the majority of the player logic in DS3. It's also able to decompile all HavokScript files present in Elden Ring.

Some of the design decisions that differentiate this decompiler from other Lua decompilers are:
1. Designed to run without any debug information in the Lua file, as debug information is generally stripped in the games, but will use the information to assist in decompilation if available.
2. Uses Single-Static Analysis (SSA) for the majority of the data-flow analysis, type inference, and constant/expression propogation
3. Uses a control-flow graph (CFG) and structural analysis to recover high level control flow constructs (if/else, while, for, etc).
4. Will use Dark Souls specific research to automatically name and annotate decompiled files.
