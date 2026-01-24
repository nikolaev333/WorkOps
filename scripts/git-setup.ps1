# Git setup: init, first commit, main branch
# Run from repo root:  .\scripts\git-setup.ps1

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot\..

if (Test-Path .git) {
    Write-Host "Git repo exists. Checking status..."
    git status
    $r = Read-Host "Continue with add/commit? (y/n)"
    if ($r -ne "y") { exit }
} else {
    git init
    git branch -M main
    Write-Host "Git initialized, branch main."
}

git add .
git status

$msg = "Initial commit: ASP.NET Core API, EF Core, SQL Server"
$r = Read-Host "Commit with message: $msg`nProceed? (y/n)"
if ($r -ne "y") { exit }

git commit -m $msg
Write-Host "Done. To push:"
Write-Host '  git remote add origin https://github.com/<username>/WorkOps.Api.git'
Write-Host '  git push -u origin main'
