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
        public Task SendMailAsync(
            string subject,
            string body,
            EmailTarget recipient,
            string installation
        );
        public Task SendFencillaResultEmail(
            string inspectionId,
            float? confidence,
            string installation
        );
    }

    public record AnalysisImageEmailOptions
    {
        public string TargetEmail { get; set; } = "";
        public string EmailGroup { get; set; } = "";
        public string? AnalysedImageBasePath { get; set; } = "";
    }

    public record FencillaOptions
    {
        public Dictionary<string, AnalysisImageEmailOptions> Installations { get; set; } = [];
    }

    public record EmailOptions
    {
        public FencillaOptions Fencilla { get; set; } = new FencillaOptions();
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

        public async Task SendMailAsync(
            string subject,
            string body,
            EmailTarget recipient,
            string installation
        )
        {
            installation = installation.ToUpperInvariant();

            var recipientEmail = recipient switch
            {
                EmailTarget.Fencilla => _emailOptions
                    .Fencilla
                    .Installations[installation]
                    .TargetEmail,
                _ => throw new ArgumentException("No email found for given email target"),
            };

            var emailGroup = recipient switch
            {
                EmailTarget.Fencilla => _emailOptions
                    .Fencilla
                    .Installations[installation]
                    .EmailGroup,
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

        public async Task SendFencillaResultEmail(
            string inspectionId,
            float? confidence,
            string installation
        )
        {
            installation = installation.ToUpperInvariant();
            if (!_emailOptions.Fencilla.Installations.ContainsKey(installation))
                return;

            var title = "[AI] Potensielt perimeterbrudd";
            var flotillaImageUrl =
                $"{_emailOptions.Fencilla.Installations[installation].AnalysedImageBasePath}{installation}:mission-simple?analysisId={inspectionId}";
            var urlMessage = $"\nBildet er tilgjengelig her:\n\n{flotillaImageUrl}";
            var confidenceMessage =
                confidence != null
                    ? $"\nDenne analysen har en estimert treffsikkerhet på {Convert.ToInt32(confidence * 100.0)}%."
                    : "";
            var body =
                $"Et potensielt brudd har blitt detektert langs gjerdet (enten et hull eller et nytt objekt).{confidenceMessage}{urlMessage}";
            await SendMailAsync(title, body, EmailTarget.Fencilla, installation);
        }
    }
}
