using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuoteFlow.Core.Pricing;
using QuoteFlow.Core.Rules;

namespace QuoteFlow.Infrastructure.Pricing;

/// <summary>
/// Background worker ที่ sync ราคาน้ำมันจาก external API เข้า FuelSurcharge rule
/// ทุก 24 ชั่วโมง — PricingEngine ยังอ่านจาก rule ตามปกติ
/// ปิดการคำนวณน้ำมันได้โดย set isActive=false บน FuelSurcharge rule
/// </summary>
public class FuelPriceSyncWorker(
    IFuelPriceService fuelPriceService,
    IRuleRepository ruleRepository,
    ILogger<FuelPriceSyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // sync ทันทีตอน startup
        await SyncAsync();

        using var timer = new PeriodicTimer(SyncInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await SyncAsync();
    }

    private async Task SyncAsync()
    {
        var price = await fuelPriceService.GetCurrentPriceAsync();
        if (price is null)
        {
            logger.LogDebug("Fuel price not available from external API — keeping stored rule value");
            return;
        }

        var rules = await ruleRepository.GetAllAsync();
        var fuelRule = rules.FirstOrDefault(r => r.RuleType == RuleType.FuelSurcharge && r.IsActive);
        if (fuelRule is null)
        {
            logger.LogDebug("No active FuelSurcharge rule found — skipping sync");
            return;
        }

        // อัปเดต pricePerLiter ใน parameters ของ rule
        fuelRule.Parameters = JsonSerializer.Serialize(new { pricePerLiter = price.Value });
        fuelRule.UpdatedAt = DateTimeOffset.UtcNow;

        await ruleRepository.UpdateAsync(fuelRule.Id, fuelRule);
        logger.LogInformation(
            "FuelSurcharge rule updated: {Price} THB/L (rule: {RuleId})",
            price.Value, fuelRule.Id);
    }
}
