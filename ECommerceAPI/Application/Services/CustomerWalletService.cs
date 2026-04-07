using ECommerceAPI.Application.DTOs.Customer;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class CustomerWalletService : ICustomerWalletService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CustomerWalletService> _logger;

    public CustomerWalletService(ApplicationDbContext context, ILogger<CustomerWalletService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CustomerWalletDto?> GetWalletAsync(Guid customerId)
    {
        var wallet = await _context.CustomerWallets
            .FirstOrDefaultAsync(w => w.CustomerId == customerId);

        if (wallet == null)
            return new CustomerWalletDto { AvailableBalance = 0, UpdatedAt = DateTime.UtcNow };

        var totalRefunded = await _context.CustomerWalletLedgers
            .Where(l => l.WalletId == wallet.Id && l.Type == "refund")
            .SumAsync(l => l.Amount);

        var totalWithdrawn = await _context.CustomerWalletLedgers
            .Where(l => l.WalletId == wallet.Id && l.Type == "withdrawal")
            .SumAsync(l => (decimal?)l.Amount) ?? 0;

        return new CustomerWalletDto
        {
            Id = wallet.Id,
            AvailableBalance = wallet.AvailableBalance,
            TotalRefunded = totalRefunded,
            TotalWithdrawn = totalWithdrawn,
            UpdatedAt = wallet.UpdatedAt
        };
    }

    public async Task<CustomerWalletLedgerResponseDto> GetTransactionsAsync(Guid customerId, int page, int pageSize)
    {
        var wallet = await _context.CustomerWallets
            .FirstOrDefaultAsync(w => w.CustomerId == customerId);

        if (wallet == null)
            return new CustomerWalletLedgerResponseDto { Success = true, Transactions = new(), TotalCount = 0, Page = page, PageSize = pageSize };

        var query = _context.CustomerWalletLedgers
            .Where(l => l.WalletId == wallet.Id)
            .OrderByDescending(l => l.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new CustomerWalletLedgerItemDto
            {
                Id = l.Id,
                Type = l.Type,
                Amount = l.Amount,
                ReferenceType = l.ReferenceType,
                ReferenceId = l.ReferenceId,
                Note = l.Note,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();

        return new CustomerWalletLedgerResponseDto
        {
            Success = true,
            Transactions = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task CreditRefundAsync(Guid customerId, decimal amount, string referenceType, Guid referenceId, string note)
    {
        if (amount <= 0) return;

        try
        {
            var wallet = await _context.CustomerWallets
                .FirstOrDefaultAsync(w => w.CustomerId == customerId);

            if (wallet == null)
            {
                wallet = new CustomerWallet
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customerId,
                    AvailableBalance = amount,
                    Currency = "VND",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.CustomerWallets.Add(wallet);
            }
            else
            {
                wallet.AvailableBalance += amount;
                wallet.UpdatedAt = DateTime.UtcNow;
            }

            _context.CustomerWalletLedgers.Add(new CustomerWalletLedger
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                Type = "refund",
                Amount = amount,
                Currency = wallet.Currency,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                Note = note,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            _logger.LogInformation("Credited {Amount} VND refund to customer {CustomerId} for {ReferenceType} {ReferenceId}", amount, customerId, referenceType, referenceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to credit refund for customer {CustomerId}", customerId);
            throw;
        }
    }

    public async Task<CustomerWithdrawalResponseDto> CreateWithdrawalRequestAsync(Guid customerId, CreateCustomerWithdrawalDto dto)
    {
        try
        {
            var wallet = await _context.CustomerWallets
                .FirstOrDefaultAsync(w => w.CustomerId == customerId);

            if (wallet == null || wallet.AvailableBalance <= 0)
                return new CustomerWithdrawalResponseDto { Success = false, Message = "Số dư khả dụng không đủ" };

            if (dto.Amount > wallet.AvailableBalance)
                return new CustomerWithdrawalResponseDto { Success = false, Message = $"Số tiền rút ({dto.Amount:N0} ₫) vượt quá số dư khả dụng ({wallet.AvailableBalance:N0} ₫)" };

            var hasPending = await _context.CustomerWithdrawalRequests
                .AnyAsync(r => r.CustomerId == customerId && r.Status == 0);

            if (hasPending)
                return new CustomerWithdrawalResponseDto { Success = false, Message = "Bạn đã có một yêu cầu rút tiền đang chờ xử lý" };

            wallet.AvailableBalance -= dto.Amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            var requestId = Guid.NewGuid();
            _context.CustomerWithdrawalRequests.Add(new CustomerWithdrawalRequest
            {
                Id = requestId,
                CustomerId = customerId,
                WalletId = wallet.Id,
                Amount = dto.Amount,
                Currency = wallet.Currency,
                BankName = dto.BankName,
                BankAccountNumber = dto.BankAccountNumber,
                BankAccountName = dto.BankAccountName,
                Status = 0,
                RequestedAt = DateTime.UtcNow
            });

            _context.CustomerWalletLedgers.Add(new CustomerWalletLedger
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                Type = "withdrawal",
                Amount = dto.Amount,
                Currency = wallet.Currency,
                ReferenceType = "WithdrawalRequest",
                ReferenceId = requestId,
                Note = $"Yêu cầu rút tiền #{requestId:N}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            var saved = await _context.CustomerWithdrawalRequests.FindAsync(requestId);
            return new CustomerWithdrawalResponseDto
            {
                Success = true,
                Message = "Yêu cầu rút tiền đã được gửi",
                Request = MapToDto(saved!)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create withdrawal for customer {CustomerId}", customerId);
            return new CustomerWithdrawalResponseDto { Success = false, Message = "Có lỗi xảy ra khi tạo yêu cầu" };
        }
    }

    public async Task<CustomerWithdrawalListResponseDto> GetWithdrawalRequestsAsync(Guid customerId, int page, int pageSize)
    {
        var query = _context.CustomerWithdrawalRequests
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.RequestedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => MapToDto(r))
            .ToListAsync();

        return new CustomerWithdrawalListResponseDto
        {
            Success = true,
            Requests = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static CustomerWithdrawalRequestDto MapToDto(CustomerWithdrawalRequest r) => new()
    {
        Id = r.Id,
        Amount = r.Amount,
        BankName = r.BankName,
        BankAccountNumber = r.BankAccountNumber,
        BankAccountName = r.BankAccountName,
        Status = r.Status,
        StatusName = r.Status switch
        {
            0 => "Chờ xử lý",
            1 => "Đã duyệt",
            2 => "Từ chối",
            3 => "Đã thanh toán",
            _ => "Không xác định"
        },
        RejectionReason = r.RejectionReason,
        AdminNote = r.AdminNote,
        RequestedAt = r.RequestedAt,
        ReviewedAt = r.ReviewedAt,
        PaidAt = r.PaidAt
    };
}
