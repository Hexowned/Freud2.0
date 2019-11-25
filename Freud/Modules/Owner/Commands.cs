﻿#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Freud.Common.Attributes;
using Freud.Common.Configuration;
using Freud.Database.Db;
using Freud.Exceptions;
using Freud.Extensions.Discord;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Owner
{
    public partial class OwnerModule
    {
        [Group("commands"), NotBlocked]
        [Description("Manipulate bot commands in runtime.")]
        [Aliases("cmds", "cmd")]
        [RequireOwner]
        public class CommandsModule : FreudModule
        {
            public CommandsModule(SharedData shared, DatabaseContextBuilder dcb)
                : base(shared, dcb)
            {
                this.ModuleColor = DiscordColor.NotQuiteBlack;
            }

            [GroupCommand, Priority(0)]
            public Task ExecuteGroupAsync(CommandContext ctx)
                => this.ListAsync(ctx);

            #region COMMAND_COMMANDS_ADD

            [Command("add")]
            [Description("Add a new command.")]
            [Aliases("+", "a", "<", "<<", "+=")]
            [UsageExampleArgs("\\`\\`\\`[Command(\"test\")] public Task TestAsync(CommandContext ctx) => ctx.RespondAsync(\"Hello world!\");\\`\\`\\`")]
            public Task AddAsync(CommandContext ctx,
                             [RemainingText, Description("Code to evaluate.")] string code)
            {
                if (string.IsNullOrWhiteSpace(code))
                    throw new InvalidCommandUsageException("Code missing.");

                int cs1 = code.IndexOf("```") + 3;
                int cs2 = code.LastIndexOf("```");
                if (cs1 == -1 || cs2 == -1)
                    throw new InvalidCommandUsageException("You need to wrap the code into a code block.");

                code = $@"{code.Substring(cs1, cs2 - cs1)}";

                string type = $"DynamicCommands{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
                Type moduleType = null;
                try
                {
                    var refs = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(xa => !xa.IsDynamic && !string.IsNullOrWhiteSpace(xa.Location))
                        .Select(x => MetadataReference.CreateFromFile(x.Location));

                    var ast = SyntaxFactory.ParseSyntaxTree(code, new CSharpParseOptions().WithKind(SourceCodeKind.Script).WithLanguageVersion(LanguageVersion.Latest));
                    var opts = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                                                            scriptClassName: type, usings: new[] { "System", "System.Collections.Generic", "System.Linq", "System.Text",
                                                                                                   "System.Threading.Tasks", "DSharpPlus", "DSharpPlus.Entities", "DSharpPlus.CommandsNext",
                                                                                                   "DSharpPlus.CommandsNext.Attributes", "DSharpPlus.Interactivity"},
                                                            optimizationLevel: OptimizationLevel.Release, allowUnsafe: true, platform: Platform.X64);

                    var compilation = CSharpCompilation.CreateScriptCompilation(type, ast, refs, opts, returnType: typeof(object));

                    Assembly assembly = null;
                    using (var ms = new MemoryStream())
                    {
                        var result = compilation.Emit(ms);
                        ms.Position = 0;
                        assembly = Assembly.Load(ms.ToArray());
                    }

                    var outType = assembly.ExportedTypes.FirstOrDefault(x => x.Name == type);
                    moduleType = outType.GetNestedTypes().FirstOrDefault(x => x.BaseType == typeof(BaseCommandModule));

                    ctx.CommandsNext.RegisterCommands(moduleType);
                    FreudShard.UpdateCommandList(ctx.CommandsNext);

                    return this.InformAsync(ctx, StaticDiscordEmoji.Information, "Compilation successful! Command(s) successfully added!", important: false);
                } catch (Exception ex)
                {
                    return this.InformOfFailureAsync(ctx, $"Compilation failed!\n\n{Formatter.Bold(ex.GetType().ToString())}: {ex.Message}");
                }
            }

            #endregion COMMAND_COMMANDS_ADD

            #region COMMAND_COMMANDS_DELTE

            [Command("delete")]
            [Description("Remove an existing command.")]
            [Aliases("-", "remove", "rm", "del", ">", ">>", "-=")]
            [UsageExampleArgs("say")]
            public Task DeleteAsync(CommandContext ctx, [RemainingText, Description("Command to remove")] string command)
            {
                Command cmd = ctx.CommandsNext.FindCommand(command, out _);
                if (cmd is null)
                    throw new CommandFailedException("Cannot find that command.");
                ctx.CommandsNext.UnregisterCommands(cmd);
                FreudShard.UpdateCommandList(ctx.CommandsNext);

                return this.InformAsync(ctx, $"Removed command {Formatter.Bold(cmd.QualifiedName)}.", important: false);
            }

            #endregion COMMAND_COMMANDS_DELTE

            #region COMMAND_COMMANDS_LIST

            [Command("list")]
            [Description("List all privileged users.")]
            [Aliases("ls", "l", "print")]
            public Task ListAsync(CommandContext ctx)
            {
                return ctx.SendCollectionInPagesAsync("", ctx.CommandsNext.GetAllRegisteredCommands()
                    .OrderBy(cmd => cmd.QualifiedName), cmd => cmd.QualifiedName, this.ModuleColor, 10);
            }

            #endregion COMMAND_COMMANDS_LIST
        }
    }
}
