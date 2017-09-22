[CmdletBinding()]
Param([Parameter(Mandatory=$true)][string]$Version)

$regex = [regex]"(?<=\d+\.)\d+(?=\.\d+)"
$match = $regex.Match($Version)
$MaxVersion = $regex.Replace($Version, ([int]$match.Value) + 1)

#[Version]$MaxVersion = "$($Version.Major).$($Version.Minor).$($Version.Build)"


foreach($nuspecFile in Get-ChildItem ".\**\*.nuspec") {
    [xml] $file = Get-Content $nuspecFile
    $metadata = $file.package.metadata
    $metadata.copyright = "Copyright " + (Get-Date).Year
    $metadata.version = $Version

    $autoDiDependency = $metadata.dependencies.dependency | Where-Object { $_.id -eq "AutoDI" } | Select-Object -First 1
    if ($autoDiDependency) {
        $autoDiDependency.version = "[$Version,$MaxVersion)"
    }

    $file.Save($nuspecFile)
}