# 园区破损上报 —— Go 后端验收脚本（契约 v2.0：priority + status 三态 + 防重放）
# 用法：powershell -ExecutionPolicy Bypass -File scripts/acceptance.ps1
# 通过条件：所有检查 PASS 且退出码 0。

$ErrorActionPreference = 'Stop'
$base = 'http://localhost:8080'
$serverDir = Join-Path $PSScriptRoot '..\server'
$dbFile = Join-Path $serverDir 'park-inspection.db'

function Assert-True($cond, $msg) {
  if ($cond) { Write-Host "  PASS: $msg" -ForegroundColor Green }
  else { Write-Host "  FAIL: $msg" -ForegroundColor Red; exit 1 }
}

# 1) 启动服务器（先清掉旧库保证幂等）
if (Test-Path $dbFile) { Remove-Item $dbFile -Force }
$env:CGO_ENABLED = '0'
$p = Start-Process -FilePath (Join-Path $serverDir 'server.exe') -WorkingDirectory $serverDir -PassThru -WindowStyle Hidden
try {
  Start-Sleep -Seconds 2

  Write-Host '== 1) /healthz =='
  $health = Invoke-RestMethod "$base/healthz"
  Assert-True ($health.status -eq 'ok') 'healthz 返回 {"status":"ok"}'

  Write-Host '== 2) POST 上报 =='
  $body = @{
    title = '3号楼前地面破损'
    description = '地面有约30cm裂缝'
    priority = 'high'
    position = @{ x = 12.5; y = 0.0; z = -8.2 }
  } | ConvertTo-Json -Depth 5
  # PowerShell 5.1 对 string body 默认 ASCII，中文会乱码；转 UTF-8 字节 + charset=utf-8
  $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($body)
  $created = Invoke-RestMethod -Method Post "$base/api/v1/markers" -ContentType 'application/json; charset=utf-8' -Body $bodyBytes
  Assert-True ($created.code -eq 0) 'POST 信封 code=0'
  Assert-True ($created.data.status -eq 'open') '新标记默认 status=open'
  Assert-True ($created.data.priority -eq 'high') 'priority=high 正确保存'
  $id = $created.data.id
  Assert-True ($id -ne '') '服务端生成了唯一 id'

  Write-Host '== 3) GET 列表 =='
  $list = Invoke-RestMethod "$base/api/v1/markers?page=1&pageSize=20"
  Assert-True ($list.code -eq 0 -and $list.data.total -ge 1) '列表 total>=1'

  Write-Host '== 4) GET 详情 =='
  $detail = Invoke-RestMethod "$base/api/v1/markers/$id"
  Assert-True ($detail.data.title -eq '3号楼前地面破损') '详情 title 一致'

  Write-Host '== 5) PATCH 状态流转 open->in_progress =='
  $patched = Invoke-RestMethod -Method Patch "$base/api/v1/markers/$id" -ContentType 'application/json' -Body '{"status":"in_progress"}'
  Assert-True ($patched.data.status -eq 'in_progress') 'status 流转到 in_progress'

  Write-Host '== 6) 防重放：重复上报相同内容应 409 =='
  try {
    Invoke-RestMethod -Method Post "$base/api/v1/markers" -ContentType 'application/json; charset=utf-8' -Body $bodyBytes | Out-Null
    Assert-True $false '重复上报应报 409'
  } catch {
    Assert-True ($_.Exception.Response.StatusCode.value__ -eq 409) '重复上报返回 409（防重放生效）'
  }

  Write-Host '== 7) 非法 priority 400 =='
  try {
    Invoke-RestMethod -Method Post "$base/api/v1/markers" -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes('{"title":"x","description":"y","priority":"urgent","position":{"x":1,"y":2,"z":3}}')) | Out-Null
    Assert-True $false '非法 priority 应报 400'
  } catch {
    Assert-True ($_.Exception.Response.StatusCode.value__ -eq 400) '非法 priority 返回 400'
  }

  # 8) 重启持久化
  Write-Host '== 8) 重启持久化 =='
  Stop-Process -Id $p.Id -Force; $p.WaitForExit()
  $p2 = Start-Process -FilePath (Join-Path $serverDir 'server.exe') -WorkingDirectory $serverDir -PassThru -WindowStyle Hidden
  Start-Sleep -Seconds 2
  $after = Invoke-RestMethod "$base/api/v1/markers/$id"
  Assert-True ($after.data.status -eq 'in_progress') '重启后数据仍在（SQLite 持久化）'
  Stop-Process -Id $p2.Id -Force; $p2.WaitForExit()

  Write-Host '== 9) DELETE 删除 =='
  $p3 = Start-Process -FilePath (Join-Path $serverDir 'server.exe') -WorkingDirectory $serverDir -PassThru -WindowStyle Hidden
  Start-Sleep -Seconds 2
  Invoke-RestMethod -Method Delete "$base/api/v1/markers/$id" | Out-Null
  try {
    Invoke-RestMethod "$base/api/v1/markers/$id" | Out-Null
    Assert-True $false '删除后详情应 404'
  } catch {
    Assert-True ($_.Exception.Response.StatusCode.value__ -eq 404) '删除后详情返回 404'
  }
  Stop-Process -Id $p3.Id -Force; $p3.WaitForExit()

  Write-Host "`nALL CHECKS PASSED ✔" -ForegroundColor Green
} finally {
  if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
}
