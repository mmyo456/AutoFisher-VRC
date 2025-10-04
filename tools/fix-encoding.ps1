<#
Usage: Run from repository root in PowerShell (Windows PowerShell 5.1 or later):
  .\tools\fix-encoding.ps1 -Path . -WhatIf:$false

This script finds files with common source extensions (.cs, .resx, .Designer.cs, .xaml)
that appear to contain replacement characters (�) or invalid UTF-8 sequences, makes
a backup with suffix .orig, and tries to re-decode the original bytes as GBK (codepage 936)
and save as UTF8 (without BOM). It will only modify files when -WhatIf is $false.
#>

param(
    [string]$Path = ".",
    [object]$WhatIf = $true
)

# Coerce WhatIf to boolean to tolerate different CI/pass-through representations
$WhatIfBool = $true
if ($WhatIf -is [bool]) {
    $WhatIfBool = $WhatIf
}
elseif ($WhatIf -is [int]) {
    $WhatIfBool = [bool]$WhatIf
}
elseif ($WhatIf -is [string]) {
    switch ($WhatIf.ToLower()) {
        'false' { $WhatIfBool = $false }
        'true'  { $WhatIfBool = $true }
        '0'     { $WhatIfBool = $false }
        '1'     { $WhatIfBool = $true }
        default { 
            # If CI passed a type name or other unexpected token, default to $false only when explicitly 'false' isn't matched.
            try { $parsed = [System.Convert]::ToBoolean($WhatIf); $WhatIfBool = $parsed } catch { $WhatIfBool = $true }
        }
    }
}
else {
    # Fallback
    $WhatIfBool = $true
}

Function Test-ContainsReplacementChar([string]$content) {
    return $content -match "\uFFFD|�"
}

Write-Output "Scanning path: $Path"

$exts = '*.cs','*.resx','*.Designer.cs','*.xaml','*.config','*.txt'
$files = @()
foreach ($ext in $exts) {
    $files += Get-ChildItem -Path $Path -Recurse -Include $ext -File -ErrorAction SilentlyContinue
}

if ($files.Count -eq 0) {
    Write-Output "No matching files found."
    return
}

$candidates = @()
foreach ($f in $files) {
    try {
        # Try read as UTF8
        $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
        $utf8 = [System.Text.Encoding]::UTF8.GetString($bytes)

        if (Test-ContainsReplacementChar $utf8) {
            $candidates += $f
            continue
        }

        # Additional heuristic: attempt to decode strictly as UTF8 and catch failures
        $enc = New-Object System.Text.UTF8Encoding $false,$true
        $decoder = $enc.GetDecoder()
        $null = $decoder.GetCharCount($bytes,0,$bytes.Length)
    }
    catch {
        # If UTF8 decode failed, consider candidate
        $candidates += $f
    }
}

if ($candidates.Count -eq 0) {
    Write-Output "No likely-encoded files found."
    return
}

Write-Output "Found $($candidates.Count) candidate files to inspect/convert:" 
foreach ($c in $candidates) { Write-Output " - $($c.FullName)" }

foreach ($f in $candidates) {
    Write-Output "Processing: $($f.FullName)"
    if ($WhatIfBool -eq $true) { Write-Output "WhatIf: backup and conversion skipped."; continue }

    $origPath = "$($f.FullName).orig"
    if (-not (Test-Path $origPath)) {
        Copy-Item -Path $f.FullName -Destination $origPath -Force
        Write-Output "  -> backup saved to $origPath"
    }

    $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
    # Decode bytes as GBK (codepage 936) and re-encode into UTF8 without BOM
    $gbk = [System.Text.Encoding]::GetEncoding(936)
    $text = $gbk.GetString($bytes)
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($f.FullName, $text, $utf8NoBom)
    Write-Output "  -> converted to UTF-8 (assumed GBK)"
}

Write-Output "Done. Please rebuild the solution and test the produced executable for proper Chinese rendering."
