using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Inventory.Contracts;

namespace Inventory.Server.Security;

public sealed class PasswordService
{
    // IdentityV3: random salt, PBKDF2-HMAC-SHA512, versioned hash format.
    private readonly PasswordHasher<string> _hasher = new(Options.Create(new PasswordHasherOptions { IterationCount = 600_000 }));
    private readonly string _dummy;
    public PasswordService() => _dummy = Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    public string Hash(string password) => _hasher.HashPassword("", password);
    public PasswordVerificationResult Verify(string hash, string password) => _hasher.VerifyHashedPassword("", hash ?? _dummy, password);
    public static string NormalizeUsername(string username)
    {
        if (username == null || !Regex.IsMatch(username, @"\A[a-zA-Z0-9_.-]{3,64}\z"))
            throw new ArgumentException("Логин: 3–64 символа, латинские буквы, цифры, точка, дефис или подчёркивание.");
        return username.ToUpperInvariant();
    }
    public static void ValidatePassword(string password)
    {
        if (password == null || password.Length < 12 || password.Length > 128)
            throw new ArgumentException("Пароль должен содержать от 12 до 128 символов.");
    }
    public static void Validate(CreateUserRequest user)
    {
        NormalizeUsername(user.Username); ValidatePassword(user.Password);
        if (!Roles.IsValid(user.Role)) throw new ArgumentException("Неизвестная роль.");
    }
}
