using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Text;

namespace AspNetCore.Identity.DocumentDb
{
    public interface ICosmosClientAccessor
    {
        CosmosClient Client { get; }
    }
}
