$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$generatorRoot = Join-Path $repositoryRoot "scripts/livekit-config-schema"
$schemaRoot = Join-Path $repositoryRoot "schemas/livekit-upstream"
$modelRoot = Join-Path $repositoryRoot "src/Fstonge.Aspire.Hosting.LiveKit/UpstreamConfiguration"
$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) "livekit-upstream-$([guid]::NewGuid().ToString('N'))"

$services = @(
    @{
        Name = "server"
        Repository = "livekit"
        Branch = "master"
        Package = "./pkg/config"
        Type = "Config"
        Namespace = "Aspire.Hosting.LiveKit.Configuration.Server"
        CSharpType = "LiveKitServerUpstreamConfiguration"
    },
    @{
        Name = "ingress"
        Repository = "ingress"
        Branch = "main"
        Package = "./pkg/config"
        Type = "Config"
        Namespace = "Aspire.Hosting.LiveKit.Configuration.Ingress"
        CSharpType = "LiveKitIngressUpstreamConfiguration"
    },
    @{
        Name = "egress"
        Repository = "egress"
        Branch = "main"
        Package = "./pkg/config"
        Type = "ServiceConfig"
        Namespace = "Aspire.Hosting.LiveKit.Configuration.Egress"
        CSharpType = "LiveKitEgressUpstreamConfiguration"
    },
    @{
        Name = "sip"
        Repository = "sip"
        Branch = "main"
        Package = "./pkg/config"
        Type = "Config"
        Namespace = "Aspire.Hosting.LiveKit.Configuration.Sip"
        CSharpType = "LiveKitSipUpstreamConfiguration"
    }
)

function Invoke-CheckedCommand([string] $Executable, [string[]] $Arguments, [string] $Description) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Write-Utf8File([string] $Path, [string] $Content) {
    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        [System.Text.UTF8Encoding]::new($false))
}

function Test-GeneratedSchema([string] $Path, [string] $ServiceName) {
    $content = [System.IO.File]::ReadAllText($Path)
    try {
        $schema = ConvertFrom-Json -InputObject $content
    }
    catch {
        throw "The generated $ServiceName schema is not valid JSON: $($_.Exception.Message)"
    }

    if ($schema.'$schema' -ne "https://json-schema.org/draft/2020-12/schema" -or
        $schema.type -ne "object" -or
        $null -eq $schema.properties) {
        throw "The generated $ServiceName schema is missing its JSON Schema metadata or root object properties."
    }

    $definitionNames = @($schema.'$defs'.PSObject.Properties.Name)
    $references = [regex]::Matches($content, '"\$ref"\s*:\s*"#/\$defs/([^"]+)"')
    foreach ($reference in $references) {
        if ($definitionNames -notcontains $reference.Groups[1].Value) {
            throw "The generated $ServiceName schema contains an unresolved reference '$($reference.Groups[1].Value)'."
        }
    }
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Git is required to clone the LiveKit upstream repositories."
}
if (-not (Get-Command go -ErrorAction SilentlyContinue)) {
    throw "Go 1.23 or later is required to generate the LiveKit configuration schemas."
}

[System.IO.Directory]::CreateDirectory($schemaRoot) | Out-Null
[System.IO.Directory]::CreateDirectory($modelRoot) | Out-Null
[System.IO.Directory]::CreateDirectory($workRoot) | Out-Null

try {
    $sources = foreach ($service in $services) {
        $checkoutPath = Join-Path $workRoot $service.Repository
        $repositoryUri = "https://github.com/livekit/$($service.Repository).git"

        Write-Host "Cloning $($service.Name) configuration source from $repositoryUri ($($service.Branch))"
        Invoke-CheckedCommand "git" @(
            "clone",
            "--depth", "1",
            "--branch", $service.Branch,
            $repositoryUri,
            $checkoutPath
        ) "Clone $($service.Name) repository"

        $revision = (& git -C $checkoutPath rev-parse HEAD).Trim()
        if ($LASTEXITCODE -ne 0) {
            throw "Could not read the $($service.Name) repository revision."
        }

        $outputPath = Join-Path $schemaRoot "$($service.Name).schema.json"
        $modelPath = Join-Path $modelRoot "$($service.Name).g.cs"
        Write-Host "Generating $($service.Name) schema from $($service.Package).$($service.Type)"
        Push-Location $generatorRoot
        try {
            Invoke-CheckedCommand "go" @(
                "run", ".",
                "-dir", $checkoutPath,
                "-package", $service.Package,
                "-type", $service.Type,
                "-out", $outputPath
            ) "Generate $($service.Name) schema"
            Test-GeneratedSchema -Path $outputPath -ServiceName $service.Name

            Write-Host "Generating $($service.Name) C# models from $outputPath"
            Invoke-CheckedCommand "go" @(
                "run", ".",
                "-from-schema", $outputPath,
                "-csharp-out", $modelPath,
                "-csharp-namespace", $service.Namespace,
                "-csharp-root-type", $service.CSharpType
            ) "Generate $($service.Name) C# models"
        }
        finally {
            Pop-Location
        }

        [ordered]@{
            service = $service.Name
            repository = $repositoryUri
            branch = $service.Branch
            revision = $revision
            package = $service.Package
            type = $service.Type
        }
    }

    $sourceManifest = ConvertTo-Json -InputObject @($sources) -Depth 10
    Write-Utf8File -Path (Join-Path $schemaRoot "sources.json") -Content ($sourceManifest + "`n")
}
finally {
    if (Test-Path -LiteralPath $workRoot) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force
    }
}
