using Contracts.DTOs;
using Db;
using Db.Entities.Views;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class FinancialService(EventPlatformDbContext context) : IFinancialService
{
    public async Task<PagedResponse<StripeTransactionView>> GetTransactionsAsync(
        Guid? organizationId,
        string? search,
        int page,
        int pageSize,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.StripeTransactionViews.AsNoTracking();

        if (organizationId.HasValue)
            query = query.Where(t => t.OrganizationId == organizationId.Value);

        if (fromDate.HasValue)
            query = query.Where(t => t.PaidAt >= fromDate.Value || t.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.PaidAt <= toDate.Value || t.CreatedAt <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(t =>
                t.PaymentIntentId.ToLower().Contains(term) ||
                t.PurchaseNumber.ToLower().Contains(term) ||
                t.UserEmail.ToLower().Contains(term) ||
                t.EventTitle.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResponse<StripeTransactionView>(items, totalCount, page, pageSize);
    }
}
