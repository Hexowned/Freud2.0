#region USING_DIRECTIVES

using System;

#endregion USING_DIRECTIVES

namespace Freud.Exceptions
{
    public class ServiceDisabledException : Exception
    {
        public ServiceDisabledException()
            : base("This service has been disabled by the bot owner.")
        {
        }
    }
}
