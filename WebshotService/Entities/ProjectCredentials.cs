using Newtonsoft.Json;
using System;
using System.Collections.Immutable;
using System.Runtime.Versioning;

namespace WebshotService.Entities
{
    public record ProjectCredentials
    {
        public ImmutableDictionary<Uri, AuthCredentials> CredentialsByDomain { get; init; } =
            ImmutableDictionary<Uri, AuthCredentials>.Empty;
    }

    [SupportedOSPlatform("windows")]
    public record AuthCredentials
    {
        public string User { get; init; }
        public string Password { get; init; }

        public string DecryptUser() => string.IsNullOrEmpty(User) ? "" : Encryption.Unprotect(User);

        public string DecryptPassword() => string.IsNullOrEmpty(Password) ? "" : Encryption.Unprotect(Password);

        /// <summary>
        /// Stores encrypted credentials for web authentication.
        /// </summary>
        /// <param name="user"></param>
        /// <param name="password"></param>
        /// <param name="encrypted">If the credentials should be encrypted using the windows user account.</param>
        [JsonConstructor]
        public AuthCredentials(string user, string password, bool encrypted = false)
        {
            User = encrypted ? Encryption.Protect(user) : user;
            Password = encrypted ? Encryption.Protect(password) : password;
        }
    }
}