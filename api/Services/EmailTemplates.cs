namespace Api.Services;

public static class EmailTemplates
{
    private const string BrandColor = "#6366f1";
    private const string BgColor = "#f4f4f5";

    private static string Wrap(string brandName, string content) =>
        $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"></head>
        <body style="margin:0;padding:0;background:{BgColor};font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
        <table width="100%" cellpadding="0" cellspacing="0" style="background:{BgColor};padding:40px 20px;">
        <tr><td align="center">
        <table width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,0.1);">
        <tr><td style="background:{BrandColor};padding:24px 32px;text-align:center;">
          <span style="color:#ffffff;font-size:20px;font-weight:700;letter-spacing:-0.5px;">{brandName}</span>
        </td></tr>
        <tr><td style="padding:32px;">
          {content}
        </td></tr>
        <tr><td style="padding:16px 32px 24px;text-align:center;border-top:1px solid #e4e4e7;">
          <p style="margin:0;color:#a1a1aa;font-size:12px;">This is an automated message from {brandName}. This mailbox is not monitored.</p>
        </td></tr>
        </table>
        </td></tr>
        </table>
        </body>
        </html>
        """;

    private static string Button(string text, string url) =>
        $"""
        <table cellpadding="0" cellspacing="0" style="margin:24px 0;">
        <tr><td style="background:{BrandColor};border-radius:8px;padding:14px 32px;text-align:center;">
          <a href="{url}" style="color:#ffffff;text-decoration:none;font-size:16px;font-weight:600;display:inline-block;">{text}</a>
        </td></tr>
        </table>
        """;

    private static string InfoRow(string label, string value) =>
        $"""
        <tr>
          <td style="padding:8px 12px;color:#71717a;font-size:14px;white-space:nowrap;">{label}</td>
          <td style="padding:8px 12px;font-size:14px;font-weight:600;color:#18181b;">{value}</td>
        </tr>
        """;

    private static string InfoTable(string rows) =>
        $"""
        <table cellpadding="0" cellspacing="0" style="margin:20px 0;width:100%;border:1px solid #e4e4e7;border-radius:8px;border-collapse:separate;">
        {rows}
        </table>
        """;

    public static string MagicLink(string brandName, string verifyUrl, int expiryMinutes) =>
        Wrap(brandName,
            $"""
            <h2 style="margin:0 0 16px;font-size:22px;font-weight:700;color:#18181b;">Sign in to {brandName}</h2>
            <p style="margin:0 0 8px;font-size:15px;color:#3f3f46;line-height:1.6;">Click the button below to log in. No password needed.</p>
            {Button("Sign In", verifyUrl)}
            <p style="margin:0;color:#71717a;font-size:13px;line-height:1.5;">This link expires in {expiryMinutes} minutes. If you didn't request this, you can safely ignore this email.</p>
            """);

    public static string PurchaseConfirmed(
        string brandName, string firstName, string purchaseNumber,
        string eventTitle, int ticketCount, int totalCents, int taxAmountCents, int totalChargedCents, string checkinLink) =>
        Wrap(brandName,
            $"""
            <h2 style="margin:0 0 16px;font-size:22px;font-weight:700;color:#18181b;">Purchase Confirmed!</h2>
            <p style="margin:0 0 20px;font-size:15px;color:#3f3f46;line-height:1.6;">Hi {firstName}, your purchase is all set.</p>
            {InfoTable(
                InfoRow("Booking Number", purchaseNumber) +
                InfoRow("Event", eventTitle) +
                InfoRow("Number of Tickets", ticketCount.ToString()) +
                InfoRow("Total", $"${totalCents / 100.0:F2}") +
                InfoRow("Tax", $"${taxAmountCents / 100.0:F2}") +
                InfoRow("Total Paid", $"${totalChargedCents / 100.0:F2}")
            )}
            {Button("View Check-in QR Code", checkinLink)}
            """);

    public static string TicketInvite(
        string brandName, string guestName, string inviterName,
        string eventTitle, string eventDate, int seatNumber, string claimUrl) =>
        Wrap(brandName,
            $"""
            <h2 style="margin:0 0 16px;font-size:22px;font-weight:700;color:#18181b;">You're Invited!</h2>
            <p style="margin:0 0 20px;font-size:15px;color:#3f3f46;line-height:1.6;">Hi{(string.IsNullOrEmpty(guestName) ? "" : $" {guestName}")}, <strong>{inviterName}</strong> has invited you to an event!</p>
            {InfoTable(
                InfoRow("Event", eventTitle) +
                InfoRow("Date", eventDate) +
                InfoRow("Seat", $"#{seatNumber}")
            )}
            {Button("Claim Your Ticket", claimUrl)}
            <p style="margin:0;color:#71717a;font-size:13px;line-height:1.5;">This invitation expires in 7 days.</p>
            """);

    public static string Invitation(string brandName, string inviterName, string role, string signupUrl, int expiryDays) =>
        Wrap(brandName,
            $"""
            <h2 style="margin:0 0 16px;font-size:22px;font-weight:700;color:#18181b;">You've been invited to {brandName}</h2>
            <p style="margin:0 0 8px;font-size:15px;color:#3f3f46;line-height:1.6;"><strong>{inviterName}</strong> has invited you to join {brandName} as {(role == "Admin" ? "an" : "a")} <strong>{role}</strong>.</p>
            <p style="margin:0 0 8px;font-size:15px;color:#3f3f46;line-height:1.6;">Click the button below to create your account:</p>
            {Button("Create Account", signupUrl)}
            <p style="margin:0;color:#71717a;font-size:13px;line-height:1.5;">This invitation expires in {expiryDays} days. If you weren't expecting this, you can safely ignore this email.</p>
            """);

    public static string EmailVerification(string brandName, string firstName, string verifyUrl, int expiryMinutes) =>
        Wrap(brandName,
            $"""
            <h2 style="margin:0 0 16px;font-size:22px;font-weight:700;color:#18181b;">Confirm your email</h2>
            <p style="margin:0 0 8px;font-size:15px;color:#3f3f46;line-height:1.6;">Hi{(string.IsNullOrEmpty(firstName) ? "" : $" {firstName}")}, thanks for signing up for {brandName}. Click below to verify your email and activate your account.</p>
            {Button("Verify Email", verifyUrl)}
            <p style="margin:0;color:#71717a;font-size:13px;line-height:1.5;">This link expires in {expiryMinutes} minutes. If you didn't sign up, you can safely ignore this email.</p>
            """);

    public static string PasswordReset(string brandName, string resetUrl, int expiryMinutes) =>
        Wrap(brandName,
            $"""
            <h2 style="margin:0 0 16px;font-size:22px;font-weight:700;color:#18181b;">Reset Your Password</h2>
            <p style="margin:0 0 8px;font-size:15px;color:#3f3f46;line-height:1.6;">We received a request to reset your password. Click the button below to choose a new one:</p>
            {Button("Reset Password", resetUrl)}
            <p style="margin:0;color:#71717a;font-size:13px;line-height:1.5;">This link expires in {expiryMinutes} minutes. If you didn't request this, your password will remain unchanged.</p>
            """);

    public static string OnboardingLinkEmail(
        string brandName, string firstName, string organizationName, string onboardingUrl, int expiryMinutes) =>
        Wrap(brandName,
            $"""
            <h2 style="margin:0 0 16px;font-size:22px;font-weight:700;color:#18181b;">Finish setting up payouts for {organizationName}</h2>
            <p style="margin:0 0 8px;font-size:15px;color:#3f3f46;line-height:1.6;">Hi{(string.IsNullOrEmpty(firstName) ? "" : $" {firstName}")}, the {brandName} team has set up a Stripe Connect account for <strong>{organizationName}</strong>.</p>
            <p style="margin:0 0 8px;font-size:15px;color:#3f3f46;line-height:1.6;">Click the button below to complete the onboarding form (legal name, address, bank account, etc.). Stripe will collect everything and send you back to {brandName} when you're done.</p>
            {Button("Complete Stripe Onboarding", onboardingUrl)}
            <p style="margin:0;color:#71717a;font-size:13px;line-height:1.5;">This link expires in about {expiryMinutes} minutes - request a new one from the developer if it lapses. Your data goes directly to Stripe; we never see your bank credentials.</p>
            """);
}
