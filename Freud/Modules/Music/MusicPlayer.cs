#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Music
{
    public class MusicPlayer
    {
        private bool playing = false;
        private bool stopped = false;
        private readonly ConcurrentQueue<SongInfo> songs;
        private readonly VoiceNextConnection vnc;
        private readonly DiscordChannel channel;
        private readonly DiscordClient client;
        private DiscordMessage msgHandle;
        private readonly object operationLock;

        public MusicPlayer(DiscordClient client, DiscordChannel chn, VoiceNextConnection vnc)
        {
            this.operationLock = new object();
            this.songs = new ConcurrentQueue<SongInfo>();
            this.client = client;
            this.channel = chn;
            this.vnc = vnc;
        }

        public bool IsPlaying
        {
            get
            {
                lock (this.operationLock)
                {
                    return this.playing;
                };
            }
        }

        public bool IsStopped
        {
            get
            {
                lock (this.operationLock)
                {
                    return this.stopped;
                };
            }
        }

        public void Enqueue(SongInfo si)
        {
            this.songs.Enqueue(si);
            lock (this.operationLock)
            {
                if (this.stopped)
                {
                    this.stopped = false;
                    var t = Task.Run(()
                        => this.StartAsync());
                }
            }
        }

        public void Skip()
        {
            lock (this.operationLock)
                this.playing = false;
        }

        public void Stop()
        {
            lock (this.operationLock)
            {
                this.playing = false;
                this.stopped = true;
            }
        }

        public async Task StartAsync()
        {
            this.client.MessageReactionAdded += this.ReactionHandler;
            try
            {
                while (!this.songs.IsEmpty && !this.stopped)
                {
                    if (!this.songs.TryDequeue(out var si))
                        continue;

                    lock (this.operationLock)
                        this.playing = true;

                    this.msgHandle = await this.channel.SendMessageAsync("Playing: ", embed: si.ToDiscordEmbed(DiscordColor.Red));
                    await this.msgHandle.CreateReactionAsync(DiscordEmoji.FromUnicode("▶"));

                    var ffmpeg_inf = new ProcessStartInfo
                    {
                    };

                    var ffmpeg = Process.Start(ffmpeg_inf);
                    var ffout = ffmpeg.StandardOutput.BaseStream;

                    using (var ms = new MemoryStream())
                    {
                        await ffout.CopyToAsync(ms);
                        ms.Position = 0;

                        byte[] buff = new byte[3840];
                        int br = 0;
                        while (this.playing && (br = ms.Read(buff, 0, buff.Length)) > 0)
                        {
                            if (br < buff.Length)
                                for (int i = br; i < buff.Length; i++)
                                    buff[i] = 0;

                            // await this.vnc.SendAsync(buff, 20);
                        }
                    }

                    await this.msgHandle.DeleteAllReactionsAsync();
                }
            } catch (Exception e)
            {
                // handle exc
                Console.Write(e); // log whatever exception and handle it here
            } finally
            {
                lock (this.operationLock)
                {
                    this.playing = false;
                    this.stopped = true;
                }

                // remove reaction handler
            }
        }

        private async Task ReactionHandler(MessageReactionAddEventArgs e)
        {
            if (e.User.IsBot || e.Message.Id != this.msgHandle.Id)
                return;

            switch (e.Emoji.Name)
            {
                case "▶":
                default:
                    break;
            }

            await e.Message.DeleteReactionAsync(e.Emoji, e.User);
        }
    }
}
