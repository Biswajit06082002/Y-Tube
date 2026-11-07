# Starts the gateway and every service, each in its own terminal window.
# Usage (from the repo root):  .\scripts\run-backend.ps1
$ErrorActionPreference = 'Stop'
$backend = Join-Path $PSScriptRoot '..\backend' | Resolve-Path

Write-Host 'Building solution...' -ForegroundColor Cyan
dotnet build (Join-Path $backend 'YTube.slnx')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$projects = @(
    @{ Name = 'Auth       :5001'; Path = 'Services\Auth\Auth.Api' },
    @{ Name = 'User       :5002'; Path = 'Services\User\User.Api' },
    @{ Name = 'Video      :5003'; Path = 'Services\Video\Video.Api' },
    @{ Name = 'Feed       :5004'; Path = 'Services\Feed\Feed.Api' },
    @{ Name = 'Comment    :5005'; Path = 'Services\Comment\Comment.Api' },
    @{ Name = 'Notification :5006'; Path = 'Services\Notification\Notification.Api' },
    @{ Name = 'Processing (worker)'; Path = 'Services\Processing\Processing.Worker' },
    @{ Name = 'Gateway    :5000'; Path = 'Gateway\YTube.Gateway' }
)

foreach ($p in $projects) {
    $dir = Join-Path $backend $p.Path
    Start-Process powershell -ArgumentList @(
        '-NoExit', '-Command',
        "`$Host.UI.RawUI.WindowTitle = 'YTube - $($p.Name)'; dotnet run --no-build --project '$dir'"
    )
    Write-Host "Started $($p.Name)" -ForegroundColor Green
}
