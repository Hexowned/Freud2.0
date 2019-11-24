﻿#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using Freud.Common.Configuration;
using Freud.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Extensions.Discord
{
    internal static class CommandContextExtension
    {
        public static string BuildInvocationDetailsString(this CommandContext ctx, string reason = null)
            => $"{ctx.User} : {reason ?? "No reason provided."} | Invoked in: {ctx.Channel}";

        public static Task SendCollectionInPagesAsync<T>(this CommandContext ctx, string title, IEnumerable<T> collection, Func<T, string> selector, DiscordColor? color = null, int pageSize = 10)
        {
            var pages = new List<Page>();
            int size = collection.Count();
            int amountOfPages = (size - 1) / pageSize;
            int start = 0;
            for (int i = 0; i <= amountOfPages; i++)
            {
                int takeAmount = start + pageSize > size ? size - start : pageSize;
                var formattedCollectionPart = collection.Skip(start).Take(takeAmount).Select(selector);

                pages.Add(new Page(embed: new DiscordEmbedBuilder
                {
                    Title = $"{title} (page {i + 1}/{amountOfPages + 1})",
                    Description = string.Join("\n", formattedCollectionPart),
                    Color = color ?? DiscordColor.Black
                }));
                start += pageSize;
            }

            if (pages.Count > 1)
                return ctx.Client.GetInteractivity().SendPaginatedMessageAsync(ctx.Channel, ctx.User, pages);
            else
                return ctx.Channel.SendMessageAsync(content: pages.First().Content, embed: pages.First().Embed);
        }

        public static async Task<bool> WaitForBoolReplyAsync(this CommandContext ctx, string question, DiscordChannel channel = null, bool reply = true)
        {
            channel = channel ?? ctx.Channel;

            await ctx.RespondAsync(embed: new DiscordEmbedBuilder
            {
                Description = $"{StaticDiscordEmoji.Question} {question} (y/n)",
                Color = DiscordColor.Yellow
            });

            if (await ctx.Client.GetInteractivity().WaitForBoolReplyAsync(ctx))
                return true;
            if (reply)
                await channel.InformOfFailureAsync("Alrighty, aboring...");

            return false;
        }

        internal static async Task<DiscordUser> WaitForGameOpponentAsync(this CommandContext ctx)
        {
            SharedData shared = ctx.Services.GetService<SharedData>();
            shared.AddPendingResponse(ctx.Channel.Id, ctx.User.Id);

            var mctx = await ctx.Client.GetInteractivity().WaitForMessageAsync(xm =>
            {
                if (xm.Author.IsBot || xm.Author.Id == ctx.User.Id || xm.Channel.Id != ctx.Channel.Id)
                    return false;
                string[] split = xm.Content.ToLowerInvariant().Split(' ');

                return split.Length == 1 && (split[0] == "me" || split[0] == "i");
            });

            if (!shared.TryRemovePendingResponse(ctx.Channel.Id, ctx.User.Id))
                throw new ConcurrentOperationException("Failed to remove user from waiting list. This isn't good...");

            return mctx.TimedOut ? null : mctx.Result.Author;
        }

        internal static async Task<List<string>> WaitAndParsePollOptionsAsync(this CommandContext ctx)
        {
            SharedData shared = ctx.Services.GetService<SharedData>();
            shared.AddPendingResponse(ctx.Channel.Id, ctx.User.Id);

            var mctx = await ctx.Client.GetInteractivity().WaitForMessageAsync(xm => xm.Author.Id == ctx.User.Id && xm.Channel.Id == ctx.Channel.Id);

            if (!shared.TryRemovePendingResponse(ctx.Channel.Id, ctx.User.Id))
                throw new ConcurrentOperationException("Failed to remove user from waiting list. This isn't good...");

            if (mctx.TimedOut)
                return null;

            return mctx.Result.Content.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
        }
    }
}
