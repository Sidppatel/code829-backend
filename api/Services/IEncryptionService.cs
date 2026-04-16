namespace Api.Services;

public interface IEncryptionService
{
    string HashEmail(string email);
}
