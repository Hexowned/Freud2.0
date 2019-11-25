﻿#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

#endregion USING_DIRECTIVES

namespace Freud.Common.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class UsageExampleArgsAttribute : Attribute
    {
        public string[] Examples { get; private set; }

        public UsageExampleArgsAttribute(params string[] examples)
        {
            if (examples is null)
                throw new ArgumentException($"No examples provided to {this.GetType().Name}!");

            this.Examples = examples
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToArray();

            if (!this.Examples.Any())
                throw new ArgumentException($"Please provide non-empty examples to {this.GetType().Name}!");
        }

        public string JoinExamples(Command cmd, CommandContext ctx = null, string seperator = "\n")
        {
            if (ctx is null)
                return string.Join(seperator, this.Examples);

            string cname = cmd.QualifiedName;
            string prefix = ctx.Services.GetService<SharedData>().GetGuildPrefix(ctx.Guild.Id);

            if (cmd.Overloads.Any(o => o.Arguments.All(a => a.IsOptional)))
                return string.Join(seperator, new[] { "" }.Concat(this.Examples).Select(e => $"{prefix}{cname} {e}"));
            else
                return string.Join(seperator, this.Examples.Select(e => $"{prefix}{cname} {e}"));
        }
    }
}
