using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpsAPI.Services
{
    public static class EmailEventTemplate
    {
        /// <summary>
        /// On-brand event cancellation email (dark theme, inline CSS, mobile-friendly).
        /// </summary>
        public static string EventCancellationHtml(
            string appName,
            string? logoUrl,
            string? supportEmail,
            string userDisplayName,
            string eventDatePretty,
            string timeRangePretty,
            string locationName,
            string addressPretty,
            string sessionType,
            string? organiserMessage)
        {
            var year = DateTime.UtcNow.Year;
            var safeApp = string.IsNullOrWhiteSpace(appName) ? "Your Corps" : appName;

            var preheader = $"Update: your {safeApp} event has been cancelled.";

            var organiserBlock = string.IsNullOrWhiteSpace(organiserMessage)
                ? ""
                : $@"
                <tr>
                  <td style=""padding:0 24px 0 24px;"">
                    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%""
                           style=""background:#121212; border:1px solid #1f1f1f; border-radius:10px;"">
                      <tr>
                        <td style=""padding:16px 18px;"">
                          <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:14px; line-height:1.7; color:#cfcfcf;"">
                            <strong>Message from the organiser:</strong><br>
                            {System.Net.WebUtility.HtmlEncode(organiserMessage)}
                          </p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
                <tr><td style=""height:16px;""></td></tr>";

            return $@"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
            <meta charset=""UTF-8"">
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
            <title>{safeApp} — Event cancelled</title>
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
                                Event cancelled
                                </h1>
                                <div style=""height:10px;""></div>
                                <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:14px; line-height:1.7; color:#cfcfcf;"">
                                Hi <strong>{System.Net.WebUtility.HtmlEncode(userDisplayName)}</strong>,<br>
                                Unfortunately, the event below has been cancelled. We’re sorry for the inconvenience.
                                </p>
                            </td>
                            </tr>

                            <!-- Details -->
                            <tr>
                            <td style=""padding:8px 24px 0 24px;"">
                                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"" 
                                    style=""background:#121212; border:1px solid #1f1f1f; border-radius:10px;"">
                                <tr>
                                    <td style=""padding:16px 18px;"">
                                    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" width=""100%"">
                                        <tr>
                                        <td style=""font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#eaeaea; padding:6px 0; width:140px;"">Date</td>
                                        <td style=""font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#cfcfcf; padding:6px 0;"">{eventDatePretty}</td>
                                        </tr>
                                        <tr>
                                        <td style=""font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#eaeaea; padding:6px 0;"">Time</td>
                                        <td style=""font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#cfcfcf; padding:6px 0;"">{timeRangePretty}</td>
                                        </tr>
                                        <tr>
                                        <td style=""font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#eaeaea; padding:6px 0;"">Location</td>
                                        <td style=""font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#cfcfcf; padding:6px 0;"">
                                            <strong>{System.Net.WebUtility.HtmlEncode(locationName)}</strong><br>
                                            <span style=""color:#9e9e9e;"">{System.Net.WebUtility.HtmlEncode(addressPretty)}</span>
                                        </td>
                                        </tr>
                                        <tr>
                                        <td style=""font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#eaeaea; padding:6px 0;"">Session</td>
                                        <td style=""font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#cfcfcf; padding:6px 0;"">{System.Net.WebUtility.HtmlEncode(sessionType)}</td>
                                        </tr>
                                    </table>
                                    </td>
                                </tr>
                                </table>
                            </td>
                            </tr>

                            <!-- Optional organiser message -->
                            {organiserBlock}

                            <!-- Closing -->
                            <tr>
                            <td style=""padding:8px 24px 24px 24px; text-align:left;"">
                                <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:13px; line-height:1.7; color:#cfcfcf;"">
                                Any questions or need help with future bookings?
                                {(string.IsNullOrWhiteSpace(supportEmail) ? "" : $@" Contact us at 
                                    <a href=""mailto:{supportEmail}"" style=""color:#9ecbff; text-decoration:underline;"">{supportEmail}</a>.")}
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