using System.Text.RegularExpressions;

namespace Api.Helpers;

public static partial class PasswordValidator
{
    private const int MinLength = 10;

    public static (bool IsValid, string? Error) Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            return (false, $"Password must be at least {MinLength} characters");

        if (!HasUppercase().IsMatch(password))
            return (false, "Password must contain at least one uppercase letter");

        if (!HasDigit().IsMatch(password))
            return (false, "Password must contain at least one digit");

        if (!HasSpecial().IsMatch(password))
            return (false, "Password must contain at least one special character");

        return (true, null);
    }

    [GeneratedRegex("[A-Z]")]
    private static partial Regex HasUppercase();

    [GeneratedRegex("[0-9]")]
    private static partial Regex HasDigit();

    [GeneratedRegex("[^a-zA-Z0-9]")]
    private static partial Regex HasSpecial();
}
