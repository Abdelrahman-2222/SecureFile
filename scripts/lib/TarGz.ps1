<#
.SYNOPSIS
  New-TarGz: writes a .tar.gz with real Unix file modes, also when run on Windows. Dot-source it: . "$PSScriptRoot/lib/TarGz.ps1"
.DESCRIPTION
  Windows' own tar.exe stores every file as 0644 (not executable), so a program packed on Windows cannot be started on
  Linux or macOS until the user runs chmod +x. This helper uses .NET's System.Formats.Tar instead and sets 0755 on the
  files named in -Executables (and on folders), 0644 on everything else. Needs PowerShell 7 (pwsh).
#>

function New-TarGz {
    param(
        [Parameter(Mandatory)][string]$ParentFolder,   # the folder that contains $FolderName
        [Parameter(Mandatory)][string]$FolderName,     # the top folder inside the archive
        [Parameter(Mandatory)][string]$Archive,        # the .tar.gz to create
        [string[]]$Executables = @()                   # file names (leaf) that must be executable
    )

    $executableMode = [System.IO.UnixFileMode]493   # 0755 rwxr-xr-x
    $fileMode = [System.IO.UnixFileMode]420         # 0644 rw-r--r--
    $base = Join-Path $ParentFolder $FolderName
    $file = [System.IO.File]::Create($Archive)
    $gzip = [System.IO.Compression.GZipStream]::new($file, [System.IO.Compression.CompressionLevel]::Optimal)
    $writer = [System.Formats.Tar.TarWriter]::new($gzip, [System.Formats.Tar.TarEntryFormat]::Pax, $false)
    try {
        $items = @(Get-Item $base) + @(Get-ChildItem $base -Recurse | Sort-Object FullName)
        foreach ($item in $items) {
            $relative = [System.IO.Path]::GetRelativePath($ParentFolder, $item.FullName).Replace('\', '/')
            if ($item.PSIsContainer) {
                $entry = [System.Formats.Tar.PaxTarEntry]::new([System.Formats.Tar.TarEntryType]::Directory, "$relative/")
                $entry.Mode = $executableMode
                $writer.WriteEntry($entry)
                continue
            }

            $entry = [System.Formats.Tar.PaxTarEntry]::new([System.Formats.Tar.TarEntryType]::RegularFile, $relative)
            $entry.Mode = if ($Executables -contains $item.Name) { $executableMode } else { $fileMode }
            $stream = [System.IO.File]::OpenRead($item.FullName)
            try {
                $entry.DataStream = $stream
                $writer.WriteEntry($entry)
            }
            finally {
                $stream.Dispose()
            }
        }
    }
    finally {
        $writer.Dispose()
        $gzip.Dispose()
        $file.Dispose()
    }
}
