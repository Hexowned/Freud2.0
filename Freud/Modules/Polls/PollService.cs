﻿#region USING_DIRECTIVES

using System.Collections.Concurrent;

#endregion USING_DIRECTIVES

namespace Freud.Modules.Polls
{
    public static class PollService
    {
        private static readonly ConcurrentDictionary<ulong, Poll> _polls = new ConcurrentDictionary<ulong, Poll>();

        public static Poll GetPollInChannel(ulong cid)
            => _polls.TryGetValue(cid, out Poll poll) ? poll : null;

        public static bool IsPollRunningInChannel(ulong cid)
            => !(GetPollInChannel(cid) is null);

        public static void RegisterPollInChannel(Poll poll, ulong cid)
            => _polls[cid] = poll;

        public static void UnregisterPollInChannel(ulong cid)
        {
            if (!_polls.ContainsKey(cid))
                return;

            if (!_polls.TryRemove(cid, out _))
                _polls[cid] = null;
        }
    }
}
