using System.Net;
using System.Net.Mail;
using ECommerceAPI.Application.Interfaces;

namespace ECommerceAPI.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var host     = _config["Smtp:Host"]     ?? throw new InvalidOperationException("Smtp:Host chưa được cấu hình trong appsettings.json");
        var port     = int.Parse(_config["Smtp:Port"] ?? "587");
        var username = _config["Smtp:Username"] ?? throw new InvalidOperationException("Smtp:Username chưa được cấu hình");
        var password = _config["Smtp:Password"] ?? throw new InvalidOperationException("Smtp:Password chưa được cấu hình");
        var fromEmail = _config["Smtp:FromEmail"] ?? username;
        var fromName  = _config["Smtp:FromName"]  ?? "E-Commerce";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl   = true,
            Credentials = new NetworkCredential(username, password),
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        using var mail = new MailMessage
        {
            From       = new MailAddress(fromEmail, fromName),
            Subject    = subject,
            Body       = htmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(to);

        _logger.LogInformation("Sending email to {To}, subject: {Subject}", to, subject);
        await client.SendMailAsync(mail);
    }
}
