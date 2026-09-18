param(
    [Parameter(Mandatory=$true)][string]$OutPath
)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$bitmap.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()

# NOTE: an earlier version of this script picked a JPEG encoder with a tuned
# Quality parameter (System.Drawing.Imaging.Encoder / EncoderParameters) to cut
# file size. Windows Defender's AMSI flagged that exact pattern as malicious
# ("ScriptContainedMaliciousContent", reproduced twice) even though the plain
# PNG save below never was. Deliberately kept simple and unflagged — see
# README "מה למדנו" before reintroducing quality/codec tuning here.
