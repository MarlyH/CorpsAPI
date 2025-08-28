// using Microsoft.Extensions.Options;
// using System.Net;
// using System.Net.Mail;

// namespace CorpsAPI.Services
// {
//     public class EmailService
//     {
//         private readonly EmailSettings _emailSettings;

//         public EmailService(IOptions<EmailSettings> emailSettings)
//         {
//             _emailSettings = emailSettings.Value;
//         }

//         public async Task SendEmailAsync(string toEmail, string subject, string body)
//         {
//             var message = new MailMessage
//             {
//                 From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
//                 Subject = subject,
//                 Body = body,
//                 IsBodyHtml = true
//             };

//             message.To.Add(toEmail);

//             using var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port)
//             {
//                 Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.Password),
//                 EnableSsl = true
//             };

//             await client.SendMailAsync(message);
//         }
//     }
// }
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;

namespace CorpsAPI.Services
{
    public class EmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        /// <summary>
        /// Backward-compatible simple HTML email.
        /// </summary>
        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port)
            {
                Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.Password),
                EnableSsl = true
            };

            await client.SendMailAsync(message);
        }

        /// <summary>
        /// HTML email with a single inline image referenced by &lt;img src="cid:{contentId}" /&gt;.
        /// Pass null for inlineBytes/contentId to send without an image.
        /// </summary>
        public async Task SendEmailWithInlineAsync(
            string toEmail,
            string subject,
            string htmlBody,
            byte[]? inlineBytes,
            string? contentId,
            string mediaType = MediaTypeNames.Image.Jpeg // or MediaTypeNames.Image.Png
        )
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_emailSettings.SenderEmail, _emailSettings.SenderName),
                Subject = subject,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            // Build HTML view
            var htmlView = AlternateView.CreateAlternateViewFromString(
                htmlBody, null, MediaTypeNames.Text.Html);

            // Attach inline image if provided
            if (inlineBytes != null && !string.IsNullOrWhiteSpace(contentId))
            {
                // Keep stream open for the duration of send (MailMessage disposes it later)
                var stream = new MemoryStream(inlineBytes);
                var lr = new LinkedResource(stream, mediaType)
                {
                    ContentId = contentId,
                    TransferEncoding = TransferEncoding.Base64,
                };
                // Optional: name helps some clients
                lr.ContentType.Name = "qr-code";
                htmlView.LinkedResources.Add(lr);
            }

            message.AlternateViews.Add(htmlView);

            using var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.Port)
            {
                Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.Password),
                EnableSsl = true
            };

            await client.SendMailAsync(message);
        }
    }
}
