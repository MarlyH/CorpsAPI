using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpsAPI.Services
{
    public static class EmailBookingTemplate
    {
         /// <summary>
        /// On-brand booking confirmation (dark theme, inline CSS, mobile-friendly).
        /// Uses table layout for broad client compatibility.
        /// </summary>
        /// <param name="appName">"Your Corps"</param>
        /// <param name="logoUrl">Brand logo URL (same as verify email)</param>
        /// <param name="supportEmail">Footer support email</param>
        /// <param name="userDisplayName">e.g., "Barlow"</param>
        /// <param name="sessionType">e.g., "Adults"</param>
        /// <param name="eventDatePretty">e.g., "Saturday, October 18, 2025"</param>
        /// <param name="timeRangePretty">e.g., "6:00 PM – 9:00 PM"</param>
        /// <param name="locationName">e.g., "Your Corps HQ"</param>
        /// <param name="addressPretty">e.g., "123 Example St, Christchurch"</param>
        /// <param name="seatText">e.g., "Seat 14"</param>
        /// <param name="isChildBooking">true if this was booked for a child</param>
        /// <param name="childNameOrNull">Child name if any (optional)</param>
        /// <param name="qrCid">Content-ID used for the inline QR image</param>
        /// <param name="viewInAppUrl">Optional deep link or web URL to the ticket</param>
        public static string BookingConfirmationHtml(
            string appName,
            string? logoUrl,
            string? supportEmail,
            string userDisplayName,
            string sessionType,
            string eventDatePretty,
            string timeRangePretty,
            string locationName,
            string addressPretty,
            string seatText,
            bool isChildBooking,
            string? childNameOrNull,
            string qrCid,
            string? viewInAppUrl = null)
        {
            var year = DateTime.UtcNow.Year;
            var safeApp = string.IsNullOrWhiteSpace(appName) ? "Your Corps" : appName;

            var preheader = $"{safeApp} booking confirmed for {eventDatePretty} · {timeRangePretty} · {locationName}";
            var forLine = isChildBooking && !string.IsNullOrWhiteSpace(childNameOrNull)
                ? $"for <strong>{System.Net.WebUtility.HtmlEncode(childNameOrNull)}</strong>"
                : "for <strong>you</strong>";

            // Button (optional)
            var ctaButton = string.IsNullOrWhiteSpace(viewInAppUrl)
                ? ""
                : $@"
                <tr>
                  <td style=""padding:0 24px 20px 24px; text-align:center;"">
                    <a href=""{viewInAppUrl}"" target=""_blank""
                       style=""display:inline-block; font-family:Segoe UI, Arial, sans-serif; text-decoration:none;
                              background:#D01417; color:#ffffff; padding:12px 22px; border-radius:8px; 
                              font-weight:700; font-size:14px;"">
                      View ticket in app
                    </a>
                  </td>
                </tr>";

            return $@"
                <!DOCTYPE html>
                <html lang=""en"">
                <head>
                <meta charset=""UTF-8"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <title>{safeApp} — Booking confirmed</title>
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
                                    Booking confirmed
                                    </h1>
                                    <div style=""height:10px;""></div>
                                    <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:14px; line-height:1.7; color:#cfcfcf;"">
                                    Hi <strong>{System.Net.WebUtility.HtmlEncode(userDisplayName)}</strong>,<br>
                                    Your reservation has been confirmed {forLine}.
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
                                            <tr>
                                            <td style=""font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#eaeaea; padding:6px 0;"">Seat</td>
                                            <td style=""font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#cfcfcf; padding:6px 0;"">{System.Net.WebUtility.HtmlEncode(seatText)}</td>
                                            </tr>
                                        </table>
                                        </td>
                                    </tr>
                                    </table>
                                </td>
                                </tr>

                                <!-- QR -->
                                <tr>
                                <td style=""padding:20px 24px 0 24px; text-align:center;"">
                                    <p style=""margin:0 0 8px 0; font-family:Segoe UI, Arial, sans-serif; font-size:14px; color:#cfcfcf;"">
                                    Present this QR at the entrance to check in / out:
                                    </p>
                                    <img src=""cid:{qrCid}"" alt=""Your Corps ticket QR"" width=""200"" height=""200""
                                        style=""display:inline-block; width:200px; height:200px; border-radius:8px; border:1px solid #2a2a2a;"">
                                </td>
                                </tr>

                                {ctaButton}

                                <tr>
                                <td style=""padding:8px 24px 24px 24px; text-align:center;"">
                                    <p style=""margin:0; font-family:Segoe UI, Arial, sans-serif; font-size:12px; line-height:1.7; color:#9e9e9e;"">
                                    Keep this email handy. You can also view your ticket anytime in the app.
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
                                    If you need to cancel or change your booking, please do so in the app.
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