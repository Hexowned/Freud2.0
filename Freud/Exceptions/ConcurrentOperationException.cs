#region USING_DIRECTIVES

using System;

#endregion USING_DIRECTIVES

namespace Sharper.Exceptions
{
    public class ConcurrentOperationException : Exception
    {
        public ConcurrentOperationException(string message)
            : base(message)
        {
        }

        public ConcurrentOperationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}