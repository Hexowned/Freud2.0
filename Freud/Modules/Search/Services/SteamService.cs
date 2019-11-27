#region USING_DIRECTIVES

using DSharpPlus;
using DSharpPlus.Entities;
using Freud.Services;
using Steam.Models.SteamCommunity;
using SteamWebAPI2.Interfaces;
using SteamWebAPI2.Utilities;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UserStatus = Steam.Models.SteamCommunity.UserStatus;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Search.Services
{
    public class SteamService : IFreudService
    {
        private readonly SteamUser user;

        public static string GetProfileUrlForId(ulong id)
            => $"http://steamcommunity.com/id/{ id }/";

        public SteamService(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
                this.user = new SteamUser(key);
        }

        public bool IsDisabled()
            => this.user is null;

        public DiscordEmbed EmbedSteamResults(SteamCommunityProfileModel profile, PlayerSummaryModel summary)
        {
            if (this.IsDisabled())
                return null;

            var emb = new DiscordEmbedBuilder
            {
                Title = summary.Nickname,
                Description = Regex.Replace(profile.Summary, "<[^>]*>", string.Empty),
                ThumbnailUrl = profile.AvatarFull.ToString(),
                Color = DiscordColor.Black,
                Url = GetProfileUrlForId(profile.SteamID)
            };

            if (summary.ProfileVisibility != ProfileVisibility.Public)
            {
                emb.Description = "This profile is private";

                return emb;
            }

            emb.AddField("Member since", summary.AccountCreatedDate.ToUniversalTime().ToString(), inline: true);

            if (summary.UserStatus != UserStatus.Offline)
                emb.AddField("Status:", summary.UserStatus.ToString(), inline: true);
            else
                emb.AddField("Last seen:", summary.LastLoggedOffDate.ToUniversalTime().ToString(), inline: true);

            if (!string.IsNullOrWhiteSpace(summary.PlayingGameName))
                emb.AddField("Playing: ", summary.PlayingGameName);

            if (!string.IsNullOrWhiteSpace(profile.Location))
                emb.AddField("Location: ", profile.Location);
            emb.AddField("Game activity", $"{profile.HoursPlayedLastTwoWeeks} hours in the past 2 weeks.", inline: true);

            if (profile.IsVacBanned)
            {
                var bans = this.user.GetPlayerBansAsync(profile.SteamID).Result.Data;

                uint banCount = 0;
                foreach (var b in bans)
                    banCount += b.NumberOfVACBans;
                emb.AddField("VAC Status:", $"{Formatter.Bold(banCount.ToString())} ban(s) on record.", inline: true);
            } else
            {
                emb.AddField("VAC Status:", "No bans registered!");
            }

            return emb.Build();
        }

        public async Task<DiscordEmbed> GetEmbeddedInfoAsync(ulong id)
        {
            if (this.IsDisabled())
                return null;

            SteamCommunityProfileModel profile = null;
            ISteamWebResponse<PlayerSummaryModel> summary = null;
            try
            {
                profile = await this.user.GetCommunityProfileAsync(id);
                summary = await this.user.GetPlayerSummaryAsync(id);
            } catch
            {
            }

            if (profile is null || summary is null || summary.Data is null)
                return null;

            return this.EmbedSteamResults(profile, summary.Data);
        }
    }
}
