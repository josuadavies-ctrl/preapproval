# Dynamic path resolution to work perfectly on any developer machine or GitHub Actions runner
$root = $PSScriptRoot
if ([string]::IsNullOrEmpty($root)) {
    $root = Get-Location
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outputFile = "$root\Data\Test_Data_Output\Test_Data_Output_$timestamp.csv"
$stateFile = "$root\bin\Debug\net10.0\teststate.json"
$proactiveAccountsFile = "$root\Data\Master_Ledger\DATA_BorrowProactiveActiveAccounts.csv"
$reportFile = "$root\Data\Journey_Report\Journey_Report_$timestamp.txt"

# Track baseline time for the report
$startTime = [DateTime]::Now

# Helper function to check step success and force termination on failure
function Check-StepResult {
    param (
        [int]$ExitCode,
        [string]$StepName
    )
    if ($ExitCode -ne 0) {
        Write-Error "❌ CRITICAL FAILURE: $StepName failed with Exit Code $ExitCode. Halting entire sequence execution immediately to preserve environment state!"
        Exit $ExitCode
    }
}

# 1. Run Seeding Task
Write-Host "Starting Step B (Data Seeding)..." -ForegroundColor Cyan
dotnet test --filter B_DataSeeding
Check-StepResult -ExitCode $LASTEXITCODE -StepName "Step B (Data Seeding)"

# ⏳ Cooldown delay to let the RC app pools recycle and stabilize 
Write-Host "Waiting 15 seconds for RC environment App Pools to recycle..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

Write-Host "📡 Testing network route to Ops Admin UI..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "https://rc-opsadminui.saldev.net/login" -TimeoutSec 10 -Method Head -ErrorAction Stop
    Write-Host "✅ Route Open! Received Status Code: $($response.StatusCode)" -ForegroundColor Green
} catch {
    Write-Error "❌ NETWORK ISOLATION DETECTED: The GitHub Actions runner cannot reach the Ops Admin UI. Reason: $($_.Exception.Message)"
    Exit 1
}
# 2. Run UI and API Test Automation Steps
Write-Host "Starting Step C (Ops Admin)..." -ForegroundColor Cyan
dotnet test --filter C_OpsAdmin
Check-StepResult -ExitCode $LASTEXITCODE -StepName "Step C (Ops Admin)"

Write-Host "Starting Step A (Postman Automation)..." -ForegroundColor Cyan
dotnet test --filter A_PostmanAutomation
Check-StepResult -ExitCode $LASTEXITCODE -StepName "Step A (Postman Automation)"

# =========================================================================
# 📊 DYNAMIC DATA MERGE (Executed AFTER tests complete)
# =========================================================================
Write-Host "Processing test state and merging data set into CSV..." -ForegroundColor Yellow

$data = [ordered]@{
    "email"             = ""
    "payrollId"         = ""
    "firstName"         = "Sharon"
    "lastName"          = "Rajapaksa"
    "title"             = "Ms"
    "dateOfBirth"       = ""
    "loanAmount"        = 1500
    "loanPurpose"       = "VehicleLoan"
    "buildingNumber"    = "80B"
    "street"            = "High Street"
    "town"              = "St. Albans"
    "postCode"          = "AL3 8LE"
    "applyUrl"          = ""
    "urlTarget"         = "" 
}

# Read state file AFTER automated tasks have updated it
if (Test-Path $stateFile) {
    $state = Get-Content $stateFile | ConvertFrom-Json
    $data["email"] = $state.Email
    $data["payrollId"] = $state.PayrollId
    $data["dateOfBirth"] = $state.DateOfBirth
    
    # Capture the generated Id and structure the urlTarget accurately
    if ($state.Id) {
        $data["urlTarget"] = "https://rc-loanapi.saldev.net/api/topupapplications/borrower/$($state.Id)/eligibility?apiKey=c10cc286-9877-4a3b-a12c-2cff2c7005dc&useDecisionCache=true"
        Write-Host "Captured urlTarget successfully for Borrower ID: $($state.Id)" -ForegroundColor Green
    } else {
        Write-Warning "State file found, but 'Id' property was missing or empty. Cannot construct urlTarget."
    }
} else {
    Write-Warning "Target state file not found at: $stateFile"
}

# Pull applyUrl from the temporary automation output text if available
$tempFile = "$root\Data\Test Data Output.csv"
if (Test-Path $tempFile) {
    $csvA = Import-Csv -Path $tempFile
    if ($null -eq $csvA -or $csvA.Count -eq 0 -or [string]::IsNullOrEmpty($csvA[0].applyUrl)) {
        Write-Error "❌ CRITICAL SEQUENCE ABORT: 'applyUrl' is missing or null because the application was Declined by the Decisioning Engine API. Verify the printed Postman payload for validation discrepancies."
        Exit 1
    }
    $data["applyUrl"] = $csvA[0].applyUrl
}

