using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AspNetCore.Identity.DocumentDb
{
    public class LookupNormalizer : ILookupNormalizer
    {
        public string Normalize(string key)
        {
            return key.Normalize().ToLowerInvariant();
        }

        public string NormalizeName(string name) => Normalize(name);

        public string NormalizeEmail(string email) => Normalize(email);
    }
}
