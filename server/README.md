# park-inspection/server —— 标注存储 API

## 启动

```powershell
# 前置：Go 便携版（%USERPROFILE%\go-sdk\go\bin）或系统 Go ≥ 1.22
$env:PATH = "$env:USERPROFILE\go-sdk\go\bin;" + $env:PATH
$env:CGO_ENABLED = '0'
cd server
go build ./...
.\server.exe            # 监听 :8080，SQLite 落盘 park-inspection.db
```

## 验收

```powershell
powershell -ExecutionPolicy Bypass -File ..\scripts\acceptance.ps1
```

## curl 示例（契约第 4 节）

见 `docs/api-contract.md` 第 4 节（healthz / POST / GET 列表 / GET 详情 / PATCH / DELETE / 非法参数）。
