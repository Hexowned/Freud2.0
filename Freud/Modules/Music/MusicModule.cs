#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using Freud.Common.Attributes;
using Freud.Common.Configuration;
using Freud.Database.Db;
using Freud.Exceptions;
using Freud.Modules.Search.Services;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Music
{
    [Module(ModuleType.Music), NotBlocked]
    [Cooldown(3, 5, CooldownBucketType.Channel)]
    [RequireBotPermissions(Permissions.UseVoice)]
    // TODO unlock when finished
    [RequireOwner]

    #region MusicModule : FreudServiceModule<YtService>

    public partial class MusicModule : FreudServiceModule<YtService>
    {
        // TODO move to shared or even better create a transient module ?
        public static ConcurrentDictionary<ulong, MusicPlayer> MusicPlayers { get; } = new ConcurrentDictionary<ulong, MusicPlayer>();

        public MusicModule(YtService yt, SharedData shared, DatabaseContextBuilder dcb)
            : base(yt, shared, dcb)
        {
            this.ModuleColor = DiscordColor.Grayple;
        }

        #region COMMAND_CONNECT

        [Command("connect")]
        [Description("Connect the bot to a voice channel. If the channel is not given, connects the bot to the same channel you are in.")]
        [Aliases("con", "conn", "enter")]
        [UsageExampleArgs("Music")]
        public async Task ConnectAsync(CommandContext ctx,
                                      [Description("Channel.")] DiscordChannel channel = null)
        {
            var vnext = ctx.Client.GetVoiceNext();
            if (vnext is null)
                throw new CommandFailedException("VNext is not enabled or configured.");

            var vnc = vnext.GetConnection(ctx.Guild);
            if (!(vnc is null))
                throw new CommandFailedException("Already connected in this guild.");

            var vstat = ctx.Member?.VoiceState;
            if ((vstat is null || vstat.Channel is null) && channel is null)
                throw new CommandFailedException("You are not in a voice channel.");

            if (channel is null)
                channel = vstat.Channel;

            vnc = await vnext.ConnectAsync(channel);

            await this.InformAsync(ctx, StaticDiscordEmoji.Headphones, $"Connected to {Formatter.Bold(channel.Name)}.", important: false);
        }

        #endregion COMMAND_CONNECT

        #region COMMAND_DISCONNECT

        [Command("disconnect")]
        [Description("Disconnects the bot from the voice channel.")]
        [Aliases("dcon", "dconn", "discon", "disconn", "dc")]
        public Task DisconnectAsync(CommandContext ctx)
        {
            var vnext = ctx.Client.GetVoiceNext();
            if (vnext is null)
                throw new CommandFailedException("VNext is not enabled or configured.");

            var vnc = vnext.GetConnection(ctx.Guild);
            if (vnc is null)
                throw new CommandFailedException("Not connected in this guild.");

            if (MusicPlayers.TryGetValue(ctx.Guild.Id, out MusicPlayer player))
            {
                player.Stop();
                MusicPlayers.TryRemove(ctx.Guild.Id, out _);
            }

            // TODO check await Task.Delay(500);
            vnc.Disconnect();

            return this.InformAsync(ctx, StaticDiscordEmoji.Headphones, "Disconnected.", important: false);
        }

        #endregion COMMAND_DISCONNECT

        #region COMMAND_SKIP

        [Command("skip")]
        [Description("Skip current voice playback.")]
        public Task SkipAsync(CommandContext ctx)
        {
            if (!MusicPlayers.TryGetValue(ctx.Guild.Id, out MusicPlayer player))
                throw new CommandFailedException("Not playing in this guild");

            player.Skip();
            return Task.CompletedTask;
        }

        #endregion COMMAND_SKIP

        #region COMMAND_STOP

        [Command("stop")]
        [Description("Stops current voice playback.")]
        public Task StopAsync(CommandContext ctx)
        {
            if (!MusicPlayers.TryGetValue(ctx.Guild.Id, out MusicPlayer player))
                throw new CommandFailedException("Not playing in this guild");

            player.Stop();
            return this.InformAsync(ctx, StaticDiscordEmoji.Headphones, "Stopped.", important: false);
        }

        #endregion COMMAND_STOP
    }

    #endregion MusicModule : FreudServiceModule<YtService>

    #region MusicModule.Play

    public partial class MusicModule
    {
        [Group("play")]
        [Description("Commands for playing music. Group call plays given URL or searches YouTube for given query and plays the first result.")]
        [Aliases("music", "p")]
        [UsageExampleArgs("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "what is love?")]
        [RequireBotPermissions(Permissions.UseVoice | Permissions.Speak)]
        [RequireOwner]
        public class PlayModule : MusicModule
        {
            public PlayModule(YtService yt, SharedData shared, DatabaseContextBuilder dcb)
                : base(yt, shared, dcb)
            {
                this.ModuleColor = DiscordColor.Grayple;
            }

            [GroupCommand, Priority(1)]
            public async Task ExecuteGroupAsync(CommandContext ctx,
                                               [Description("URL to play.")] Uri url)
            {
                SongInfo si = await this.Service.GetSongInfoAsync(url.AbsoluteUri);
                if (si is null)
                    throw new CommandFailedException("Failed to retrieve song information for that URL.");
                si.Queuer = ctx.User.Mention;

                await this.ConnectAndAddToQueueAsync(ctx, si);
            }

            [GroupCommand, Priority(0)]
            public async Task ExecuteGroupAsync(CommandContext ctx,
                                               [RemainingText, Description("YouTube search query.")] string query)
            {
                string result = await this.Service.GetFirstVideoResultAsync(query);
                if (string.IsNullOrWhiteSpace(result))
                    throw new CommandFailedException("No results found!");

                SongInfo si = await this.Service.GetSongInfoAsync(result);
                if (si is null)
                    throw new CommandFailedException("Failed to retrieve song information for that query.");

                si.Queuer = ctx.User.Mention;

                await this.ConnectAndAddToQueueAsync(ctx, si);
            }

            #region COMMAND_PLAY_FILE

            [Command("file")]
            [Description("Plays an audio file from the server filesystem.")]
            [Aliases("f")]
            [UsageExampleArgs("test.mp3")]
            public async Task PlayFileAsync(CommandContext ctx,
                                           [RemainingText, Description("Full path to the file to play.")] string filename)
            {
                var vnext = ctx.Client.GetVoiceNext();
                if (vnext is null)
                    throw new CommandFailedException("VNext is not enabled or configured.");

                var vnc = vnext.GetConnection(ctx.Guild);
                if (vnc is null)
                {
                    await this.ConnectAsync(ctx);
                    vnc = vnext.GetConnection(ctx.Guild);
                }

                if (!File.Exists(filename))
                    throw new CommandFailedException($"File {Formatter.InlineCode(filename)} does not exist.");

                var si = new SongInfo
                {
                    Title = filename,
                    Provider = "Server file system",
                    Query = ctx.Client.CurrentUser.AvatarUrl,
                    Queuer = ctx.User.Mention,
                    Uri = filename
                };

                if (MusicPlayers.TryGetValue(ctx.Guild.Id, out var player))
                {
                    player.Enqueue(si);
                    await ctx.RespondAsync("Added to queue:", embed: si.ToDiscordEmbed(this.ModuleColor));
                } else
                {
                    var newPlayer = new MusicPlayer(ctx.Client, ctx.Channel, vnc);
                    if (!MusicPlayers.TryAdd(ctx.Guild.Id, newPlayer))
                        throw new ConcurrentOperationException("Failed to initialize music player!");
                    newPlayer.Enqueue(si);
                    await newPlayer.StartAsync();
                }
            }

            #endregion COMMAND_PLAY_FILE

            #region HELPER_FUNCTIONS

            private async Task ConnectAndAddToQueueAsync(CommandContext ctx, SongInfo si)
            {
                var vnext = ctx.Client.GetVoiceNext();
                if (vnext is null)
                    throw new CommandFailedException("VNext is not enabled or configured.");

                var vnc = vnext.GetConnection(ctx.Guild);
                if (vnc is null)
                {
                    await this.ConnectAsync(ctx);
                    vnc = vnext.GetConnection(ctx.Guild);
                }

                if (MusicPlayers.TryGetValue(ctx.Guild.Id, out var player))
                {
                    player.Enqueue(si);
                    await ctx.RespondAsync("Added to queue:", embed: si.ToDiscordEmbed(this.ModuleColor));
                } else
                {
                    var newPlayer = new MusicPlayer(ctx.Client, ctx.Channel, vnc);
                    if (!MusicPlayers.TryAdd(ctx.Guild.Id, newPlayer))
                        throw new ConcurrentOperationException("Failed to initialize music player!");
                    newPlayer.Enqueue(si);

                    // TODO
                    var t = Task.Run(() => newPlayer.StartAsync());
                }
            }

            #endregion HELPER_FUNCTIONS
        }
    }

    #endregion MusicModule.Play
}
