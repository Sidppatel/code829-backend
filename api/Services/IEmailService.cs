namespace Api.Services;

public interface IEmailService
{
    Task SendAsync(string recipient, string subject, string body);
}
