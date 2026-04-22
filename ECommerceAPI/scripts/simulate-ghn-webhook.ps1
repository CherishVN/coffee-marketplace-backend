# Giả lập callback GHN (cần GHN:AllowSimulateWebhook=true và GHN:SimulateKey trùng).
# Ví dụ: đổi $BaseUrl, $OrderCode (mã vận đơn = orders.tracking_code), $ShopId (ghn_shop_id của shop).

param(
    [string] $BaseUrl = "https://localhost:5001",
    [string] $SimulateKey = "CHANGE_ME_DEV_ONLY",
    [string] $OrderCode = "Z82BS",
    [string] $ClientOrderCode = "",
    [string] $Status = "delivered",
    [int] $ShopId = 81558
)

$body = @{
    OrderCode      = $OrderCode
    ClientOrderCode = $ClientOrderCode
    Status         = $Status
    Type           = "Switch_status"
    Time           = (Get-Date).ToUniversalTime().ToString("o")
    TotalFee       = 71400
    ShopID         = $ShopId
} | ConvertTo-Json

Invoke-RestMethod -Uri "$BaseUrl/api/webhooks/ghn/simulate" -Method Post -Body $body -ContentType "application/json" -Headers @{ "X-Simulate-Key" = $SimulateKey }
