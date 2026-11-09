param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory,
    [string]$PackageVersion
)

$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\packages"
}
else {
    $OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
}

$projectDirectory = Join-Path $repoRoot "src"
$projects = @(Get-ChildItem -Path $projectDirectory -Filter "*.csproj" -Recurse | Sort-Object FullName)

if ($projects.Count -eq 0) {
    throw "No project files were found under '$projectDirectory'."
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

foreach ($project in $projects) {
    Write-Host "Packing $($project.FullName)"
    $packArguments = @(
        "pack"
        $project.FullName
        "--configuration"
        $Configuration
        "--output"
        $OutputDirectory
    )

    if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
        $packArguments += "-p:PackageVersion=$PackageVersion"
    }

    & dotnet @packArguments

    if ($LASTEXITCODE -ne 0) {
        throw "Packing '$($project.FullName)' failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Packages written to '$OutputDirectory'."
