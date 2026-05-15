using ECommerceAPI.Application.DTOs.Disputes;

namespace ECommerceAPI.Application.Interfaces;

public interface ISellerDisputeService
{
    /// <summary>Lấy danh sách dispute của shop thuộc quyền sở hữu của seller</summary>
    Task<SellerDisputeListResponseDto> GetShopDisputesAsync(Guid sellerId, int page, int pageSize, short? status = null, short? type = null);

    /// <summary>Lấy chi tiết 1 dispute (chỉ của shop mình)</summary>
    Task<SellerDisputeResponseDto> GetDisputeByIdAsync(Guid sellerId, Guid disputeId);

    /// <summary>Seller phản hồi dispute kèm bằng chứng</summary>
    Task<SellerDisputeResponseDto> RespondToDisputeAsync(Guid sellerId, Guid disputeId, SellerRespondDisputeDto dto);

    /// <summary>Seller chấp nhận yêu cầu trả hàng</summary>
    Task<SellerDisputeResponseDto> ApproveReturnAsync(Guid sellerId, Guid disputeId);

    /// <summary>Seller xác nhận đã nhận hàng trả về kèm bằng chứng ảnh (đối soát)</summary>
    Task<SellerDisputeResponseDto> ConfirmReturnReceiptAsync(Guid sellerId, Guid disputeId, ConfirmReturnReceiptDto dto);

    /// <summary>Seller chấp nhận hoàn tiền (dành cho loại khiếu nại không yêu cầu trả hàng)</summary>
    Task<SellerDisputeResponseDto> ApproveRefundAsync(Guid sellerId, Guid disputeId);

    /// <summary>Seller từ chối khiếu nại (chuyển cho Admin phân xử)</summary>
    Task<SellerDisputeResponseDto> RejectDisputeAsync(Guid sellerId, Guid disputeId, SellerRespondDisputeDto dto);
}
