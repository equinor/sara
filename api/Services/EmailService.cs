using api.Configurations;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace api.Services
{
    public enum EmailTarget
    {
        Fencilla,
    }

    public interface IEmailService
    {
        public Task SendMailAsync(string subject, string body, EmailTarget recipient);
    }

    public record EmailOptions
    {
        public string FencillaTargetEmail { get; init; } = "";
        public string FencillaEmailGroup { get; init; } = "";
    }

    public class EmailService : IEmailService
    {
        private readonly GraphServiceClient appClient;
        private readonly EmailOptions _emailOptions;

        public EmailService(
            IOptions<AzureAdOptions> azureAdOptions,
            IOptions<EmailOptions> emailOptions
        )
        {
            var clientSecretCredential = new ClientSecretCredential(
                azureAdOptions.Value.TenantId,
                azureAdOptions.Value.ClientId,
                azureAdOptions.Value.ClientSecret
            );

            appClient = new GraphServiceClient(
                clientSecretCredential,
                ["https://graph.microsoft.com/.default"]
            );

            _emailOptions = emailOptions.Value;
        }

        public async Task SendMailAsync(string subject, string body, EmailTarget recipient)
        {
            var recipientEmail = recipient switch
            {
                EmailTarget.Fencilla => _emailOptions.FencillaTargetEmail,
                _ => throw new ArgumentException("No email found for given email target"),
            };

            var emailGroup = recipient switch
            {
                EmailTarget.Fencilla => _emailOptions.FencillaEmailGroup,
                _ => throw new ArgumentException("No email found for given email group"),
            };

            // Create a new message
            var message = new Message
            {
                Subject = subject,
                Body = new ItemBody { Content = body, ContentType = BodyType.Text },
                ToRecipients =
                [
                    new Recipient { EmailAddress = new EmailAddress { Address = recipientEmail } },
                ],
            };

            // Send the message
            await appClient
                .Users[emailGroup]
                .SendMail.PostAsync(
                    new SendMailPostRequestBody { Message = message, SaveToSentItems = true }
                );
        }
    }
}
