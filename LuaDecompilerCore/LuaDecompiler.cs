using System;
using System.Collections.Generic;
using System.Linq;
using LuaDecompilerCore.IR;
using LuaDecompilerCore.Utilities;

namespace LuaDecompilerCore
{
    public class LuaDecompiler
    {
        private DecompilationOptions DecompilationOptions { get; set; }

        public LuaDecompiler(DecompilationOptions options)
        {
            DecompilationOptions = options;
        }

        public DecompilationResult DecompileLuaFunction(
            ILanguageDecompiler languageDecompiler,
            Function main,
            LuaFile.Function luaMain)
        {
            var globalSymbolTable = new GlobalSymbolTable();
            var functions = new List<Function>();
            
            void Visit(LuaFile.Function luaFunction, Function irFunction)
            {
                // Language specific initialization
                languageDecompiler.InitializeFunction(luaFunction, irFunction, globalSymbolTable);
                
                // Enable debug comments if set
                irFunction.InsertDebugComments = DecompilationOptions.OutputDebugComments;

                // Initialize function parameters
                irFunction.ParameterCount = (int)luaFunction.NumParams;
                irFunction.RegisterCount = (int)luaFunction.NumParams;

                // Give constant table
                irFunction.Constants = luaFunction.Constants;

                // Now generate IR from the bytecode using language specific decompiler
                languageDecompiler.GenerateIr(luaFunction, irFunction, globalSymbolTable);
                
                // Add function to run decompilation passes on
                functions.Add(irFunction);
                
                // Now visit all the child closures unless they are excluded
                for (var i = 0; i < luaFunction.ChildFunctions.Length; i++)
                {
                    if (DecompilationOptions.IncludedFunctionIds != null && 
                        !DecompilationOptions.IncludedFunctionIds.Contains(luaFunction.ChildFunctions[i].FunctionId))
                        continue;
                    if (DecompilationOptions.ExcludedFunctionIds.Contains(luaFunction.ChildFunctions[i].FunctionId))
                        continue;
                    Visit(luaFunction.ChildFunctions[i], irFunction.LookupClosure((uint)i));
                }
            }
            
            // Visit all the functions and generate the initial IR from the bytecode
            Visit(luaMain, main);

            // Create pass manager, add language specific passes, and run
            var passManager = new PassManager(DecompilationOptions);
            languageDecompiler.AddDecompilePasses(passManager);
            return passManager.RunOnFunctions(
                new DecompilationContext(globalSymbolTable), 
                functions,
                DecompilationOptions.CatchPassExceptions);
        }
    }
}
