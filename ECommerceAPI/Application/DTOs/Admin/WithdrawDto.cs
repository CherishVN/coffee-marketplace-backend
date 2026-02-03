namespace ECommerceAPI.Application.DTOs.Admin;

public class WithdrawRequestDto
{
    public Guid Id { get; set; }
    public Guid SellerId { get; set; }
    public string? SellerName { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;
    public short Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public string? AdminNote { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class ApproveWithdrawDto
{
    public string? AdminNote { get; set; }
}

public class RejectWithdrawDto
{
    public string Reason { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
}

public class WithdrawListResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<WithdrawRequestDto> Requests { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class WithdrawResponseDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public WithdrawRequestDto? Request { get; set; }
}
