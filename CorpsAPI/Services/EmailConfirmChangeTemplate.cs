using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;

namespace CorpsAPI.Services
{
    public class EmailConfirmChangeTemplate
    {
        public static string ConfirmEmailChangeHtml(
            string appName,
            string currentEmail,
            string newEmail,
            string confirmationUrl,
            string? logoUrl = null,
            string? supportEmail = null)
        {
            var year = DateTime.UtcNow.Year;
            var safeApp = string.IsNullOrWhiteSpace(appName) ? "Your Corps" : appName;
            const string preheader = "Confirm your new email to finish updating your account.";

            return $@"
        <!DOCTYPE html>
        <html lang=""en"">
        <head>
        <meta charset=""UTF-8"">
        <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
        <title>{safeApp} — Confirm new email</title>
        </head>
        <body style=""margin:0; padding:0; background:#000000; color:#ffffff;"">
        <div style=""display:none; font-size:1px; color:#000000; line-height:1px; max-height:0; max-width:0; opacity:0; overflow:hidden;"">
            {preheader}
        </div>

        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background:#000000;"">
            <tr>
            <td align=""center"">
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""width:600px; max-width:100%;"">
                <tr>
                    <td style=""padding:32px 20px 0 20px; text-align:center;"">
                    {(string.IsNullOrWhiteSpace(logoUrl) ? $@"<div style=""display:inline-block;width:56px;height:56px;border-radius:50%;background:#D01417;border:4px solid #000;""></div>"
                        : $@"<img src=""{logoUrl}"" alt=""{safeApp}"" width=""120"" style=""max-width:160px; height:auto; display:inline-block;"">")}
                    </td>
                </tr>

                <tr>
                    <td style=""padding:24px 20px 0 20px;"">
                    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" 
                            style=""background:#1C1C1C; border-radius:12px; border:1px solid #2a2a2a;"">
                        <tr>
                        <td style=""padding:28px 24px 8px 24px; text-align:center;"">
                            <h1 style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:24px; line-height:1.3; color:#ffffff;"">
                            Confirm your new email
                            </h1>
                            <div style=""height:10px;""></div>
                            <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:14px; line-height:1.6; color:#cfcfcf;"">
                            You're changing your <strong>{safeApp}</strong> account email.
                            </p>
                        </td>
                        </tr>

                        <tr>
                        <td style=""padding:16px 24px 0 24px; text-align:left;"">
                            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" 
                                style=""background:#151515; border:1px solid #2a2a2a; border-radius:10px;"">
                            <tr>
                                <td style=""padding:14px 16px; font-family:Segoe UI, Arial, sans-serif; font-size:13px; color:#cfcfcf;"">
                                <div style=""margin-bottom:6px;""><strong>Current:</strong> {WebUtility.HtmlEncode(currentEmail)}</div>
                                <div><strong>New:</strong> {WebUtility.HtmlEncode(newEmail)}</div>
                                </td>
                            </tr>
                            </table>
                        </td>
                        </tr>

                        <tr>
                        <td style=""padding:24px 24px 8px 24px; text-align:center;"">
                            <a href=""{confirmationUrl}"" target=""_blank""
                            style=""display:inline-block; font-family:Segoe UI, Arial, sans-serif; text-decoration:none;
                                    background:#D01417; color:#ffffff; padding:12px 22px; border-radius:8px; 
                                    font-weight:700; font-size:14px; border:3px solid #000;"">
                            Confirm new email
                            </a>
                        </td>
                        </tr>

                        <tr>
                        <td style=""padding:12px 24px 20px 24px; text-align:center;"">
                            <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:12px; line-height:1.6; color:#9e9e9e;"">
                            Or paste this link into your browser:
                            <br>
                            <a href=""{confirmationUrl}"" target=""_blank"" 
                                style=""color:#9ecbff; word-break:break-all; text-decoration:underline;"">{confirmationUrl}</a>
                            </p>
                        </td>
                        </tr>

                    </table>
                    </td>
                </tr>

                <tr>
                    <td style=""padding:16px 20px 0 20px;"">
                    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" 
                            style=""background:#121212; border-radius:12px; border:1px solid #1f1f1f;"">
                        <tr>
                        <td style=""padding:16px 20px;"">
                            <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:12px; line-height:1.6; color:#cfcfcf;"">
                            If you didn’t request this change, your current email will remain active. You can safely ignore this message.
                            {(string.IsNullOrWhiteSpace(supportEmail) ? "" : $@" For help, contact <a href=""mailto:{supportEmail}"" style=""color:#9ecbff; text-decoration:underline;"">{supportEmail}</a>.")}
                            </p>
                        </td>
                        </tr>
                    </table>
                    </td>
                </tr>

                <tr>
                    <td style=""padding:24px 20px 40px 20px; text-align:center;"">
                    <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:12px; line-height:1.8; color:#8a8a8a;"">
                        © {year} {safeApp}. All rights reserved.
                    </p>
                    </td>
                </tr>

                </table>
            </td>
            </tr>
        </table>
        </body>
        </html>";
        }

        
    }
}