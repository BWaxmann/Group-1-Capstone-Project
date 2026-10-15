# Records placeholder announcer lines with a built-in Windows voice (Zira or David), one WAV per line in
# Assets/Resources/Announcer/lines.txt. Swap the WAVs for real recordings later (same names), no code changes needed.
#   powershell -File Tools/make-announcer-voice.ps1                 (Zira, every line)
#   powershell -File Tools/make-announcer-voice.ps1 -Voice David    (the other voice)
#   powershell -File Tools/make-announcer-voice.ps1 -Only boss_fed  (just one line)
param(
    [ValidateSet('Zira', 'David')] [string]$Voice = 'Zira',
    [int]$Rate = 1,   # -10 (slow) to 10 (fast)
    [string]$Only = ''
)

$folder = Join-Path $PSScriptRoot '..\Assets\Resources\Announcer'
$linesFile = Join-Path $folder 'lines.txt'
Add-Type -AssemblyName System.Speech
$speech = New-Object System.Speech.Synthesis.SpeechSynthesizer
$speech.SelectVoice("Microsoft $Voice Desktop")
$speech.Rate = $Rate
$format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)

$made = 0
foreach ($row in Get-Content $linesFile) {
    $row = $row.Trim()
    if ($row -eq '' -or $row.StartsWith('#')) { continue }
    $parts = $row.Split('|')
    if ($parts.Count -lt 2) { continue }
    $id = $parts[0].Trim()
    if ($Only -ne '' -and $id -ne $Only) { continue }
    $wav = Join-Path $folder "$id.wav"
    $speech.SetOutputToWaveFile($wav, $format)
    $speech.Speak($parts[1].Trim())
    $speech.SetOutputToNull()
    $made++
    "recorded $id"
}
$speech.Dispose()
"$made line(s) recorded with $Voice."
