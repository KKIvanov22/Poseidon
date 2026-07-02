using System.Text.RegularExpressions;

namespace Poseidon.Server.Endpoints;

public static partial class AuthValidation
{
    public const string PasswordRequirement =
        "Password must be at least 8 characters and include uppercase, lowercase, number, and special character.";

    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) &&
        email.Length <= 255 &&
        EmailRegex().IsMatch(email);

    public static bool IsValidPassword(string? password) =>
        !string.IsNullOrWhiteSpace(password) &&
        PasswordRegex().IsMatch(password);

    [GeneratedRegex(@"^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d]).{8,}$", RegexOptions.CultureInvariant)]
    private static partial Regex PasswordRegex();
}
