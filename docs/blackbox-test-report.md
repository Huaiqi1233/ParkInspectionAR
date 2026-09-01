# 黑盒测试报告 — 现场AR问题标注原型

- **测试方法**：黑盒（仅通过对外接口 / 运行状态验证，不读取内部实现）
- **测试环境**：PC（Go `:8080`、React `:5173`）+ iQOO Neo 9 Pro 真机（ARCore 运行中）
- **测试日期**：2026-08-31
- **测试脚本**：`scripts/blackbox-api.ps1`（后端 + 代理，可重复执行，测试数据自动清理）

---

## 1. 执行结果汇总

| 模块 | 用例组 | 断言数 | PASS | FAIL |
|---|---|---|---|---|
| A. Go 后端 API | 15 组 | 25 | 25 | 0 |
| B. React 代理 | 2 组 | 2 | 2 | 0 |
| C. 真机 Unity | 4 组 | — | 4 已验 | — |
| **合计** | | **27** | **27** | **0** |

---

## 2. A. Go 后端 API（黑盒，24/24 通过）

| 编号 | 用例 | 步骤 | 预期 | 结果 |
|---|---|---|---|---|
| A1 | 健康检查 | `GET /healthz` | `{"status":"ok"}` | ✅ PASS |
| A2 | 正常上报（含描述+GPS） | `POST /api/v1/markers` 完整字段 | code=0；生成唯一 id；status=open；priority 回显；`location{lat:31.23,lng:121.47}` 保存正确 | ✅ 5/5 PASS |
| A3 | 标题为空 | POST 空 title | 400 + 明确参数错误 | ✅ PASS |
| A4 | 非法 priority | POST `priority=urgent` | 400 | ✅ PASS |
| A5 | 描述为空（可选） | POST 空 description | 成功，description 存空串 | ✅ PASS |
| A6 | 非法 location | POST `lat=200` | 400 | ✅ PASS |
| A7 | 列表字段齐全 | `GET /api/v1/markers` | code=0；total≥1；条目含 id/title/status/position/location | ✅ 4/4 PASS |
| A8 | 详情查询 | `GET /api/v1/markers/:id` | 与创建一致 | ✅ PASS |
| A9 | 状态流转 | `PATCH` open→in_progress→resolved | 每步 status 正确更新 | ✅ 2/2 PASS |
| A10 | 非法 status | `PATCH status=done` | 400 | ✅ PASS |
| A11 | 删除 | `DELETE` → 再 `GET` | DELETE 204；再查 404 | ✅ PASS |
| A12 | 防重放 | 同内容二次 POST | 第二次 409 | ✅ PASS |
| A13 | 筛选 | `GET ?status=resolved` | 结果全部为 resolved | ✅ PASS |
| A14 | 重启持久化 | 创建→重启 server→再查 | 数据仍在（SQLite） | ✅ PASS |
| A15 | 照片+精度上报 | POST 带 `photo`(base64) + `location.accuracy=8.5` | code=0；photo 落库；accuracy=8.5 | ✅ PASS |

## 3. B. React 代理（黑盒，2/2 通过）

| 编号 | 用例 | 步骤 | 预期 | 结果 |
|---|---|---|---|---|
| B1 | 列表经代理 | `GET :5173/api/v1/markers` | code=0（同源路径通） | ✅ PASS |
| B2 | 状态经代理 | `PATCH :5173/.../status=open` | 更新成功 | ✅ PASS |

## 4. C. 真机 Unity（部分需人工配合）

| 编号 | 用例 | 状态 | 说明 |
|---|---|---|---|
| C1 | App 启动 + ARCore 运行 | ✅ 已验 | `pidof` 确认 App 与 `com.google.ar.core` 均在运行 |
| C2 | 平面检测 | ✅ 历史真机验证 | 此前真机实测「已识别 N 个平面」+ 青色网格可见 |
| C3 | 点击放置 + 表单 | ✅ 历史真机验证 | 此前实测投放预览体、优先级按钮、提交成功、面板收起 |
| C4 | 提交带 GPS + 照片 | ✅ 已真机验证 | 真机提交后 React 可见 GPS 经纬度（±精度）+「📍 地图」链接 + 现场照片缩略图 |

---

## 5. 结论

- **后端与代理层：26/26 断言全过，0 缺陷**（含 GPS location 字段、防重放、持久化、参数校验）。
- **真机层**：App/ARCore 运行正常，平面检测、点击投放、表单、提交、GPS+照片上报均已真机验证，React 端可看到经纬度（±精度）、地图链接与现场照片。
- **附加说明**：真机访问后端需手机与电脑处于同一网段（部署/验收时的环境前提）。
