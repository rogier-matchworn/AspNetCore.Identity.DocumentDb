﻿using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using System.Net;
using AspNetCore.Identity.DocumentDb.Tools;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace AspNetCore.Identity.DocumentDb.Stores
{
    /// <summary>
    /// Represents a DocumentDb-based persistence store for ASP.NET Core Identity users with the role type defaulted to <see cref="DocumentDbIdentityRole"/>
    /// </summary>
    /// <typeparam name="TUser">The type representing a user</typeparam>
    public class DocumentDbUserStore<TUser> : DocumentDbUserStore<TUser, DocumentDbIdentityRole>
        where TUser : DocumentDbIdentityUser
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentDbUserStore{TUser}"/>
        /// </summary>
        /// <param name="documentClient">The DocumentDb client to be used</param>
        /// <param name="options">The configuraiton options for the <see cref="IDocumentClient"/></param>
        /// <param name="roleStore">The <see cref="IRoleStore{TRole}"/> to be used for storing and retrieving roles for the user</param>
        public DocumentDbUserStore(ICosmosClientAccessor clientAccessor, IOptions<DocumentDbOptions> options, IRoleStore<DocumentDbIdentityRole> roleStore)
            : base(clientAccessor, options, roleStore)
        {
        }
    }

    /// <summary>
    /// Represents a DocumentDb-based persistence store for ASP.NET Core Identity users
    /// </summary>
    /// <typeparam name="TUser">The type representing a user</typeparam>
    /// <typeparam name="TRole">The type representing a role</typeparam>
    public class DocumentDbUserStore<TUser, TRole> :
        StoreBase,
        IUserStore<TUser>,
        IUserClaimStore<TUser>,
        IUserLoginStore<TUser>,
        IUserRoleStore<TUser>,
        IUserPasswordStore<TUser>,
        IUserSecurityStampStore<TUser>,
        IUserTwoFactorStore<TUser>,
        IUserPhoneNumberStore<TUser>,
        IUserEmailStore<TUser>,
        IUserAuthenticatorKeyStore<TUser>,
        IUserTwoFactorRecoveryCodeStore<TUser>,
        IUserLockoutStore<TUser>,
        IQueryableUserStore<TUser>
        where TUser : DocumentDbIdentityUser<TRole>
        where TRole : DocumentDbIdentityRole
    {
        private readonly IRoleStore<TRole> roleStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentDbUserStore{TUser, TRole}"/>
        /// </summary>
        /// <param name="documentClient">The DocumentDb client to be used</param>
        /// <param name="options">The configuraiton options for the <see cref="IDocumentClient"/></param>
        /// <param name="roleStore">The <see cref="IRoleStore{TRole}"/> to be used for storing and retrieving roles for the user</param>
        public DocumentDbUserStore(ICosmosClientAccessor clientAccessor, IOptions<DocumentDbOptions> options, IRoleStore<TRole> roleStore)
            : base(clientAccessor, options, options.Value.UserStoreDocumentCollection)
        {
            this.roleStore = roleStore;
        }

        public IQueryable<TUser> Users => container.GetItemLinqQueryable<TUser>(allowSynchronousQueryExecution: true);

        public async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            // If no UserId was supplied, generate a GUID
            if (string.IsNullOrEmpty(user.Id))
            {
                user.Id = Guid.NewGuid().ToString();
            }

            ItemResponse<TUser> result = await container.CreateItemAsync(user);

            return result.StatusCode == HttpStatusCode.Created
                ? IdentityResult.Success
                : IdentityResult.Failed(new IdentityError() { Code = result.StatusCode.ToString() });
        }

        public async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            try
            {
                await container.DeleteItemAsync<TUser>(user.Id, PartitionKey.None);
            }
            catch (CosmosException cex) when (cex.StatusCode == HttpStatusCode.NotFound)
            {
                return IdentityResult.Failed();
            }

            return IdentityResult.Success;
        }

        public async Task<TUser> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (userId == null)
            {
                throw new ArgumentNullException(nameof(userId));
            }

            try
            {
                TUser user = await container.ReadItemAsync<TUser>(userId, PartitionKey.None);
                return user;
            }
            catch (CosmosException cex) when (cex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public Task<TUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (normalizedUserName == null)
            {
                throw new ArgumentNullException(nameof(normalizedUserName));
            }

            TUser user = container.GetItemLinqQueryable<TUser>(allowSynchronousQueryExecution: true)
                .Where(u => u.NormalizedUserName == normalizedUserName && u.DocumentType == typeof(TUser).Name)
                .ToList()
                .FirstOrDefault();
            return Task.FromResult(user);
        }

        public Task<string> GetNormalizedUserNameAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.NormalizedUserName);
        }

        public Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.Id);
        }

        public Task<string> GetUserNameAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.UserName);
        }

        public Task SetNormalizedUserNameAsync(TUser user, string normalizedName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.NormalizedUserName = normalizedName ?? throw new ArgumentNullException(nameof(normalizedName));

            return Task.CompletedTask;
        }

        public Task SetUserNameAsync(TUser user, string userName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.UserName = userName ?? throw new ArgumentNullException(nameof(userName));

            return Task.CompletedTask;
        }

        public async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            try
            {
                await container.ReplaceItemAsync(user, user.Id);
            }
            catch (CosmosException cex) when (cex.StatusCode == HttpStatusCode.NotFound)
            {
                return IdentityResult.Failed();
            }

            return IdentityResult.Success;
        }

        public Task<IList<Claim>> GetClaimsAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.Claims);
        }

        public Task AddClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (claims == null)
            {
                throw new ArgumentNullException(nameof(claims));
            }

            foreach (Claim newClaim in claims)
            {
                user.Claims.Add(newClaim);
            }

            return Task.CompletedTask;
        }

        public Task ReplaceClaimAsync(TUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (claim == null)
            {
                throw new ArgumentNullException(nameof(claim));
            }

            if (newClaim == null)
            {
                throw new ArgumentNullException(nameof(newClaim));
            }

            if (user.Claims.Any(c => c.Equals(claim)))
            {
                user.Claims.Remove(claim);
                user.Claims.Add(newClaim);
            }

            return Task.CompletedTask;
        }

        public Task RemoveClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (claims == null)
            {
                throw new ArgumentNullException(nameof(claims));
            }

            IEnumerable<Claim> foundClaims = user.Claims.Where(c => claims.Any(rc => rc.Type == c.Type && rc.Value == c.Value)).ToList();

            foreach (Claim claimToRemove in foundClaims)
            {
                user.Claims.Remove(claimToRemove);
            }

            return Task.CompletedTask;
        }

        public Task<IList<TUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (claim == null)
            {
                throw new ArgumentNullException(nameof(claim));
            }

            IList<TUser> results = container.GetItemLinqQueryable<TUser>(allowSynchronousQueryExecution: true)
                .SelectMany(u => u.Claims
                    .Where(c => c.Type == claim.Type && c.Value == claim.Value)
                    .Select(c => u)
                ).ToList();
            return Task.FromResult(results);
        }

        public Task AddLoginAsync(TUser user, UserLoginInfo login, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (login == null)
            {
                throw new ArgumentNullException(nameof(login));
            }

            user.Logins.Add(login);

            return Task.CompletedTask;
        }

        public Task RemoveLoginAsync(TUser user, string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (loginProvider == null)
            {
                throw new ArgumentNullException(nameof(loginProvider));
            }

            if (providerKey == null)
            {
                throw new ArgumentNullException(nameof(providerKey));
            }

            UserLoginInfo userLoginToRemove = user.Logins.FirstOrDefault(l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey);

            if (userLoginToRemove != null)
            {
                user.Logins.Remove(userLoginToRemove);
            }

            return Task.CompletedTask;
        }

        public Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.Logins);
        }

        public Task<TUser> FindByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (loginProvider == null)
            {
                throw new ArgumentNullException(nameof(loginProvider));
            }

            if (loginProvider == null)
            {
                throw new ArgumentNullException(nameof(loginProvider));
            }

            TUser user = container.GetItemLinqQueryable<TUser>(allowSynchronousQueryExecution: true)
                .SelectMany(u => u.Logins
                    .Where(l => l.LoginProvider == loginProvider && l.ProviderKey == providerKey)
                    .Select(l => u)
                ).ToList().FirstOrDefault();

            return Task.FromResult(user);
        }

        public async Task AddToRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (normalizedRoleName == null)
            {
                throw new ArgumentNullException(nameof(normalizedRoleName));
            }

            // Check if the given role name exists
            TRole foundRole = await roleStore.FindByNameAsync(normalizedRoleName, cancellationToken);

            if (foundRole == null)
            {
                throw new ArgumentException(nameof(normalizedRoleName), $"The role with the given name {normalizedRoleName} does not exist");
            }

            user.Roles.Add(foundRole);
        }

        public Task RemoveFromRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (normalizedRoleName == null)
            {
                throw new ArgumentNullException(nameof(normalizedRoleName));
            }

            TRole roleToRemove = user.Roles.FirstOrDefault(r => r.NormalizedName == normalizedRoleName);

            if (roleToRemove != null)
            {
                user.Roles.Remove(roleToRemove);
            }

            return Task.CompletedTask;
        }

        public Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            IList<string> userRoles = user.Roles.Select(r => r.Name).ToList();

            return Task.FromResult(userRoles);
        }

        public Task<bool> IsInRoleAsync(TUser user, string normalizedRoleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (normalizedRoleName == null)
            {
                throw new ArgumentNullException(nameof(normalizedRoleName));
            }

            bool isInRole = user.Roles.Any(r => r.NormalizedName.Equals(normalizedRoleName));

            return Task.FromResult(isInRole);
        }

        public Task<IList<TUser>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (normalizedRoleName == null)
            {
                throw new ArgumentNullException(nameof(normalizedRoleName));
            }

            IList<TUser> result = container.GetItemLinqQueryable<TUser>(allowSynchronousQueryExecution: true)
                .SelectMany(u => u.Roles
                    .Where(r => r.NormalizedName == normalizedRoleName)
                    .Select(r => u)
                ).ToList();

            return Task.FromResult(result);
        }

        public Task SetPasswordHashAsync(TUser user, string passwordHash, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));

            return Task.CompletedTask;
        }

        public Task<string> GetPasswordHashAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.PasswordHash);
        }

        public Task<bool> HasPasswordAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.PasswordHash != null);
        }

        public Task SetSecurityStampAsync(TUser user, string stamp, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.SecurityStamp = stamp;

            return Task.CompletedTask;
        }

        public Task<string> GetSecurityStampAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.SecurityStamp);
        }

        public Task SetTwoFactorEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.IsTwoFactorAuthEnabled = enabled;

            return Task.CompletedTask;
        }

        public Task<bool> GetTwoFactorEnabledAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.IsTwoFactorAuthEnabled);
        }

        public Task SetPhoneNumberAsync(TUser user, string phoneNumber, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.PhoneNumber = phoneNumber;

            return Task.CompletedTask;
        }

        public Task<string> GetPhoneNumberAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.PhoneNumber);
        }

        public Task<bool> GetPhoneNumberConfirmedAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.IsPhoneNumberConfirmed);
        }

        public Task SetPhoneNumberConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.IsPhoneNumberConfirmed = confirmed;

            return Task.CompletedTask;
        }

        public Task SetEmailAsync(TUser user, string email, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.Email = email;

            return Task.FromResult(user.Email);
        }

        public Task<string> GetEmailAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.Email);
        }

        public Task<bool> GetEmailConfirmedAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.IsEmailConfirmed);
        }

        public Task SetEmailConfirmedAsync(TUser user, bool confirmed, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.IsEmailConfirmed = confirmed;

            return Task.FromResult(user.Email);
        }

        public Task<TUser> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (normalizedEmail == null)
            {
                throw new ArgumentNullException(nameof(normalizedEmail));
            }

            TUser user = container.GetItemLinqQueryable<TUser>(allowSynchronousQueryExecution: true)
                .Where(u => u.NormalizedEmail == normalizedEmail && u.DocumentType == typeof(TUser).Name)
                .ToList()
                .FirstOrDefault();

            return Task.FromResult(user);
        }

        public Task<string> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.NormalizedEmail);
        }

        public Task SetNormalizedEmailAsync(TUser user, string normalizedEmail, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.NormalizedEmail = normalizedEmail;

            return Task.FromResult(user.Email);
        }

        public Task<DateTimeOffset?> GetLockoutEndDateAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.LockoutEndDate);
        }

        public Task SetLockoutEndDateAsync(TUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.LockoutEndDate = lockoutEnd;

            return Task.CompletedTask;
        }

        public Task<int> IncrementAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.AccessFailedCount++;

            return Task.FromResult(user.AccessFailedCount);
        }

        public Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.AccessFailedCount = 0;

            return Task.CompletedTask;
        }

        public Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.AccessFailedCount);
        }

        public Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.LockoutEnabled);
        }

        public Task SetLockoutEnabledAsync(TUser user, bool enabled, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.LockoutEnabled = enabled;

            return Task.CompletedTask;
        }


        public Task SetAuthenticatorKeyAsync(TUser user, string key, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.AuthenticatorKey = key;

            return Task.CompletedTask;
        }

        public Task<string> GetAuthenticatorKeyAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return Task.FromResult(user.AuthenticatorKey);
        }

        public Task ReplaceCodesAsync(TUser user, IEnumerable<string> recoveryCodes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            user.RecoveryCodes = recoveryCodes;

            return Task.CompletedTask;
        }

        public async Task<bool> RedeemCodeAsync(TUser user, string code, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            if (user.RecoveryCodes.Contains(code))
            {
                var codes = user.RecoveryCodes.Where(x => x != code);
                await ReplaceCodesAsync(user, codes, cancellationToken);
                return true;
            }
            return false;
        }

        public Task<int> CountCodesAsync(TUser user, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var recoveryCodesCount = user.RecoveryCodes?.Count();
            return Task.FromResult(recoveryCodesCount ?? 0);
        }

        #region IDisposable Support

        public void Dispose()
        {
            // TODO: Workaround, gets disposed too early currently
            disposed = false;
        }

        #endregion
    }
}
