#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using Freud.Common.Converters;
using Freud.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Extensions.Discord
{
    internal static class InteractivityExtensions
    {
        public static Task<bool> WaitForBoolReplyAsync(this InteractivityExtension interactivity, CommandContext ctx, ulong uid = 0)
            => interactivity.WaitForBoolReplyAsync(ctx.Channel.Id, uid != 0 ? uid : ctx.User.Id, ctx.Services.GetService<SharedData>());

        public static async Task<bool> WaitForBoolReplyAsync(this InteractivityExtension interactivity, ulong cid, ulong uid, SharedData shared = null)
        {
            if (!(shared is null))
                shared.AddPendingResponse(cid, uid);

            bool response = false;
            InteractivityResult<DiscordMessage> mctx = await interactivity.WaitForMessageAsync(m =>
            {
                if (m.ChannelId != cid || m.Author.Id != uid)
                    return false;

                bool? b = CustomBoolConverter.TryConvert(m.Content);
                response = b ?? false;

                return b.HasValue;
            });

            if (!(shared is null) && !shared.TryRemovePendingResponse(cid, uid))
                throw new ConcurrentOperationException("Failed to remove user from waiting list. Something went wrong!");

            return response;
        }

        public static async Task<InteractivityResult<DiscordMessage>> WaitForDmReplyAsync(this InteractivityExtension interactivity, DiscordDmChannel dm, ulong cid, ulong uid, SharedData shared = null)
        {
            if (!(shared is null))
                shared.AddPendingResponse(cid, uid);

            var mctx = await interactivity.WaitForMessageAsync(xm => xm.Channel == dm && xm.Author.Id == uid, TimeSpan.FromMinutes(1));

            if (!(shared is null) && !shared.TryRemovePendingResponse(cid, uid))
                throw new ConcurrentOperationException("Failed to remove user from waiting list. Something went wrong!");

            return mctx;
        }
    }
}
