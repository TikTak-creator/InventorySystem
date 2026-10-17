using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EasyDo.Data;
using Inventory.Contracts;
using Inventory.Storage;
using Microsoft.AspNetCore.Identity;

namespace Inventory.Server.Security;

public sealed class UserStore(Store store, PasswordService passwords, TimeProvider time, IConfiguration config)
{
    private static readonly string[] Schema =
    [
        """
        CREATE TABLE IF NOT EXISTS app_users (
            id INT AUTO_INCREMENT PRIMARY KEY,
            username VARCHAR(64) NOT NULL,
            normalized_username VARCHAR(64) NOT NULL UNIQUE,
            password_hash VARCHAR(512) NOT NULL,
            role VARCHAR(16) NOT NULL,
            active BOOLEAN NOT NULL DEFAULT TRUE,
            failed_attempts INT NOT NULL DEFAULT 0,
            locked_until DATETIME NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """,
        """
        CREATE TABLE IF NOT EXISTS app_sessions (
            token_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
            user_id INT NOT NULL,
            expires_at DATETIME NOT NULL,
            FOREIGN KEY (user_id) REFERENCES app_users(id) ON DELETE CASCADE,
            INDEX ix_session_expiry (expires_at)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
        """
    ];
    public void Initialize() => store.Run(s => { foreach (var ddl in Schema) s.Execute(Sql.TextOnly(ddl)); });
    private sealed record Account(int Id, string Username, string Hash, string Role, bool Active, int Failures, DateTime? LockedUntil);
    public async Task<LoginResponse> Login(LoginRequest request, CancellationToken cancellation)
    {
        var name = PasswordService.NormalizeUsername(request.Username);
        if (request.Password == null || request.Password.Length > 128) throw new ArgumentException("Некорректные данные входа.");
        var now = time.GetUtcNow();
        var sessionMinutes = config.GetValue("Security:SessionMinutes", 60);
        return await store.TransactionAsync(async (s, ct) =>
        {
            var user = (await s.QueryAsync(Sql.TextOnly("""
                SELECT id, username, password_hash, role, active, failed_attempts, locked_until
                FROM app_users WHERE normalized_username=@name FOR UPDATE
                """).Bind("name", name), r => new Account(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3),
                    r.GetBoolean(4), r.GetInt32(5), r.IsDBNull(6) ? null : r.GetDateTime(6)), ct)).SingleOrDefault();
            var verified = passwords.Verify(user?.Hash, request.Password);
            if (user == null || !user.Active || user.LockedUntil > now.UtcDateTime) return null;
            if (verified == PasswordVerificationResult.Failed)
            {
                var failures = user.LockedUntil.HasValue ? 1 : user.Failures + 1;
                await s.ExecuteAsync(Sql.TextOnly("UPDATE app_users SET failed_attempts=@fail, locked_until=@lock WHERE id=@id")
                    .Bind("fail", failures).Bind("lock", failures >= 5 ? now.AddMinutes(15).UtcDateTime : null).Bind("id", user.Id), ct);
                return null; // Return after commit, so failed-attempt counters are retained.
            }
            var hash = verified == PasswordVerificationResult.SuccessRehashNeeded ? passwords.Hash(request.Password) : user.Hash;
            await s.ExecuteAsync(Sql.TextOnly("UPDATE app_users SET failed_attempts=0, locked_until=NULL, password_hash=@hash WHERE id=@id")
                .Bind("hash", hash).Bind("id", user.Id), ct);
            await s.ExecuteAsync(Sql.TextOnly("DELETE FROM app_sessions WHERE expires_at <= @now").Bind("now", now.UtcDateTime), ct);
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var expires = now.AddMinutes(sessionMinutes);
            await s.ExecuteAsync(Sql.TextOnly("INSERT INTO app_sessions (token_hash, user_id, expires_at) VALUES (@hash, @user, @expires)")
                .Bind("hash", TokenHash(token)).Bind("user", user.Id).Bind("expires", expires.UtcDateTime), ct);
            return new LoginResponse(token, expires, new UserInfo(user.Id, user.Username, user.Role));
        }, cancellation);
    }
    private static string TokenHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public Task<UserInfo> Authenticate(string token, CancellationToken cancellation)
    {
        if (token == null || !Regex.IsMatch(token, @"\A[A-Za-z0-9_-]{43}\z")) return Task.FromResult<UserInfo>(null);
        return store.ReadAsync(async (s, ct) => (await s.QueryAsync(Sql.TextOnly("""
            SELECT u.id, u.username, u.role FROM app_sessions t JOIN app_users u ON u.id=t.user_id
            WHERE t.token_hash=@hash AND t.expires_at>@now AND u.active=TRUE
            """).Bind("hash", TokenHash(token)).Bind("now", time.GetUtcNow().UtcDateTime),
            r => new UserInfo(r.GetInt32(0), r.GetString(1), r.GetString(2)), ct)).SingleOrDefault(), cancellation);
    }
    public Task Logout(string token, CancellationToken cancellation) => store.ReadAsync((s, ct) =>
        s.ExecuteAsync(Sql.TextOnly("DELETE FROM app_sessions WHERE token_hash=@hash").Bind("hash", TokenHash(token)), ct), cancellation);
    public List<UserInfo> List() => store.Read(s => s.Query(Sql.TextOnly("SELECT id, username, role FROM app_users ORDER BY username"),
        r => new UserInfo(r.GetInt32(0), r.GetString(1), r.GetString(2))));
    public UserInfo Create(CreateUserRequest request)
    {
        PasswordService.Validate(request);
        var hash = passwords.Hash(request.Password);
        return store.Read(s =>
        {
            s.Execute(Sql.TextOnly("INSERT INTO app_users (username, normalized_username, password_hash, role) VALUES (@name, @normal, @hash, @role)")
                .Bind("name", request.Username).Bind("normal", PasswordService.NormalizeUsername(request.Username)).Bind("hash", hash).Bind("role", request.Role));
            return new UserInfo(s.Scalar<int>(Sql.TextOnly("SELECT LAST_INSERT_ID()")), request.Username, request.Role);
        });
    }
    public void SetPassword(int userId, string password)
    {
        PasswordService.ValidatePassword(password); var hash = passwords.Hash(password);
        store.Transaction(s =>
        {
            if (s.Execute(Sql.TextOnly("UPDATE app_users SET password_hash=@hash, failed_attempts=0, locked_until=NULL WHERE id=@id")
                .Bind("hash", hash).Bind("id", userId)) == 0) throw new MissingEntityException("Пользователь не найден.");
            s.Execute(Sql.TextOnly("DELETE FROM app_sessions WHERE user_id=@id").Bind("id", userId));
        });
    }
    public void SetActive(int userId, bool active)
    {
        store.Transaction(s =>
        {
            if (s.Scalar<int>(Sql.TextOnly("SELECT COUNT(*) FROM app_users WHERE id=@id FOR UPDATE").Bind("id", userId)) == 0)
                throw new MissingEntityException("Пользователь не найден.");
            s.Execute(Sql.TextOnly("UPDATE app_users SET active=@active WHERE id=@id").Bind("active", active).Bind("id", userId));
            s.Execute(Sql.TextOnly("DELETE FROM app_sessions WHERE user_id=@id").Bind("id", userId));
        });
    }
}
