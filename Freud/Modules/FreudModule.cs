#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Freud.Common.Configuration;
using System;
using System.Net.Http;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules
{
    public abstract class FreudModule : BaseCommandModule
    {
        protected static readonly HttpClient _http;
        private static readonly HttpClientHandler _handler;

        public SharedData Shared { get; }
        public DatabaseContextBuilder Database { get; }

        public DiscordColor ModuleColor
        {
            get { return this.ModuleColor ?? DiscordColor.Green; }
            set { this.ModuleColor = value; }
        }

        private DiscordColor? moduleColor;

        static FreudModule()
        {
            _handler = new HttpClientHandler { AllowAutoRedirect = false };
            _http = new HttpClient(_handler, true);
        }

        protected FreudModule(SharedData shared, DatabaseContextBuilder dcb = null)
        {
            this.Shared = shared;
            this.Database = dcb;
            this.ModuleColor = DiscordColor.Green;
        }

        protected Task InformAsync(CommandContext ctx, string message = null, string emoji = null, bool important = true)
            => this.InformAsync(ctx, (emoji is null ? StaticDiscordEmoji.CheckmarkSuccess : DiscordEmoji.FromName(ctx.Client, emoji)), message, important);

        protected async Task InformAsync(CommandContext ctx, DiscordEmoji emoji, string message = null, bool important = true)
        {
            if (!important && this.Shared.GetGuildConfiguration(ctx.Guild.Id).ReactionResponse)
            {
                try
                {
                    await ctx.Message.CreateReactionAsync(StaticDiscordEmoji.CheckMarkSuccess);
                } catch (NotFoundException)
                {
                    await this.InformAsync(ctx, "Action completed!");
                }
            } else
            {
                await ctx.RespondAsync(embed: new DiscordEmbedBuilder
                {
                    Description = $"{(emoji ?? StaticDiscordEmoji.CheckMarkSuccess)} {message ?? "Done!"}",
                    Color = this.ModuleColor
                });
            }
        }

        protected Task InformFailureAsync(CommandContext ctx, string message)
        {
            return ctx.RespondAsync(embed: new DiscordEmbedBuilder
            {
                Description = $"{StaticDiscordEmoji.X} {message}",
                Color = DiscordColor.IndianRed
            });
        }

        protected async Task<bool> IsValidImageUriAsync(Uri uri)
        {
            try
            {
                HttpResponseMessage response = await _http.GetAsync(uri).ConfigureAwait(false);
                if (response.Content.Headers.ContentType.MediaType.StartsWith("image/"))
                    return true;
            } catch
            {
                // swallow
            }

            return false;
        }
    }
}
