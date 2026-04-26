using Contracts.DTOs;
using Db.Entities.Views;

namespace Api.Services;

public interface IFinancialService
{
    Task<PagedResponse<StripeTransactionView>> GetTransactionsAsync(
        Guid? organizationId,
        string? search,
        int page,
        int pageSize,
        DateTime? fromDate = null,
        DateTime? toDate = null);
}
