using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpsAPI.Services
{
    public static class EmailTemplates
    {
        /// <summary>
        /// Generates a dark, on-brand verification email (inline CSS, table layout).
        /// </summary>
        /// <param name="appName">e.g., "Your Corps"</param>
        /// <param name="confirmationUrl">Full verify link</param>
        /// <param name="logoUrl">Public logo URL (optional but recommended) https://static.wixstatic.com/media/ff8734_0e11ba81866b4340a9ba8d912f1a5423~mv2.png/v1/fill/w_542,h_112,al_c,q_85,usm_0.66_1.00_0.01,enc_avif,quality_auto/YOURCORPS_THIN%20copy.png</param>
        /// <param name="supportEmail">Support email for footer (optional)</param>
        public static string ConfirmEmailHtml(
            string appName,
            string confirmationUrl,
            string? logoUrl = null,
            string? supportEmail = null)
        {
            var year = DateTime.UtcNow.Year;
            var safeApp = string.IsNullOrWhiteSpace(appName) ? "Your Corps" : appName;

            // Basic preheader text for inbox previews
            const string preheader = "Confirm your email to finish setting up your account.";

            return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>{safeApp} — Verify your email</title>
</head>
<body style=""margin:0; padding:0; background:#000000; color:#ffffff;"">
  <!-- Preheader (hidden in most clients) -->
  <div style=""display:none; font-size:1px; color:#000000; line-height:1px; max-height:0; max-width:0; opacity:0; overflow:hidden;"">
    {preheader}
  </div>

  <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" style=""background:#000000;"">
    <tr>
      <td align=""center"">
        <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""600"" style=""width:600px; max-width:100%;"">
          <tr>
            <td style=""padding:32px 20px 0 20px; text-align:center;"">
              {(string.IsNullOrWhiteSpace(logoUrl) ? $@"<div style=""display:inline-block;width:56px;height:56px;border-radius:50%;background:#D01417;border:4px solid #000;"">
                  </div>"
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
                      Verify your email
                    </h1>
                    <div style=""height:10px;""></div>
                    <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:14px; line-height:1.6; color:#cfcfcf;"">
                      Thanks for joining <strong>{safeApp}</strong>! Please confirm your email to finish setting up your account.
                    </p>
                  </td>
                </tr>

                <tr>
                  <td style=""padding:24px 24px 8px 24px; text-align:center;"">
                    <a href=""{confirmationUrl}"" target=""_blank""
                       style=""display:inline-block; font-family:Segoe UI, Arial, sans-serif; text-decoration:none;
                              background:#D01417; color:#ffffff; padding:12px 22px; border-radius:8px; 
                              font-weight:700; font-size:14px;"">
                      Confirm email
                    </a>
                  </td>
                </tr>

                <tr>
                  <td style=""padding:12px 24px 28px 24px; text-align:center;"">
                    <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:12px; line-height:1.6; color:#9e9e9e;"">
                      Or copy &amp; paste this link into your browser:
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
                      Didn’t create an account? You can safely ignore this message.
                    </p>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <tr>
            <td style=""padding:24px 20px 40px 20px; text-align:center;"">
              <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:12px; line-height:1.8; color:#8a8a8a;"">
                © {year} {safeApp}. All rights reserved.<br>
                {(string.IsNullOrWhiteSpace(supportEmail) ? "" : $@"Need help? 
                  <a href=""mailto:{supportEmail}"" style=""color:#9ecbff; text-decoration:underline;"">{supportEmail}</a>")}
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
