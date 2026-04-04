namespace Api.Services;

public static class EmailTemplates
{
    private static string Sign(string brandName) =>
        $"\n\n— The {brandName} Team\nThis is an automated message. This mailbox is not monitored.";

    public static string MagicLink(string brandName, string verifyUrl, int expiryMinutes) =>
        $"Sign in to {brandName}\n\n" +
        $"Click the link below to log in. No password needed.\n\n" +
        $"{verifyUrl}\n\n" +
        $"This link expires in {expiryMinutes} minutes. If you didn't request this, you can safely ignore this email." +
        Sign(brandName);

    public static string BookingConfirmed(
        string brandName, string firstName, string bookingNumber,
        string eventTitle, string totalFormatted, string checkinLink) =>
        $"Booking Confirmed!\n\n" +
        $"Hi {firstName}, your booking is all set.\n\n" +
        $"Booking #: {bookingNumber}\n" +
        $"Event: {eventTitle}\n" +
        $"Total: {totalFormatted}\n\n" +
        $"View your check-in QR code: {checkinLink}" +
        Sign(brandName);

    public static string TicketInvite(
        string brandName, string guestName, string inviterName,
        string eventTitle, string eventDate, int seatNumber, string claimUrl) =>
        $"You're Invited!\n\n" +
        $"Hi{(string.IsNullOrEmpty(guestName) ? "" : $" {guestName}")}, {inviterName} has invited you to an event!\n\n" +
        $"Event: {eventTitle}\n" +
        $"Date: {eventDate}\n" +
        $"Seat: #{seatNumber}\n\n" +
        $"Claim your ticket: {claimUrl}\n\n" +
        $"This invitation expires in 7 days." +
        Sign(brandName);
}