# Export clean, un-nested PSCustomObject to the isolated unique output CSV
$currentRecordObject = [PSCustomObject]$data
$currentRecordObject | Export-Csv -Path $outputFile -NoTypeInformation -Force
Write-Host "Created full context unique CSV: $outputFile" -ForegroundColor Green

# =========================================================================
# 📝 PRE-APPROVAL & NON-DESTRUCTIVE LEDGER APPEND
# =========================================================================
$env:CSV_PATH = $outputFile

# Set display behavior based on execution engine parameters
if ($null -eq $env:HEADLESS) {
    $env:HEADED = 1
} elseif ($env:HEADLESS -eq "true") {
    $env:HEADED = 0
} else {
    $env:HEADED = 1
}

Write-Host "Starting Step D (Create Loan)..." -ForegroundColor Cyan
dotnet test --filter D_CreateLoan
Check-StepResult -ExitCode $LASTEXITCODE -StepName "Step D (Create Loan)"

Write-Host "Starting Step E (Wait for Loan Active Status)..." -ForegroundColor Cyan
dotnet test --filter E_OpsAdmin_Active
Check-StepResult -ExitCode $LASTEXITCODE -StepName "Step E (Wait for Loan Active Status)"

Write-Host "Starting Eligibility Check Step..." -ForegroundColor Cyan
dotnet test --filter Eligibility
Check-StepResult -ExitCode $LASTEXITCODE -StepName "Eligibility API Validation"

# Capture total runtime metrics
$endTime = [DateTime]::Now
$totalMinutes = [Math]::Round(($endTime - $startTime).TotalMinutes, 2)
$timestampString = Get-Date -Date $startTime -Format "dddd, dd MMMM yyyy HH:mm:ss"

Write-Host "Tests completed successfully! Preserving history and writing new profile record..." -ForegroundColor Green

$targetDir = Split-Path -Path $proactiveAccountsFile
if (!(Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

# Clean, strict hardcoded sequence of your target columns
$allHeaders = @("email","payrollId","firstName","lastName","title","dateOfBirth","loanAmount","loanPurpose","buildingNumber","street","town","postCode","applyUrl","urlTarget")
$alignedRecord = $currentRecordObject | Select-Object -Property $allHeaders

if (Test-Path $proactiveAccountsFile) {
    $alignedRecord | Export-Csv -Path $proactiveAccountsFile -NoTypeInformation -Append -Force
    Write-Host "Successfully appended new row into master file (History Preserved): $proactiveAccountsFile" -ForegroundColor Green
} else {
    $alignedRecord | Export-Csv -Path $proactiveAccountsFile -NoTypeInformation -Force
    Write-Host "Created brand new master ledger file with headers: $proactiveAccountsFile" -ForegroundColor Green
}

# =========================================================================
# 📄 AUTOMATED SUCCESS JOURNEY REPORT GENERATION
# =========================================================================
$reportContent = @"
============================================================
    END-TO-END PREAPPROVAL JOURNEY SUCCESS REPORT
============================================================
USER:           $env:USERNAME
TIMESTAMP:      $timestampString
OUTPUT LEDGER:  $proactiveAccountsFile
STATE RUN FILE: $stateFile
------------------------------------------------------------
STEP BREAKDOWN & PARAMETERS CAPTURED
------------------------------------------------------------
* STEP B (Data Seeding):     SUCCESS
  -> Seeded User Account:   $($data['email'])
  -> Internal Payroll ID:   $($data['payrollId'])

* STEP C (Ops Admin UI):     SUCCESS
  -> Microservice Syncing:  Verified (rc-validation, rc-loanapi, rc-bankandidcheck)
  
* STEP A (API Newman Run):   SUCCESS
  -> Rendered App Landing:  $($data['applyUrl'])

* STEP D (Loan Placement):   SUCCESS
  -> Calculated Target URL: $($data['urlTarget'])
  -> Committed Principal:   £$($data['loanAmount']).00
  -> Core Target Context:   $($data['loanPurpose']) | Address: $($data['buildingNumber']) $($data['street']), $($data['town']), $($data['postCode'])

* STEP E (Ops Admin Active): SUCCESS
  -> Loan Activation:       Confirmed Active Status within 20 retries (Polling Loop)
  
* Eligibility API:           SUCCESS
  -> Eligibility Check:     Passed with expected response codes "1" and payload validation
============================================================
============================================================
FINAL VERDICT: FULL PREAPPROVAL RUN COMPLETED (LEDGER ARTIFACT GENERATED)
TOTAL TIME:    $totalMinutes mins
============================================================
"@
$reportContent | Out-File -FilePath $reportFile -Encoding utf8
Write-Host "Journey Report successfully compiled and written to: $reportFile" -ForegroundColor Green

# powershell -ExecutionPolicy Bypass -File .\SequenceRunner.ps1 
