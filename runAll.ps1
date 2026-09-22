$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$javaApiPom = Join-Path $root 'javaAPIs\pom.xml'
$javaServicePom = Join-Path $root 'javaAPIService\pom.xml'
$javaServiceLog = Join-Path $root 'javaAPIService\service.log'
$javaServiceErr = Join-Path $root 'javaAPIService\service.err'

function Pause-ForNextStep {
    Write-Host "" 
    Write-Host 'Press Enter to continue to the next step...' -ForegroundColor Yellow
    $null = Read-Host
}

function Stop-JavaService {
    $matches = Get-CimInstance Win32_Process |
        Where-Object {
            $_.Name -eq 'java.exe' -and
            $_.CommandLine -match 'com\.example\.grpc\.JavaApiServiceServer'
        }

    if (-not $matches) {
        Write-Host 'Java gRPC service is already down.' -ForegroundColor Green
        return
    }

    foreach ($p in $matches) {
        Stop-Process -Id $p.ProcessId -Force
        Write-Host "Stopped Java gRPC service PID $($p.ProcessId)" -ForegroundColor Green
    }
}

function Confirm-ServiceDown {
    $matches = Get-CimInstance Win32_Process |
        Where-Object {
            $_.Name -eq 'java.exe' -and
            $_.CommandLine -match 'com\.example\.grpc\.JavaApiServiceServer'
        }

    if ($matches) {
        throw 'Java gRPC service is still running.'
    }

    Write-Host 'Java gRPC service is down.' -ForegroundColor Green
}

Write-Host '============================================================' -ForegroundColor Cyan
Write-Host 'runAll - Java API / JNI / IKVM / gRPC validation flow' -ForegroundColor Cyan
Write-Host 'This script will:' -ForegroundColor Cyan
Write-Host '  1) build the shared Java API' -ForegroundColor Cyan
Write-Host '  2) run the JNI test' -ForegroundColor Cyan
Write-Host '  3) run the IKVM test' -ForegroundColor Cyan
Write-Host '  4) start the Java gRPC service and run the .NET gRPC client' -ForegroundColor Cyan
Write-Host '  5) stop the Java gRPC service and confirm it is down' -ForegroundColor Cyan
Write-Host '============================================================' -ForegroundColor Cyan
Pause-ForNextStep

Write-Host ''
Write-Host '== Step 1: Build Java API ==' -ForegroundColor Cyan
& mvn -q -f $javaApiPom clean package
if ($LASTEXITCODE -ne 0) { throw 'Step 1 failed: Java API build.' }
Write-Host 'Java API build succeeded.' -ForegroundColor Green
Pause-ForNextStep

Write-Host ''
Write-Host '== Step 2: Run JNI test ==' -ForegroundColor Cyan
& dotnet test (Join-Path $root 'dotNetJavaWithJNI\dotNetJavaWithJNI.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Step 2 failed: JNI test.' }
Write-Host 'JNI bridge test passed.' -ForegroundColor Green
Pause-ForNextStep

Write-Host ''
Write-Host '== Step 3: Run IKVM test ==' -ForegroundColor Cyan
& dotnet test (Join-Path $root 'dotNetJavaWithkvm\dotNetJavaWithkvm.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Step 3 failed: IKVM test.' }
Write-Host 'IKVM bridge test passed.' -ForegroundColor Green
Pause-ForNextStep

Write-Host ''
Write-Host '== Step 4: Start Java gRPC service and run .NET client ==' -ForegroundColor Cyan
& mvn -q -f $javaServicePom package
if ($LASTEXITCODE -ne 0) { throw 'Step 4 failed: Java gRPC service build.' }

$serviceProcess = Start-Process -FilePath 'mvn' -ArgumentList @('-q', '-f', $javaServicePom, 'exec:java', '-Dexec.mainClass=com.example.grpc.JavaApiServiceServer') -WorkingDirectory $root -NoNewWindow -PassThru -RedirectStandardOutput $javaServiceLog -RedirectStandardError $javaServiceErr
Start-Sleep -Seconds 5

$javaServiceMatches = Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq 'java.exe' -and
        $_.CommandLine -match 'com\.example\.grpc\.JavaApiServiceServer'
    }

if (-not $javaServiceMatches) {
    throw 'Step 4 failed: Java gRPC service did not start.'
}

Write-Host "Java gRPC service started with PID $($javaServiceMatches[0].ProcessId)" -ForegroundColor Green

& dotnet run --project (Join-Path $root 'dotnetJavaWithgRPC\dotnetJavaWithgRPC.csproj') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Step 4 failed: .NET gRPC client invocation.' }
Write-Host '.NET gRPC client completed successfully.' -ForegroundColor Green
Pause-ForNextStep

Write-Host ''
Write-Host '== Step 5: Stop Java gRPC service ==' -ForegroundColor Cyan
Stop-JavaService
Confirm-ServiceDown

Write-Host ''
Write-Host '============================================================' -ForegroundColor Cyan
Write-Host 'Summary:' -ForegroundColor Cyan
Write-Host '  - Shared Java API built successfully.' -ForegroundColor Green
Write-Host '  - JNI bridge test passed.' -ForegroundColor Green
Write-Host '  - IKVM bridge test passed.' -ForegroundColor Green
Write-Host '  - Java gRPC service was started and exercised by the .NET client.' -ForegroundColor Green
Write-Host '  - Java gRPC service is now down.' -ForegroundColor Green
Write-Host '============================================================' -ForegroundColor Cyan
