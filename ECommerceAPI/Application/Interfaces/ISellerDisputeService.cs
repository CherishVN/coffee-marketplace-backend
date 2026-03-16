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
}
