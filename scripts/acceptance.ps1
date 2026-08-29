# 园区巡检 AR 标注 —— Go 后端验收脚本（契约第 4 节 7 条场景 + 重启持久化）
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
    type = 'hazard'; title = '3号配电箱外壳破损'
    description = '箱体右下角变形，存在漏电风险'
    position = @{ x = 12.5; y = 0.0; z = -8.2 }
    rotation = @{ x = 0.0; y = 0.7071; z = 0.0; w = 0.7071 }
    geo = @{ lat = 39.9042; lng = 116.4074 }
    reporter = '张巡检'
  } | ConvertTo-Json -Depth 5
  # 关键：PowerShell 5.1 对 string body 默认按 ASCII 编码发送，中文会变乱码；
  # 必须显式转 UTF-8 字节数组 + charset=utf-8，服务端才能正确解析中文
  $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($body)
  $created = Invoke-RestMethod -Method Post "$base/api/v1/markers" -ContentType 'application/json; charset=utf-8' -Body $bodyBytes
  Assert-True ($created.code -eq 0) 'POST 信封 code=0'
  Assert-True ($created.data.status -eq 'pending') '新标注默认 status=pending'
  $id = $created.data.id
  Assert-True ($id -ne '') '服务端生成了 id'

  Write-Host '== 3) GET 列表 =='
  $list = Invoke-RestMethod "$base/api/v1/markers?page=1&pageSize=20"
  Assert-True ($list.code -eq 0 -and $list.data.total -ge 1) '列表 total>=1'

  Write-Host '== 4) GET 详情 =='
  $detail = Invoke-RestMethod "$base/api/v1/markers/$id"
  Assert-True ($detail.data.title -eq '3号配电箱外壳破损') '详情 title 一致'

  Write-Host '== 5) PATCH 状态流转 =='
  $patched = Invoke-RestMethod -Method Patch "$base/api/v1/markers/$id" -ContentType 'application/json' -Body '{"status":"processing"}'
  Assert-True ($patched.data.status -eq 'processing') 'status 流转到 processing'

  Write-Host '== 6) 非法参数 400 =='
  try {
    Invoke-RestMethod -Method Post "$base/api/v1/markers" -ContentType 'application/json' -Body '{"type":"unknown_type","title":"x"}' | Out-Null
    Assert-True $false '非法 type 应报 40001'
  } catch {
    Assert-True ($_.Exception.Response.StatusCode.value__ -eq 400) '非法 type 返回 400'
  }

  # 7) 重启持久化：停服 → 重启 → 数据仍在
  Write-Host '== 7) 重启持久化 =='
  Stop-Process -Id $p.Id -Force; $p.WaitForExit()
  $p2 = Start-Process -FilePath (Join-Path $serverDir 'server.exe') -WorkingDirectory $serverDir -PassThru -WindowStyle Hidden
  Start-Sleep -Seconds 2
  $after = Invoke-RestMethod "$base/api/v1/markers/$id"
  Assert-True ($after.data.status -eq 'processing') '重启后数据仍在（SQLite 持久化）'
  Stop-Process -Id $p2.Id -Force; $p2.WaitForExit()

  Write-Host '== 8) DELETE 删除 =='
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
  # 兜底：无论成败都停掉服务器，避免占用 8080
  if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
}
