#region USING_DIRECTIVES

using Freud.Database.Db;
using Freud.Services;

#endregion USING_DIRECTIVES

namespace Freud.Modules
{
    public abstract class FreudServiceModule<TService> : FreudModule where TService : IFreudService
    {
        protected TService Service { get; }

        protected FreudServiceModule(TService service, SharedData shared, DatabaseContextBuilder dc)
            : base(shared, dc)
        {
            this.Service = service;
        }
    }
}
