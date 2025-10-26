using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpsAPI.Services
{
    public class EmailConfirmedChangeTemplate
    {
        public static string EmailChangeConfirmedHtml(
            string appName,
            string? logoUrl = null,
            string? appDeepLink = null)
        {
            var safeApp = string.IsNullOrWhiteSpace(appName) ? "Your Corps" : appName;

            return @$"<!doctype html>
        <html lang=""en"">
        <head>
        <meta charset=""utf-8"">
        <meta name=""viewport"" content=""width=device-width,initial-scale=1"">
        <title>{safeApp} | Email Updated</title>
        <style>
        :root {{
        --bg:#000; 
        --card:#1c1c1c;
        --text:#fff;
        --muted:#cfcfcf;
        --muter:#9b9b9b;
        --accent:#d01417;
        --ring:#000;
        --radius:14px;
        }}
        html,body {{
        height:100%;
        margin:0;
        background:var(--bg);
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        -webkit-font-smoothing:antialiased;
        -moz-osx-font-smoothing:grayscale;
        color:var(--text);
        }}
        .wrap {{
        min-height:100%;
        display:flex;
        align-items:center;
        justify-content:center;
        padding:24px;
        }}
        .card {{
        width:100%;
        max-width:640px;
        background:var(--card);
        border-radius:var(--radius);
        border:1px solid rgba(255,255,255,.08);
        box-shadow:0 10px 30px rgba(0,0,0,.35);
        padding:28px 24px;
        }}
        .brand {{
        display:flex;
        align-items:center;
        gap:12px;
        margin-bottom:10px;
        }}
        .brand img {{
        height:48px;
        display:block;
        }}
        .title {{
        font-size:22px;
        font-weight:800;
        margin:6px 0 6px 0;
        letter-spacing:.2px;
        }}
        .lead {{
        color:var(--muted);
        line-height:1.55;
        margin:0 0 18px 0;
        }}
        .cta {{
        margin:18px 0 6px 0;
        }}
        .btn {{
        display:inline-block;
        background:var(--accent);
        color:#fff;
        text-decoration:none;
        padding:12px 18px;
        border-radius:12px;
        border:3px solid var(--ring);
        font-weight:800;
        letter-spacing:.2px;
        }}
        .sub {{
        color:var(--muter);
        font-size:12px;
        margin-top:16px;
        line-height:1.5;
        }}
        .hr {{
        height:1px;
        background:rgba(255,255,255,.08);
        margin:18px 0;
        border:0;
        }}
        @media (max-width:420px) {{
        .card {{ padding:22px 18px; }}
        .title {{ font-size:20px; }}
        .brand img {{ height:40px; }}
        }}
        </style>
        </head>
        <body>
        <div class=""wrap"">
        <div class=""card"">
            <div class=""brand"">
            {(string.IsNullOrWhiteSpace(logoUrl) ? "" : $@"<img src=""{logoUrl}"" alt=""{safeApp}"" />")}
            </div>

            <div class=""title"">Email updated</div>
            <p class=""lead"">
            Your account email has been changed successfully. Re-Login with NEW email to complete change.
            </p>

            {(string.IsNullOrWhiteSpace(appDeepLink) ? "" : $@"<div class=""cta"">
            <a class=""btn"" href=""{appDeepLink}"">Open the app</a>
            </div>")}

            <hr class=""hr"" />

            <p class=""sub"">
            If you didn't make this change, please contact support immediately and reset your password.
            </p>
        </div>
        </div>
        </body>
        </html>";
        }

        
    }
}