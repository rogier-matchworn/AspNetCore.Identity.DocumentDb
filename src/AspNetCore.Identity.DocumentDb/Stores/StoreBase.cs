using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AspNetCore.Identity.DocumentDb.Stores
{
    public abstract class StoreBase
    {
        protected bool disposed = false;

        protected Container container;
        protected DocumentDbOptions options;       

        protected StoreBase(ICosmosClientAccessor clientAccessor, IOptions<DocumentDbOptions> options, string collectionName)
        {
            this.options = options.Value;
            container = clientAccessor.Client.GetContainer(options.Value.Database, collectionName);
        }

        protected virtual void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
