namespace Api.Services;

/// <summary>
/// Shared HTML email templates with consistent branding and signature.
/// All emails use inline CSS for maximum email client compatibility.
/// </summary>
public static class EmailTemplates
{
    private static string Wrap(string brandName, string content) => $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""></head>
<body style=""margin:0;padding:0;background-color:#f4f4f7;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f4f4f7;padding:32px 16px;"">
    <tr><td align=""center"">
      <table width=""100%"" style=""max-width:560px;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.06);"">
        <!-- Header -->
        <tr>
          <td style=""background:linear-gradient(135deg,#4f46e5,#7c3aed);padding:28px 32px;text-align:center;"">
            <span style=""font-size:22px;color:#ffffff;font-weight:700;letter-spacing:0.5px;"">✦ {brandName}</span>
          </td>
        </tr>
        <!-- Content -->
        <tr>
          <td style=""padding:32px;"">
            {content}
          </td>
        </tr>
        <!-- Signature / Footer -->
        <tr>
          <td style=""padding:0 32px 28px;border-top:1px solid #e5e7eb;"">
            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""padding-top:20px;"">
              <tr>
                <td>
                  <p style=""margin:0 0 4px;font-size:14px;font-weight:600;color:#1f2937;"">The {brandName} Team</p>
                  <p style=""margin:0;font-size:12px;color:#9ca3af;"">Where Great Events Come to Life</p>
                </td>
              </tr>
            </table>
          </td>
        </tr>
        <!-- Bottom bar -->
        <tr>
          <td style=""background:#f9fafb;padding:16px 32px;text-align:center;"">
            <p style=""margin:0;font-size:11px;color:#9ca3af;"">
              This is an automated message from {brandName}. Please do not reply directly to this email.
            </p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";

    public static string MagicLink(string brandName, string verifyUrl, int expiryMinutes) =>
        Wrap(brandName, $@"
            <h2 style=""margin:0 0 8px;font-size:22px;color:#1f2937;font-weight:700;"">Sign in to {brandName}</h2>
            <p style=""margin:0 0 24px;font-size:15px;color:#6b7280;line-height:1.5;"">
              Click the button below to securely log in. No password needed.
            </p>
            <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
              <tr><td align=""center"" style=""padding:8px 0 24px;"">
                <a href=""{verifyUrl}""
                   style=""display:inline-block;padding:14px 36px;background:linear-gradient(135deg,#4f46e5,#7c3aed);color:#ffffff;font-size:16px;font-weight:600;text-decoration:none;border-radius:99px;"">
                  Sign In
                </a>
              </td></tr>
            </table>
            <p style=""margin:0 0 8px;font-size:13px;color:#9ca3af;line-height:1.5;"">
              Or copy and paste this link into your browser:
            </p>
            <p style=""margin:0 0 20px;font-size:12px;color:#6366f1;word-break:break-all;"">{verifyUrl}</p>
            <div style=""background:#fef3c7;border-left:3px solid #f59e0b;padding:12px 16px;border-radius:6px;"">
              <p style=""margin:0;font-size:13px;color:#92400e;"">
                This link expires in <strong>{expiryMinutes} minutes</strong>. If you didn't request this, you can safely ignore this email.
              </p>
            </div>");

    public static string BookingConfirmed(
        string brandName, string firstName, string bookingNumber,
        string eventTitle, string totalFormatted, string checkinLink) =>
        Wrap(brandName, $@"
            <h2 style=""margin:0 0 8px;font-size:22px;color:#1f2937;font-weight:700;"">Booking Confirmed!</h2>
            <p style=""margin:0 0 24px;font-size:15px;color:#6b7280;line-height:1.5;"">
              Hi {firstName}, your booking is all set. Here are the details:
            </p>
            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f9fafb;border-radius:8px;padding:20px;margin-bottom:24px;"">
              <tr><td>
                <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                  <tr>
                    <td style=""padding:6px 0;font-size:13px;color:#9ca3af;width:120px;"">Booking #</td>
                    <td style=""padding:6px 0;font-size:14px;color:#1f2937;font-weight:600;"">{bookingNumber}</td>
                  </tr>
                  <tr>
                    <td style=""padding:6px 0;font-size:13px;color:#9ca3af;"">Event</td>
                    <td style=""padding:6px 0;font-size:14px;color:#1f2937;font-weight:600;"">{eventTitle}</td>
                  </tr>
                  <tr>
                    <td style=""padding:6px 0;font-size:13px;color:#9ca3af;"">Total</td>
                    <td style=""padding:6px 0;font-size:14px;color:#1f2937;font-weight:600;"">{totalFormatted}</td>
                  </tr>
                </table>
              </td></tr>
            </table>
            <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
              <tr><td align=""center"" style=""padding:0 0 20px;"">
                <a href=""{checkinLink}""
                   style=""display:inline-block;padding:14px 36px;background:linear-gradient(135deg,#22c55e,#16a34a);color:#ffffff;font-size:16px;font-weight:600;text-decoration:none;border-radius:99px;"">
                  View Check-In QR Code
                </a>
              </td></tr>
            </table>
            <p style=""margin:0;font-size:13px;color:#9ca3af;text-align:center;"">
              Show your QR code at the venue for check-in.
            </p>");

    public static string TicketInvite(
        string brandName, string guestName, string inviterName,
        string eventTitle, string eventDate, int seatNumber, string claimUrl) =>
        Wrap(brandName, $@"
            <h2 style=""margin:0 0 8px;font-size:22px;color:#1f2937;font-weight:700;"">You're Invited!</h2>
            <p style=""margin:0 0 24px;font-size:15px;color:#6b7280;line-height:1.5;"">
              Hi{(string.IsNullOrEmpty(guestName) ? "" : $" {guestName}")}, <strong>{inviterName}</strong> has invited you to an event!
            </p>
            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f9fafb;border-radius:8px;padding:20px;margin-bottom:24px;"">
              <tr><td>
                <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
                  <tr>
                    <td style=""padding:6px 0;font-size:13px;color:#9ca3af;width:120px;"">Event</td>
                    <td style=""padding:6px 0;font-size:14px;color:#1f2937;font-weight:600;"">{eventTitle}</td>
                  </tr>
                  <tr>
                    <td style=""padding:6px 0;font-size:13px;color:#9ca3af;"">Date</td>
                    <td style=""padding:6px 0;font-size:14px;color:#1f2937;font-weight:600;"">{eventDate}</td>
                  </tr>
                  <tr>
                    <td style=""padding:6px 0;font-size:13px;color:#9ca3af;"">Seat</td>
                    <td style=""padding:6px 0;font-size:14px;color:#1f2937;font-weight:600;"">#{seatNumber}</td>
                  </tr>
                </table>
              </td></tr>
            </table>
            <table width=""100%"" cellpadding=""0"" cellspacing=""0"">
              <tr><td align=""center"" style=""padding:0 0 20px;"">
                <a href=""{claimUrl}""
                   style=""display:inline-block;padding:14px 36px;background:linear-gradient(135deg,#4f46e5,#7c3aed);color:#ffffff;font-size:16px;font-weight:600;text-decoration:none;border-radius:99px;"">
                  Claim Your Ticket
                </a>
              </td></tr>
            </table>
            <div style=""background:#fef3c7;border-left:3px solid #f59e0b;padding:12px 16px;border-radius:6px;"">
              <p style=""margin:0;font-size:13px;color:#92400e;"">
                This invitation expires in <strong>7 days</strong>.
              </p>
            </div>");
}
