namespace Tempo.Core.Models
{
    using System;
    using Tempo.Core.Helpers;

    /// <summary>
    /// API access credential for a user. Access keys authenticate API requests.
    /// </summary>
    public class Credential
    {
        /// <summary>Credential identifier (prefix "crd_").</summary>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>Tenant identifier.</summary>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>User identifier.</summary>
        public string UserId
        {
            get
            {
                return _UserId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(UserId));
                _UserId = value;
            }
        }

        /// <summary>Human-readable name for this credential.</summary>
        public string Name { get; set; } = "default";

        /// <summary>Public access key.</summary>
        public string AccessKey
        {
            get
            {
                return _AccessKey;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(AccessKey));
                _AccessKey = value;
            }
        }

        /// <summary>Private secret key reserved for non-API uses. Not accepted on API requests.</summary>
        public string SecretKey
        {
            get
            {
                return _SecretKey;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(SecretKey));
                _SecretKey = value;
            }
        }

        /// <summary>Whether the credential is active.</summary>
        public bool Active { get; set; } = true;

        /// <summary>Whether the credential is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;

        /// <summary>Creation timestamp in UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Last-updated timestamp in UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        private string _Id = IdGenerator.GenerateCredentialId();
        private string _TenantId = String.Empty;
        private string _UserId = String.Empty;
        private string _AccessKey = IdGenerator.GenerateAccessKey();
        private string _SecretKey = IdGenerator.GenerateSecretKey();
    }
}
