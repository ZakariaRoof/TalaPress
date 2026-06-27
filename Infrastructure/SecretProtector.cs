using Microsoft.AspNetCore.DataProtection;

namespace TalaPress.Infrastructure;

public interface ISecretProtector
{
    string Protect(string plainText);
    string Unprotect(string protectedText);
}

public class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("TalaPress.SmtpPassword.v1");
    }

    public string Protect(string plainText) => _protector.Protect(plainText);

    public string Unprotect(string protectedText)
    {
        try
        {
            return _protector.Unprotect(protectedText);
        }
        catch
        {
            return string.Empty;
        }
    }
}
