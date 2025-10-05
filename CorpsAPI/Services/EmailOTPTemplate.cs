using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpsAPI.Services
{
    public class EmailOTPTemplate
    {
        /// <summary>
        /// On-brand OTP email for password reset (dark theme, inline CSS, mobile-friendly).
        /// </summary>
        public static string PasswordResetOtpHtml(
            string appName,
            string? logoUrl,
            string? supportEmail,
            string userDisplayName,
            string otpCode,
            int expiresInMinutes
        )
        {
            var year = DateTime.UtcNow.Year;
            var safeApp = string.IsNullOrWhiteSpace(appName) ? "Your Corps" : appName;
            var preheader = $"Use this code to reset your {safeApp} password.";

            return $@"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
            <meta charset=""UTF-8"">
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
            <title>{safeApp} — Reset your password</title>
            </head>
            <body style=""margin:0; padding:0; background:#000000; color:#ffffff;"">
            <!-- Preheader -->
            <div style=""display:none; font-size:1px; color:#000000; line-height:1px; max-height:0; max-width:0; opacity:0; overflow:hidden;"">
                {preheader}
            </div>

            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background:#000000;"">
                <tr>
                <td align=""center"">
                    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""width:600px; max-width:100%;"">
                    <!-- Header / Logo -->
                    <tr>
                        <td style=""padding:32px 20px 0 20px; text-align:center;"">
                        {(string.IsNullOrWhiteSpace(logoUrl)
                            ? @"<div style=""display:inline-block;width:56px;height:56px;border-radius:50%;background:#D01417;border:4px solid #000;""></div>"
                            : $@"<img src=""{logoUrl}"" alt=""{safeApp}"" width=""120"" style=""max-width:160px; height:auto; display:inline-block;"">")}
                        </td>
                    </tr>

                    <!-- Card -->
                    <tr>
                        <td style=""padding:24px 20px 0 20px;"">
                        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%""
                                style=""background:#1C1C1C; border-radius:12px; border:1px solid #2a2a2a;"">
                            <tr>
                            <td style=""padding:28px 24px 12px 24px; text-align:left;"">
                                <h1 style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:22px; line-height:1.35; color:#ffffff;"">
                                Reset your password
                                </h1>
                                <div style=""height:10px;""></div>
                                <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:14px; line-height:1.7; color:#cfcfcf;"">
                                Hi <strong>{System.Net.WebUtility.HtmlEncode(userDisplayName)}</strong>,<br>
                                Use the one-time code below to reset your account password. This code expires in {expiresInMinutes} minutes.
                                </p>
                            </td>
                            </tr>

                            <!-- OTP block -->
                            <tr>
                            <td style=""padding:16px 24px 4px 24px; text-align:center;"">
                                <div style=""display:inline-block; background:#121212; border:1px solid #1f1f1f; border-radius:10px; padding:16px 22px;"">
                                <code style=""font-family:Consolas, 'Courier New', monospace; font-size:28px; letter-spacing:6px; color:#ffffff;"">
                                    {System.Net.WebUtility.HtmlEncode(otpCode)}
                                </code>
                                </div>
                            </td>
                            </tr>

                            <tr>
                            <td style=""padding:8px 24px 24px 24px; text-align:center;"">
                                <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:12px; line-height:1.7; color:#9e9e9e;"">
                                For security, never share this code with anyone. If you didn't request this, you can safely ignore this email.
                                </p>
                            </td>
                            </tr>
                        </table>
                        </td>
                    </tr>

                    <!-- Footer helper -->
                    <tr>
                        <td style=""padding:16px 20px 0 20px;"">
                        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%""
                                style=""background:#121212; border-radius:12px; border:1px solid #1f1f1f;"">
                            <tr>
                            <td style=""padding:16px 20px;"">
                                <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:12px; line-height:1.6; color:#cfcfcf;"">
                                Tip: If you can't copy the code, try typing it manually.
                                </p>
                            </td>
                            </tr>
                        </table>
                        </td>
                    </tr>

                    <!-- Footer -->
                    <tr>
                        <td style=""padding:24px 20px 40px 20px; text-align:center;"">
                        <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:12px; line-height:1.8; color:#8a8a8a;"">
                            © {year} {safeApp}. All rights reserved.
                            {(string.IsNullOrWhiteSpace(supportEmail) ? "" : $@"<br>Need help? <a href=""mailto:{supportEmail}"" style=""color:#9ecbff; text-decoration:underline;"">{supportEmail}</a>")}
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