namespace V086Production.Api.Integrations;

public interface IEmailProvider
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default);
}

public sealed class MailpitEmailSender(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IEmailProvider
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        var baseUrl = configuration["Mail:Mailpit:BaseUrl"] ?? "http://localhost:8025/api/v1";
        var sender = configuration["Mail:Mailpit:From"] ?? "no-reply@localhost";
        using var response = await httpClientFactory.CreateClient().PostAsJsonAsync($"{baseUrl.TrimEnd('/')}/send", new { From = new { Email = sender }, To = new[] { new { Email = recipient } }, Subject = subject, Text = body }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailProvider
{
    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        using var client = new System.Net.Mail.SmtpClient(configuration["Mail:Smtp:Host"] ?? throw new InvalidOperationException("Mail:Smtp:Host is required."), configuration.GetValue("Mail:Smtp:Port", 25));
        client.EnableSsl = configuration.GetValue("Mail:Smtp:EnableSsl", true);
        client.Credentials = new System.Net.NetworkCredential(configuration["Mail:Smtp:Username"], configuration["Mail:Smtp:Password"]);
        using var message = new System.Net.Mail.MailMessage(configuration["Mail:Smtp:From"] ?? throw new InvalidOperationException("Mail:Smtp:From is required."), recipient, subject, body);
        await client.SendMailAsync(message, cancellationToken);
    }
}

public sealed class ConsoleEmailSender : IEmailProvider
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Application email to {recipient}: {subject}\n{body}");
        return Task.CompletedTask;
    }
}