﻿#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Freud.Common;
using Freud.Common.Attributes;
using Freud.Common.Configuration;
using Freud.Exceptions;
using Freud.Extensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.EventListeners
{
    internal static partial class Listeners
    {
        [AsyncEventListener(DiscordEventType.CommandExecuted)]
        public static Task CommandExecutionEventHandler(FreudShard shard, CommandExecutionEventArgs e)
        {
            shard.LogMany(LogLevel.Info,
                $"Executed: {e.Command?.QualifiedName ?? "<unknown command>"}",
                $"{e.Context.User.ToString()}",
                $"{e.Context.Guild.ToString()}; {e.Context.Channel.ToString()}");

            return Task.CompletedTask;
        }

        [AsyncEventListener(DiscordEventType.CommandErrored)]
        public static async Task CommandErrorEventHandlerAsync(FreudShard shard, CommandErrorEventArgs e)
        {
            if (e.Exception is null)
                return;

            var ex = e.Exception;
            while (ex is AggregateException)
                ex = ex.InnerException;

            if (ex is ChecksFailedException chke && chke.FailedChecks.Any(c => c is NotBlockedAttribute))
            {
                await e.Context.Message.CreateReactionAsync(StaticDiscordEmoji.X);

                return;
            }

            shard.LogMany(LogLevel.Info,
                $"Tried executing: {e.Command?.QualifiedName ?? "<unknown command>"}",
                $"{e.Context.User.ToString()}",
                $"{e.Context.Guild.ToString()}; {e.Context.Channel.ToString()}",
                $"Exception: {ex.GetType()}",
                $"Message: {ex.Message ?? "<no message provided>"}",
                ex.InnerException is null ? "" : $"Inner exception: {ex.InnerException.GetType()}",
                ex.InnerException is null ? "" : $"Inner exception message: {ex.InnerException.Message}");

            var emb = new DiscordEmbedBuilder { Color = DiscordColor.Red };
            var sb = new StringBuilder(StaticDiscordEmoji.NoEntry).Append(" ");

            switch (ex)
            {
                case CommandNotFoundException cne:
                    if (!shard.SharedData.GetGuildConfiguration(e.Context.Guild.Id).SuggestionEnabled)
                    {
                        await e.Context.Message.CreateReactionAsync(StaticDiscordEmoji.Question);
                        return;
                    }

                    sb.Clear();
                    sb.AppendLine(Formatter.Bold($"Command {Formatter.InlineCode(cne.CommandName)} not found. Did you mean..."));
                    var ordered = FreudShard.Commands
                        .OrderBy(tup => cne.CommandName.LevenshteinDistance(tup.Name)).Take(3);
                    foreach ((string alias, var cmd) in ordered)
                        emb.AddField($"{alias} ({cmd.QualifiedName})", cmd.Description);
                    break;

                case InvalidCommandUsageException _:
                    sb.Append("Invalid command usage! ");
                    sb.AppendLine(ex.Message);
                    emb.WithFooter($"Type \"{shard.SharedData.GetGuildPrefix(e.Context.Guild.Id)}help {e.Command.QualifiedName}\" for a command manual.");
                    break;

                case ArgumentException _:
                    string fcmdStr = $"help {e.Command.QualifiedName}";
                    var command = shard.CNext.FindCommand(fcmdStr, out string args);
                    var fctx = shard.CNext.CreateFakeContext(e.Context.User, e.Context.Channel, fcmdStr, e.Context.Prefix, command, args);
                    await shard.CNext.ExecuteCommandAsync(fctx);
                    return;

                case BadRequestException brex:
                    sb.Append($"Bad request! Details: {brex.JsonMessage}");
                    break;

                case NotFoundException nfe:
                    sb.Append($"404: Not found! Details: {nfe.JsonMessage}");
                    break;

                case CommandFailedException _:
                    sb.Append($"{ex.Message} {ex.InnerException?.Message}");
                    break;

                case NpgsqlException dbex:
                    sb.Append($"Database operation failed. Details: {dbex.Message}");
                    shard.SharedData.LogProvider.Log(LogLevel.Error, ex);
                    break;

                case ChecksFailedException cfex:
                    switch (cfex.FailedChecks.First())
                    {
                        case CooldownAttribute _:
                            return;

                        case UsageInteractivityAttribute _:
                            sb.Append($"I am waiting for your answer and you cannot execute commands until you either answer, or the timeout is reached.");
                            break;

                        default:
                            sb.AppendLine($"Command {Formatter.Bold(e.Command.QualifiedName)} cannot be executed because:").AppendLine();
                            foreach (var attr in cfex.FailedChecks)
                            {
                                switch (attr)
                                {
                                    case RequirePermissionsAttribute perms:
                                        sb.AppendLine($"- One of us does not have the required permissions ({perms.Permissions.ToPermissionString()})!");
                                        break;

                                    case RequireUserPermissionsAttribute uperms:
                                        sb.AppendLine($"- You do not have sufficient permissions ({uperms.Permissions.ToPermissionString()})!");
                                        break;

                                    case RequireOwnerOrPermissionsAttribute operms:
                                        sb.AppendLine($"- You do not have sufficient permissions ({operms.Permissions.ToPermissionString()})!");
                                        break;

                                    case RequireBotPermissionsAttribute bperms:
                                        sb.AppendLine($"- I do not have sufficient permissions ({bperms.Permissions.ToPermissionString()})!");
                                        break;

                                    case RequirePrivilegedUserAttribute _:
                                        sb.AppendLine($"- That command is reserved for my owner and privileged users!");
                                        break;

                                    case RequireOwnerAttribute _:
                                        sb.AppendLine($"- That command is reserved only for my owner!");
                                        break;

                                    case RequireNsfwAttribute _:
                                        sb.AppendLine($"- That command is allowed only in the NSFW channels!");
                                        break;

                                    case RequirePrefixesAttribute pattr:
                                        sb.AppendLine($"- That command can only be invoked only with the following prefixes: {string.Join(" ", pattr.Prefixes)}!");
                                        break;

                                    default:
                                        sb.AppendLine($"{attr} was not met! (this should not happen, please report this)");
                                        break;
                                }
                            }
                            break;
                    }
                    break;

                case ConcurrentOperationException _:
                    sb.Append($"A concurrency error occured - please report this. Details: {ex.Message}");
                    shard.SharedData.LogProvider.Log(LogLevel.Error, ex);
                    break;

                case UnauthorizedException _:
                    sb.Append("I am unauthorized to do that");
                    break;

                case DbUpdateException _:
                    sb.Append("A database update error has occured, possibly due to large amount of update request. Please try again later.");
                    shard.SharedData.LogProvider.Log(LogLevel.Error, ex);
                    break;

                case TargetInvocationException _:
                    sb.Append($"{ex.InnerException?.Message ?? "Target invocation error occured. Please check the arguments provided and try again."}");
                    break;

                case TaskCanceledException _:
                    return;

                default:
                    sb.AppendLine($"Command {Formatter.Bold(e.Command.QualifiedName)} errored!").AppendLine();
                    sb.AppendLine($"Exception: {Formatter.InlineCode(ex.GetType().ToString())}");
                    sb.AppendLine($"Details: {Formatter.Italic(ex.Message)}");

                    if (!(ex.InnerException is null))
                    {
                        sb.AppendLine($"Inner exception: {Formatter.InlineCode(ex.InnerException.GetType().ToString())}");
                        sb.AppendLine($"Details: {Formatter.Italic(ex.InnerException.Message ?? "No details provided")}");
                    }

                    shard.SharedData.LogProvider.Log(LogLevel.Error, ex);
                    break;
            }

            emb.Description = sb.ToString();

            await e.Context.RespondAsync(embed: emb.Build());
        }
    }
}
