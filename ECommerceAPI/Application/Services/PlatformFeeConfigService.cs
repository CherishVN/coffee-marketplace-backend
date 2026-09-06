using ECommerceAPI.Application.DTOs.Admin;
using ECommerceAPI.Application.Interfaces;
using ECommerceAPI.Domain.Entities;
using ECommerceAPI.Infrastructure.Configuration;
using ECommerceAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECommerceAPI.Application.Services;

public class PlatformFeeConfigService : IPlatformFeeConfigService
{
    private readonly ApplicationDbContext _context;
    private readonly PlatformFeeSettings _defaultSettings;

    public PlatformFeeConfigService(
        ApplicationDbContext context,
        IOptions<PlatformFeeSettings> defaultSettings)
    {
        _context = context;
        _defaultSettings = defaultSettings.Value;
    }

    public async Task<PlatformFeeConfigDto?> GetCurrentAsync()
    {
        var config = await _context.PlatformFeeConfigs
            .AsNoTracking()
            .Include(c => c.Admin)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (config == null)
            return null;

        return MapToDto(config);
    }

    public async Task<decimal> GetCurrentCommissionPercentAsync()
    {
        var latest = await _context.PlatformFeeConfigs
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => (decimal?)c.CommissionPercent)
            .FirstOrDefaultAsync();

        return latest ?? _defaultSettings.CommissionPercent;
    }

    public async Task<PlatformFeeConfigDto> UpdateAsync(UpdatePlatformFeeConfigRequest request, Guid adminId)
    {
        var percent = Math.Clamp(request.CommissionPercent, 0m, 100m);

        var config = new PlatformFeeConfig
        {
            Id = Guid.NewGuid(),
            CommissionPercent = percent,
            ChangedBy = adminId,
            Note = request.Note?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _context.PlatformFeeConfigs.AddAsync(config);
        await _context.SaveChangesAsync();

        var admin = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == adminId);

        config.Admin = admin!;
        return MapToDto(config);
    }

    public async Task<(List<PlatformFeeConfigDto> Items, int TotalCount)> GetHistoryAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.PlatformFeeConfigs
            .AsNoTracking()
            .Include(c => c.Admin)
            .OrderByDescending(c => c.CreatedAt);

        var total = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new PlatformFeeConfigDto
            {
                Id = c.Id,
                CommissionPercent = c.CommissionPercent,
                ChangedBy = c.ChangedBy,
                ChangedByName = c.Admin.FullName,
                Note = c.Note,
                CreatedAt = c.CreatedAt
            })
            .ToListAsync();

        return (items, total);
    }

    private static PlatformFeeConfigDto MapToDto(PlatformFeeConfig c) => new()
    {
        Id = c.Id,
        CommissionPercent = c.CommissionPercent,
        ChangedBy = c.ChangedBy,
        ChangedByName = c.Admin?.FullName,
        Note = c.Note,
        CreatedAt = c.CreatedAt
    };
}
