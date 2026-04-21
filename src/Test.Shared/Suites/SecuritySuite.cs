namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Tempo.Core.Security;
    using Tempo.Core.Services;
    using Tempo.Core.Settings;
    using Touchstone.Core;

    /// <summary>Password hashing and token cipher tests.</summary>
    public static class SecuritySuite
    {
        public static TestSuiteDescriptor Build()
        {
            return new TestSuiteDescriptor(
                suiteId: "Security",
                displayName: "Password hashing and token cipher",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Security", "Sha256Deterministic", "SHA-256 hash is deterministic and 64 hex chars", async _ =>
                    {
                        await Task.CompletedTask;
                        string a = PasswordHasher.Hash("password");
                        string b = PasswordHasher.Hash("password");
                        Assert2.Equal(a, b, "hashes match");
                        Assert2.Equal(64, a.Length, "hash is 64 chars");
                        foreach (char c in a) Assert2.True((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'), "hash char hex");
                    }),
                    new TestCaseDescriptor("Security", "VerifyPlaintext", "Verify succeeds against plaintext", async _ =>
                    {
                        await Task.CompletedTask;
                        string hash = PasswordHasher.Hash("correcthorse");
                        Assert2.True(PasswordHasher.Verify("correcthorse", hash), "plaintext verified");
                        Assert2.False(PasswordHasher.Verify("wrong", hash), "wrong plaintext rejected");
                    }),
                    new TestCaseDescriptor("Security", "VerifyHashed", "Verify accepts a pre-hashed submission", async _ =>
                    {
                        await Task.CompletedTask;
                        string hash = PasswordHasher.Hash("p");
                        Assert2.True(PasswordHasher.Verify(hash, hash), "hash-to-hash verified");
                    }),
                    new TestCaseDescriptor("Security", "VerifyEmpty", "Empty strings are rejected", async _ =>
                    {
                        await Task.CompletedTask;
                        Assert2.False(PasswordHasher.Verify("", "abc"), "empty submitted");
                        Assert2.False(PasswordHasher.Verify("abc", ""), "empty stored");
                    }),
                    new TestCaseDescriptor("Security", "HashNullThrows", "Hash of null throws ArgumentNullException", async _ =>
                    {
                        await Task.CompletedTask;
                        Assert2.Throws<ArgumentNullException>(() => PasswordHasher.Hash(null!), "null password");
                    }),
                    new TestCaseDescriptor("Security", "TokenRoundtripUser", "User token round trip restores all fields", async _ =>
                    {
                        await Task.CompletedTask;
                        AuthSettings auth = new AuthSettings { SigningKey = "this-is-a-sample-key-exceeding-32-chars-for-hash-derivation" };
                        TokenService svc = new TokenService(auth);
                        string token = svc.IssueUserToken("ten_abc", "usr_xyz", "acc_1");
                        AuthenticationToken? parsed = svc.Validate(token);
                        Assert2.NotNull(parsed, "parsed not null");
                        Assert2.Equal("ten_abc", parsed!.TenantId!, "tenant");
                        Assert2.Equal("usr_xyz", parsed.UserId!, "user");
                        Assert2.Equal("acc_1", parsed.AccountId!, "account");
                        Assert2.True(parsed.ExpiresUtc > DateTime.UtcNow, "not expired");
                    }),
                    new TestCaseDescriptor("Security", "TokenRoundtripAdmin", "Admin token round trip", async _ =>
                    {
                        await Task.CompletedTask;
                        AuthSettings auth = new AuthSettings { SigningKey = "another-signing-key-abcdef0123456789" };
                        TokenService svc = new TokenService(auth);
                        string token = svc.IssueAdminToken("adm_1", null);
                        AuthenticationToken? parsed = svc.Validate(token);
                        Assert2.NotNull(parsed, "parsed");
                        Assert2.Equal("adm_1", parsed!.AdministratorId!, "admin id");
                    }),
                    new TestCaseDescriptor("Security", "TokenTampered", "Tampered token fails validation", async _ =>
                    {
                        await Task.CompletedTask;
                        AuthSettings auth = new AuthSettings { SigningKey = "signing-key-for-tamper-test-abcdef" };
                        TokenService svc = new TokenService(auth);
                        string token = svc.IssueUserToken("ten_a", "usr_b");
                        string tampered = token.Substring(0, token.Length - 3) + "AAA";
                        AuthenticationToken? parsed = svc.Validate(tampered);
                        Assert2.IsNull(parsed, "tampered rejected");
                    }),
                    new TestCaseDescriptor("Security", "TokenExpired", "Expired tokens are rejected", async _ =>
                    {
                        await Task.CompletedTask;
                        AuthSettings auth = new AuthSettings { SigningKey = "signing-key-expired-test-abcdef" };
                        TokenCipher cipher = new TokenCipher(auth.SigningKey);
                        AuthenticationToken token = new AuthenticationToken
                        {
                            UserId = "usr_1",
                            TenantId = "ten_1",
                            IssuedUtc = DateTime.UtcNow.AddMinutes(-10),
                            ExpiresUtc = DateTime.UtcNow.AddMinutes(-1)
                        };
                        string encoded = cipher.Encrypt(token);
                        TokenService svc = new TokenService(auth);
                        Assert2.IsNull(svc.Validate(encoded), "expired rejected");
                    }),
                    new TestCaseDescriptor("Security", "TokenDifferentKey", "Token issued with one key is invalid with another", async _ =>
                    {
                        await Task.CompletedTask;
                        TokenService a = new TokenService(new AuthSettings { SigningKey = "key-alpha-xxxxxxxxxxxxxxxxxxxxxxxxxxxx" });
                        TokenService b = new TokenService(new AuthSettings { SigningKey = "key-beta-zzzzzzzzzzzzzzzzzzzzzzzzzzzzzz" });
                        string token = a.IssueUserToken("ten_1", "usr_1");
                        Assert2.IsNull(b.Validate(token), "wrong key");
                    }),
                    new TestCaseDescriptor("Security", "TokenGarbage", "Garbage strings return null", async _ =>
                    {
                        await Task.CompletedTask;
                        TokenService svc = new TokenService(new AuthSettings { SigningKey = "some-key-xxxxxxxxxxxxxxxxxxxxxxxxxxxx" });
                        Assert2.IsNull(svc.Validate(""), "empty");
                        Assert2.IsNull(svc.Validate("@@not-base64@@"), "not base64");
                        Assert2.IsNull(svc.Validate("abc"), "too short");
                    })
                });
        }
    }
}
