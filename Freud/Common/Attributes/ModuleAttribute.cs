#region USING_DIRECTIVES

using DSharpPlus.CommandsNext;
using System;
using System.Linq;

#endregion USING_DIRECTIVES

namespace Freud.Common.Attributes
{
    internal enum ModuleType
    {
        Administration,
        Currency,
        Games,
        Reminders,
        Miscellaneous,
        Owner,
        Polls,
        Uncategorized,
        Reactions,
        Music
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class ModuleAttribute : Attribute
    {
        public static ModuleAttribute ForCommand(Command cmd)
        {
            var mattr = cmd.CustomAttributes.FirstOrDefault(Attribute => Attribute is ModuleAttribute) as ModuleAttribute;

            return mattr ?? (cmd.Parent is null ? new ModuleAttribute(ModuleType.Uncategorized) : ForCommand(cmd.Parent));
        }

        public ModuleType Module { get; private set; }

        public ModuleAttribute(ModuleType module)
        {
            this.Module = module;
        }
    }
}
