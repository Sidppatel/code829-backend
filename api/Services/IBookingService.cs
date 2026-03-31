using Contracts.DTOs.Bookings;

namespace Api.Services;

public interface IBookingService
{
    Task<BookingDto> CreateAsync(Guid userId, CreateBookingRequest request);
    Task<BookingDto> ConfirmPaymentAsync(Guid bookingId, Guid userId);
    Task<BookingDto> CancelAsync(Guid bookingId, Guid userId);
    Task<BookingDto> RefundAsync(Guid bookingId);
    Task<BookingDto?> GetByIdAsync(Guid bookingId);
    Task<byte[]> GetQrImageAsync(Guid bookingId, Guid userId);
}
