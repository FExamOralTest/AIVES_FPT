param(
  [int]$SizeMB = 12,
  [int]$SessionId = 45,
  [int]$QuestionId = 37
)
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5000'

$s = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post `
  -Body (@{ identifier = 'demo-student@aives.local'; password = 'Demo@12345' } | ConvertTo-Json) `
  -ContentType 'application/json'
$token = $s.AccessToken

# --- Tao file webm gia lap (co EBML magic) ---
$size = $SizeMB * 1024 * 1024
$bytes = New-Object byte[] $size
$bytes[0] = 0x1a; $bytes[1] = 0x45; $bytes[2] = 0xdf; $bytes[3] = 0xa3
(New-Object Random 2026).NextBytes($bytes[4..($size - 1)])
$src = Join-Path $env:TEMP "chunk_test_${SizeMB}mb.webm"
[System.IO.File]::WriteAllBytes($src, $bytes)
"Source file : $((Get-Item $src).Length) bytes ($SizeMB MB)"

$chunkSize = 5 * 1024 * 1024
$totalChunks = [Math]::Ceiling($size / $chunkSize)

# --- INIT ---
$init = Invoke-RestMethod -Uri "$base/api/audio/chunked/init" -Method Post `
  -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' `
  -Body (@{
    uploadId    = "test-" + [Guid]::NewGuid().ToString("N")
    sessionId   = $SessionId
    questionId  = $QuestionId
    fileName    = "answer.webm"
    contentType = "audio/webm"
    totalSize   = $size
    chunkSize   = $chunkSize
    totalChunks = $totalChunks
  } | ConvertTo-Json)
"INIT        : uploadId=$($init.uploadId) status=$($init.status)"

# --- UPLOAD TUNG CHUNK (curl.exe de tao multipart) ---
for ($i = 0; $i -lt $totalChunks; $i++) {
  $off = $i * $chunkSize
  $len = [Math]::Min($chunkSize, $size - $off)
  $chunkFile = Join-Path $env:TEMP "chunkpart_$i.bin"
  $fs = [System.IO.File]::Create($chunkFile)
  $fs.Write($bytes, $off, $len)
  $fs.Close()

  $json = curl.exe -s -X POST "$base/api/audio/chunked/chunk" `
    -H "Authorization: Bearer $token" `
    -F "uploadId=$($init.uploadId)" `
    -F "chunkIndex=$i" `
    -F "chunkData=@$chunkFile;filename=chunk_$i.bin;type=application/octet-stream" | ConvertFrom-Json

  "  chunk $i : sent=$len bytes -> server has $($json.receivedChunks.Count)/$($json.totalChunks), isComplete=$($json.isComplete)"
  Remove-Item $chunkFile -ErrorAction SilentlyContinue
}

# --- COMPLETE ---
$done = Invoke-RestMethod -Uri "$base/api/audio/chunked/complete" -Method Post `
  -Headers @{ Authorization = "Bearer $token" } -ContentType 'application/json' `
  -Body (@{ uploadId = $init.uploadId } | ConvertTo-Json)
"COMPLETE    : status=$($done.status)"
"  audioKey  : $($done.audioKey)"
"  localPath : $($init.localFilePath)"

@{ uploadId = $init.uploadId; audioKey = $done.audioKey; localPath = $init.localFilePath; sourceSize = $size } |
  ConvertTo-Json | Out-File "$env:TEMP\chunk_result.json" -Encoding utf8
