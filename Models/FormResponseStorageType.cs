namespace TalaPress.Models;

public static class FormResponseStorageType
{
    public const string Database = "Database";
    public const string Email = "Email";
    public const string Both = "Both";

    public static bool IsValid(string? value) =>
        value is Database or Email or Both;

    public static bool StoresInDatabase(string? value) =>
        string.IsNullOrEmpty(value) || value == Database || value == Both;

    public static bool SendsEmail(string? value) =>
        value == Email || value == Both;
}
