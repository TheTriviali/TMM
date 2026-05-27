#requires -Version 5.1
# Overnight local-model task runner for TMM.
# Runs Aider against Ollama in headless mode, one task per file,
# auto-builds + commits on success, rolls back on failure.

$ErrorActionPreference = "Stop"
$repo = $PSScriptRoot
Set-Location $repo

$logFile = Join-Path $repo "overnight.log"
"=== Overnight run started $(Get-Date -Format o) ===" | Out-File $logFile -Append

$model = "ollama_chat/qwen2.5-coder:7b-instruct-q4_K_M"

$tasks = @(
    @{
        file   = "Services/BackendCore.cs"
        prompt = "Add XML doc comments (///) to every public class, method, property, and field in this file that lacks one. Keep summaries one factual line. Do not modify any code. Skip members that already have docs."
    },
    @{
        file   = "Services/GameRegistry.cs"
        prompt = "Add XML doc comments (///) to every public class, method, property, and field in this file that lacks one. Keep summaries one factual line. Do not modify any code. Skip members that already have docs."
    },
    @{
        file   = "Services/DeploymentPlanner.cs"
        prompt = "Add XML doc comments (///) to every public class, method, property, and field in this file that lacks one. Keep summaries one factual line. Do not modify any code. Skip members that already have docs."
    },
    @{
        file   = "Services/RuleEngine.cs"
        prompt = "Add XML doc comments (///) to every public class, method, property, and field in this file that lacks one. Keep summaries one factual line. Do not modify any code. Skip members that already have docs."
    },
    @{
        file   = "Services/LoadOrderResolver.cs"
        prompt = "Add XML doc comments (///) to every public class, method, property, and field in this file that lacks one. Keep summaries one factual line. Do not modify any code. Skip members that already have docs."
    },
    @{
        file   = "ThemeEngine.cs"
        prompt = "Add XML doc comments (///) to every public class, method, property, and field in this file that lacks one. Keep summaries one factual line. Do not modify any code. Skip members that already have docs."
    }
)

foreach ($t in $tasks) {
    $full = Join-Path $repo $t.file
    if (-not (Test-Path $full)) {
        "SKIP (missing): $($t.file)" | Out-File $logFile -Append
        continue
    }

    "--- $(Get-Date -Format o) START $($t.file) ---" | Out-File $logFile -Append

    try {
        aider --model $model `
              --yes-always `
              --no-auto-commits `
              --no-pretty `
              --message $t.prompt `
              $t.file 2>&1 | Tee-Object -FilePath $logFile -Append | Out-Null
    } catch {
        "ERROR invoking aider: $_" | Out-File $logFile -Append
        git checkout -- $t.file 2>$null
        continue
    }

    $build = dotnet build TMM.csproj --nologo -v q 2>&1
    if ($LASTEXITCODE -ne 0) {
        "FAIL build after $($t.file) — rolling back" | Out-File $logFile -Append
        $build | Out-File $logFile -Append
        git checkout -- $t.file
        continue
    }

    $diff = git diff --stat -- $t.file
    if ([string]::IsNullOrWhiteSpace($diff)) {
        "NOOP (no changes): $($t.file)" | Out-File $logFile -Append
        continue
    }

    git add $t.file | Out-Null
    $msg = "auto(local): xml docs for $(Split-Path $t.file -Leaf)"
    git commit -m $msg | Out-File $logFile -Append
    "OK $($t.file)" | Out-File $logFile -Append
}

"=== Overnight run finished $(Get-Date -Format o) ===" | Out-File $logFile -Append
