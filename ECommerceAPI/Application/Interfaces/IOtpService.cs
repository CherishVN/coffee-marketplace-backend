namespace ECommerceAPI.Application.Interfaces;

public interface IOtpService
{
    string GenerateAndStore(Guid userId, string newEmail);

    bool Verify(Guid userId, string newEmail, string otp);
}
