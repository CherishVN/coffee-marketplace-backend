using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECommerceAPI.Application.Services;

public class WithdrawAdminService : IWithdrawAdminService
{
    private readonly ApplicationDbContext _context;

    public WithdrawAdminService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WithdrawListResponseDto> GetAllRequestsAsync(int page, int pageSize, short? status)
    {
        var query = _context.SellerWithdrawalRequests
            .Include(r => r.Seller)
            .Include(r => r.ReviewedByNavigation)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var totalCount = await query.CountAsync();

        var requests = await query
            .OrderByDescending(r => r.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => MapToDto(r))
            .ToListAsync();

        return new WithdrawListResponseDto
        {
            Success = true,
            Requests = requests,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<WithdrawResponseDto> GetRequestByIdAsync(Guid requestId)
    {
        var request = await _context.SellerWithdrawalRequests
            .Include(r => r.Seller)
            .Include(r => r.ReviewedByNavigation)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            return new WithdrawResponseDto
            {
                Success = false,
                Message = "Không tìm thấy yêu cầu rút tiền"
            };
        }

        return new WithdrawResponseDto
        {
            Success = true,
            Request = MapToDto(request)
        };
    }

    public async Task<WithdrawResponseDto> ApproveRequestAsync(Guid requestId, ApproveWithdrawDto dto, Guid adminId)
    {
        var request = await _context.SellerWithdrawalRequests
            .Include(r => r.Seller)
            .Include(r => r.Wallet)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            return new WithdrawResponseDto
            {
                Success = false,
                Message = "Không tìm thấy yêu cầu rút tiền"
            };
        }

        if (request.Status != 0)
        {
            return new WithdrawResponseDto
            {
                Success = false,
                Message = "Chỉ có thể duyệt yêu cầu đang chờ xử lý"
            };
        }

        request.Status = 1;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedBy = adminId;
        request.AdminNote = dto.AdminNote;
        request.PaidAt = DateTime.UtcNow;

        if (request.Wallet != null)
        {
            request.Wallet.AvailableBalance -= request.Amount;
            request.Wallet.UpdatedAt = DateTime.UtcNow;
        }

        var auditLog = new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.SellerId,
            EditorId = adminId,
            Action = "APPROVE_WITHDRAW",
            FieldName = "WithdrawalRequest",
            OldValue = $"Pending - Amount: {request.Amount} {request.Currency}",
            NewValue = $"Approved - {dto.AdminNote}",
            CreatedAt = DateTime.UtcNow
        };
        await _context.UserAuditLogs.AddAsync(auditLog);

        await _context.SaveChangesAsync();

        var updatedRequest = await _context.SellerWithdrawalRequests
            .Include(r => r.Seller)
            .Include(r => r.ReviewedByNavigation)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        return new WithdrawResponseDto
        {
            Success = true,
            Message = "Đã duyệt yêu cầu rút tiền",
            Request = MapToDto(updatedRequest!)
        };
    }

    public async Task<WithdrawResponseDto> RejectRequestAsync(Guid requestId, RejectWithdrawDto dto, Guid adminId)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return new WithdrawResponseDto
            {
                Success = false,
                Message = "Vui lòng nhập lý do từ chối"
            };
        }

        var request = await _context.SellerWithdrawalRequests
            .Include(r => r.Seller)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (request == null)
        {
            return new WithdrawResponseDto
            {
                Success = false,
                Message = "Không tìm thấy yêu cầu rút tiền"
            };
        }

        if (request.Status != 0)
        {
            return new WithdrawResponseDto
            {
                Success = false,
                Message = "Chỉ có thể từ chối yêu cầu đang chờ xử lý"
            };
        }

        request.Status = 2;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedBy = adminId;
        request.RejectionReason = dto.Reason;
        request.AdminNote = dto.AdminNote;

        var auditLog = new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = request.SellerId,
            EditorId = adminId,
            Action = "REJECT_WITHDRAW",
            FieldName = "WithdrawalRequest",
            OldValue = $"Pending - Amount: {request.Amount} {request.Currency}",
            NewValue = $"Rejected - {dto.Reason}",
            CreatedAt = DateTime.UtcNow
        };
        await _context.UserAuditLogs.AddAsync(auditLog);

        await _context.SaveChangesAsync();

        var updatedRequest = await _context.SellerWithdrawalRequests
            .Include(r => r.Seller)
            .Include(r => r.ReviewedByNavigation)
            .FirstOrDefaultAsync(r => r.Id == requestId);

        return new WithdrawResponseDto
        {
            Success = true,
            Message = "Đã từ chối yêu cầu rút tiền",
            Request = MapToDto(updatedRequest!)
        };
    }

    private static WithdrawRequestDto MapToDto(SellerWithdrawalRequest r)
    {
        return new WithdrawRequestDto
        {
            Id = r.Id,
            SellerId = r.SellerId,
            SellerName = r.Seller?.FullName,
            Amount = r.Amount,
            Currency = r.Currency,
            BankName = r.BankName,
            BankAccountNumber = r.BankAccountNumber,
            BankAccountName = r.BankAccountName,
            Status = r.Status,
            StatusName = GetStatusName(r.Status),
            RejectionReason = r.RejectionReason,
            AdminNote = r.AdminNote,
            RequestedAt = r.RequestedAt,
            ReviewedAt = r.ReviewedAt,
            ReviewedBy = r.ReviewedBy,
            ReviewedByName = r.ReviewedByNavigation?.FullName,
            PaidAt = r.PaidAt
        };
    }

    private static string GetStatusName(short status)
    {
        return status switch
        {
            0 => "Pending",
            1 => "Approved",
            2 => "Rejected",
            _ => "Unknown"
        };
    }
}
