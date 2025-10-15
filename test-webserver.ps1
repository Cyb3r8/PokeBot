# Diagnostic script for PokeBot WebServer binding issue
# This script helps verify if the webserver is actually listening on all interfaces

Write-Host "=== PokeBot WebServer Diagnostic ===" -ForegroundColor Cyan
Write-Host ""

# Check URL reservations
Write-Host "1. Checking HTTP URL reservations:" -ForegroundColor Yellow
netsh http show urlacl url=http://+:8090/
Write-Host ""

# Check listening ports
Write-Host "2. Checking listening ports (8090):" -ForegroundColor Yellow
$ports = netstat -ano | Select-String ":8090"
$ports | ForEach-Object { Write-Host $_ }
Write-Host ""

# Find PokeBot processes
Write-Host "3. Finding PokeBot processes:" -ForegroundColor Yellow
$pokebots = Get-Process -Name "PokeBot" -ErrorAction SilentlyContinue
if ($pokebots) {
    $pokebots | Format-Table ProcessName, Id, @{Name="Port File";Expression={
        $exePath = $_.Path
        if ($exePath) {
            $dir = Split-Path $exePath
            $portFile = Join-Path $dir "PokeBot_$($_.Id).port"
            if (Test-Path $portFile) {
                Get-Content $portFile
            } else {
                "N/A"
            }
        }
    }} -AutoSize
} else {
    Write-Host "No PokeBot processes found" -ForegroundColor Red
}
Write-Host ""

# Test localhost connection
Write-Host "4. Testing localhost:8090 connection:" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:8090/" -TimeoutSec 5 -UseBasicParsing
    Write-Host "SUCCESS - Localhost accessible (Status: $($response.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "FAILED - $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# Test network interface connection (if not running on same machine)
Write-Host "5. Local IP addresses:" -ForegroundColor Yellow
Get-NetIPAddress -AddressFamily IPv4 | Where-Object {$_.IPAddress -ne "127.0.0.1"} | Format-Table IPAddress, InterfaceAlias -AutoSize
Write-Host ""

Write-Host "=== Next Steps ===" -ForegroundColor Cyan
Write-Host "If netstat shows PID 4 (System), this is NORMAL for HttpListener on Windows"
Write-Host "The http.sys kernel driver (PID 4) handles the actual socket binding"
Write-Host ""
Write-Host "To verify external access works:"
Write-Host "  1. Find your network IP above (e.g., 10.0.10.19)"
Write-Host "  2. Test from REMOTE machine: http://10.0.10.19:8090/"
Write-Host "  3. If it fails, check Windows Firewall"
Write-Host ""
