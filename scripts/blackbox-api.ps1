# 黑盒测试：Go 后端 API + React 代理（不读源码，纯接口黑盒）
# 用法：powershell -ExecutionPolicy Bypass -File scripts/blackbox-api.ps1
# 注意：会创建 BT- 前缀的测试数据，结束后自动清理；不动已有数据。
$ErrorActionPreference = 'Stop'
$base = 'http://localhost:8080'
$proxy = 'http://localhost:5173'
$script:pass = 0
$script:fail = 0
$script:testMarkers = @()

function Assert([bool]$cond, [string]$msg) {
  if ($cond) { Write-Host "  PASS  $msg" -ForegroundColor Green; $script:pass++ }
  else { Write-Host "  FAIL  $msg" -ForegroundColor Red; $script:fail++ }
}

function Post-Json([string]$url, [string]$json) {
  $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
  return Invoke-RestMethod -Method Post $url -ContentType 'application/json; charset=utf-8' -Body $bytes
}

function StatusOf($e) {
  try {
    $resp = $e.Response
    if ($null -ne $resp -and $null -ne $resp.StatusCode) { return [int]$resp.StatusCode }
  } catch {}
  return -1
}

try {
  # ============ A. Go 后端 API ============
  Write-Host '===== A. Go 后端 API ====='

  Write-Host '-- A1 /healthz --'
  $h = Invoke-RestMethod "$base/healthz"
  Assert ($h.status -eq 'ok') 'GET /healthz 返回 {"status":"ok"}'

  Write-Host '-- A2 POST 正常上报（含描述+GPS）--'
  $r2 = Post-Json "$base/api/v1/markers" '{"title":"BT-A2 正常上报","description":"黑盒测试描述","priority":"high","position":{"x":1.5,"y":2.5,"z":3.5},"location":{"lat":31.23,"lng":121.47}}'
  Assert ($r2.code -eq 0) '信封 code=0'
  Assert ($r2.data.id -ne '') '生成了非空唯一 id'
  Assert ($r2.data.status -eq 'open') '默认状态 open'
  Assert ($r2.data.priority -eq 'high') 'priority=high'
  Assert ($r2.data.location.lat -eq 31.23 -and $r2.data.location.lng -eq 121.47) 'location 经纬度保存正确'
  $script:testMarkers += $r2.data.id

  Write-Host '-- A3 POST 标题为空 -> 400 --'
  try { Post-Json "$base/api/v1/markers" '{"title":"","description":"x","priority":"high","position":{"x":0,"y":0,"z":0}}' | Out-Null; Assert $false '标题为空应被拒' }
  catch { Assert ((StatusOf $_.Exception) -eq 400) '标题为空返回 400' }

  Write-Host '-- A4 POST 非法 priority -> 400 --'
  try { Post-Json "$base/api/v1/markers" '{"title":"BT-A4","description":"x","priority":"urgent","position":{"x":0,"y":0,"z":0}}' | Out-Null; Assert $false '非法 priority 应被拒' }
  catch { Assert ((StatusOf $_.Exception) -eq 400) '非法 priority 返回 400' }

  Write-Host '-- A5 POST 描述为空（可选）-> 成功 --'
  $r5 = Post-Json "$base/api/v1/markers" '{"title":"BT-A5 描述可选","description":"","priority":"low","position":{"x":0,"y":0,"z":0}}'
  Assert ($r5.code -eq 0 -and $r5.data.description -eq '') '空描述提交成功且存为空串'
  $script:testMarkers += $r5.data.id

  Write-Host '-- A6 POST 非法 location(lat=200) -> 400 --'
  try { Post-Json "$base/api/v1/markers" '{"title":"BT-A6","description":"x","priority":"low","position":{"x":0,"y":0,"z":0},"location":{"lat":200,"lng":0}}' | Out-Null; Assert $false '非法经纬度应被拒' }
  catch { Assert ((StatusOf $_.Exception) -eq 400) '非法 location 返回 400' }

  Write-Host '-- A7 GET 列表字段齐全 --'
  $r7 = Invoke-RestMethod "$base/api/v1/markers?page=1&pageSize=20"
  Assert ($r7.code -eq 0) '列表信封 code=0'
  Assert ($r7.data.total -ge 1) "列表 total>=1（当前 $($r7.data.total)）"
  $first = $r7.data.items[0]
  Assert ($null -ne $first.id -and $null -ne $first.title -and $null -ne $first.status -and $null -ne $first.position) '条目含 id/title/status/position'
  Assert ($null -ne $first.location) '条目含 location 字段'

  Write-Host '-- A8 GET 详情 --'
  $r8 = Invoke-RestMethod "$base/api/v1/markers/$($r2.data.id)"
  Assert ($r8.code -eq 0 -and $r8.data.id -eq $r2.data.id) '按 id 查详情一致'

  Write-Host '-- A9 PATCH 状态流转 open->in_progress->resolved --'
  $p1 = Invoke-RestMethod -Method Patch "$base/api/v1/markers/$($r2.data.id)" -ContentType 'application/json' -Body '{"status":"in_progress"}'
  Assert ($p1.data.status -eq 'in_progress') '流转到 in_progress'
  $p2 = Invoke-RestMethod -Method Patch "$base/api/v1/markers/$($r2.data.id)" -ContentType 'application/json' -Body '{"status":"resolved"}'
  Assert ($p2.data.status -eq 'resolved') '流转到 resolved'

  Write-Host '-- A10 PATCH 非法 status -> 400 --'
  try { Invoke-RestMethod -Method Patch "$base/api/v1/markers/$($r2.data.id)" -ContentType 'application/json' -Body '{"status":"done"}' | Out-Null; Assert $false '非法 status 应被拒' }
  catch { Assert ((StatusOf $_.Exception) -eq 400) '非法 status 返回 400' }

  Write-Host '-- A11 DELETE -> 204，再 GET -> 404 --'
  Invoke-RestMethod -Method Delete "$base/api/v1/markers/$($r5.data.id)" | Out-Null
  try { Invoke-RestMethod "$base/api/v1/markers/$($r5.data.id)" | Out-Null; Assert $false '删除后详情应 404' }
  catch { Assert ((StatusOf $_.Exception) -eq 404) '删除后详情返回 404' }

  Write-Host '-- A12 防重放：同内容二次 POST -> 409 --'
  $dupJson = '{"title":"BT-A12 防重放","description":"x","priority":"medium","position":{"x":7,"y":8,"z":9}}'
  $r12 = Post-Json "$base/api/v1/markers" $dupJson
  $script:testMarkers += $r12.data.id
  try { Post-Json "$base/api/v1/markers" $dupJson | Out-Null; Assert $false '重复上报应 409' }
  catch { Assert ((StatusOf $_.Exception) -eq 409) '重复上报返回 409' }

  Write-Host '-- A13 列表筛选 status=resolved --'
  $r13 = Invoke-RestMethod "$base/api/v1/markers?status=resolved&page=1&pageSize=20"
  $allResolved = ($r13.data.items | Where-Object { $_.status -ne 'resolved' }).Count -eq 0
  Assert ($allResolved) 'status 筛选结果全部为 resolved'

  # ============ B. React 代理（浏览器同源路径） ============
  Write-Host '===== B. React 代理 ====='
  $b1 = Invoke-RestMethod "$proxy/api/v1/markers?page=1&pageSize=20"
  Assert ($b1.code -eq 0) '经 Vite 代理 GET 列表 code=0'
  $b2 = Invoke-RestMethod -Method Patch "$proxy/api/v1/markers/$($r2.data.id)" -ContentType 'application/json' -Body '{"status":"open"}'
  Assert ($b2.data.status -eq 'open') '经代理 PATCH 状态流转到 open'

  Write-Host "`n===== 汇总：PASS=$script:pass  FAIL=$script:fail ====="
} finally {
  foreach ($id in $script:testMarkers) {
    try { Invoke-RestMethod -Method Delete "$base/api/v1/markers/$id" -TimeoutSec 5 | Out-Null } catch {}
  }
  Write-Host '测试数据已清理'
}
