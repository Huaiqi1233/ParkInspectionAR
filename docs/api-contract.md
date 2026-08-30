# 园区破损上报 —— 三端共享契约 v2.0

> 本文件是 Unity(AR标记上报) → Go(存储) → React(管理展示) 三端的**唯一数据契约**。
> 任何一端新增/修改字段，必须先改本文件，再同步三端代码。
> v2.0 严格对齐任务书：priority 字段、status 三态、position 仅 x/y/z、去除 rotation/geo/reporter/photoUrl。

## 1. 核心实体 `Marker`（园区破损上报标记）

```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "title": "3号楼前地面破损",
  "description": "地面有约 30cm 裂缝，存在绊倒风险",
  "priority": "high",
  "status": "open",
  "position": { "x": 12.5, "y": 0.0, "z": -8.2 },
  "createdAt": "2026-08-29T06:40:00+08:00",
  "updatedAt": "2026-08-29T06:40:00+08:00"
}
```

### 字段说明

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `id` | string (UUID) | 服务端生成 | 为新问题生成唯一 ID（任务书 3.3）；Unity 上报时**不带**此字段 |
| `title` | string | ✅ | 标题，≤64 字符 |
| `description` | string | ✅ | 描述，≤256 字符（任务书表单要求：标题、描述） |
| `priority` | enum | ✅ | `high` / `medium` / `low`（任务书 3.1 表单要求优先级） |
| `status` | enum | 服务端默认 `open` | `open` / `in_progress` / `resolved`（任务书 3.2 状态三态） |
| `position` | {x,y,z} (float) | ✅ | AR 会话空间坐标（任务书：位置 x/y/z；仅三个分量，无 rotation） |
| `createdAt` | string (RFC3339) | 服务端生成 | 如 `2026-08-29T06:40:00+08:00` |
| `updatedAt` | string (RFC3339) | 服务端生成 | 每次更新重写 |

### 状态机（任务书 3.2：修改状态）

```
open(待处理) ──> in_progress(处理中) ──> resolved(已解决)
```

- `PATCH` 修改 `status` 时按上述方向流转；反向跳转由服务端校验拦截（原型阶段仅警告）。

### 响应信封（三端统一）

```
成功: HTTP 200/201 + {"code":0, "message":"ok", "data": <实体或列表>}
失败: HTTP 4xx/5xx + {"code":<非0>, "message":"<人类可读错误>"}
```

- `code=0` 恒为成功；非 0 为业务/参数错误码。
- 列表响应 `data` 固定为 `{"total": <int>, "items": [Marker, ...]}`。

---

## 2. API 端点清单（Gin，基址 `http://<host>:8080`）

| Method | Path | Request | Response |
|---|---|---|---|
| `POST` | `/api/v1/markers` | MarkerCreate（无 id/status/时间） | `201` Marker |
| `GET` | `/api/v1/markers` | Query: `status`? `priority`? `page=1` `pageSize=20` | `200` `{total, items[]}` |
| `GET` | `/api/v1/markers/:id` | — | `200` Marker / `404` |
| `PATCH` | `/api/v1/markers/:id` | 部分字段（`status`/`title`/`description`/`priority`） | `200` Marker |
| `DELETE` | `/api/v1/markers/:id` | — | `204` 空 |
| `GET` | `/healthz` | — | `200` `{"status":"ok"}` |

### 请求/响应示例

**POST /api/v1/markers** —— Unity 上报新标记

```json
// Request
{
  "title": "3号楼前地面破损",
  "description": "地面有约 30cm 裂缝",
  "priority": "high",
  "position": { "x": 12.5, "y": 0.0, "z": -8.2 }
}

// Response 201
{
  "code": 0,
  "message": "ok",
  "data": {
    "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    "title": "3号楼前地面破损",
    "description": "地面有约 30cm 裂缝",
    "priority": "high",
    "status": "open",
    "position": { "x": 12.5, "y": 0.0, "z": -8.2 },
    "createdAt": "2026-08-29T06:40:00+08:00",
    "updatedAt": "2026-08-29T06:40:00+08:00"
  }
}
```

**GET /api/v1/markers?status=open&priority=high&page=1&pageSize=20** —— 管理端列表

```json
// Response 200
{ "code": 0, "message": "ok", "data": { "total": 1, "items": [ /* Marker 数组 */ ] } }
```

**PATCH /api/v1/markers/:id** —— 管理端改状态（open → in_progress）

```json
// Request
{ "status": "in_progress" }
```

### 错误码约定

| HTTP | code | 含义 |
|---|---|---|
| 400 | 40001 | 参数/字段校验失败 |
| 404 | 40401 | 资源不存在 |
| 500 | 50001 | 服务端内部错误 |

---

## 3. SQLite 存储（Go 端）

- 驱动：`modernc.org/sqlite`（纯 Go，免 CGO，Windows 可直接编译）。
- 数据文件：`park-inspection.db`，单文件持久化，重启不丢。
- **防重放（任务书 3.3）**：`title + description + position` 三个字段的哈希存唯一索引，同一位置同一描述的重复上报会被拒绝（返回 409）。

```sql
CREATE TABLE IF NOT EXISTS markers (
  id          TEXT PRIMARY KEY,             -- UUID，服务端生成
  title       TEXT NOT NULL,
  description TEXT NOT NULL,
  priority    TEXT NOT NULL,                -- high|medium|low
  pos_x REAL NOT NULL, pos_y REAL NOT NULL, pos_z REAL NOT NULL,
  status      TEXT NOT NULL DEFAULT 'open',
  dedup_hash  TEXT NOT NULL UNIQUE,         -- 防重放：title+description+position 的哈希
  created_at  TEXT NOT NULL,
  updated_at  TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_markers_status ON markers(status);
CREATE INDEX IF NOT EXISTS idx_markers_priority ON markers(priority);
```

---

## 4. curl 自测示例

```bash
# 1) 健康检查
curl http://localhost:8080/healthz

# 2) 上报新标记
curl -X POST http://localhost:8080/api/v1/markers \
  -H "Content-Type: application/json; charset=utf-8" \
  -d '{"title":"3号楼前地面破损","description":"地面有约30cm裂缝","priority":"high","position":{"x":12.5,"y":0.0,"z":-8.2}}'

# 3) 列表（带筛选 + 分页）
curl "http://localhost:8080/api/v1/markers?status=open&priority=high&page=1&pageSize=20"

# 4) 详情
curl http://localhost:8080/api/v1/markers/<id>

# 5) 更新状态 open→in_progress
curl -X PATCH http://localhost:8080/api/v1/markers/<id> \
  -H "Content-Type: application/json" -d '{"status":"in_progress"}'

# 6) 删除
curl -X DELETE http://localhost:8080/api/v1/markers/<id>

# 7) 防重放验证（重复 POST 相同内容应 409）
curl -X POST http://localhost:8080/api/v1/markers \
  -H "Content-Type: application/json; charset=utf-8" \
  -d '{"title":"3号楼前地面破损","description":"地面有约30cm裂缝","priority":"high","position":{"x":12.5,"y":0.0,"z":-8.2}}'
```

---

## 5. Unity 端交互约定（任务书 3.1 必做）

```
Touch 开始
 └─ EventSystem.IsPointerOverGameObject(fingerId) == true ?  # UI 点击，忽略
 └─ false → ARRaycastManager.Raycast(touchPos, hits, TrackableType.PlaneWithinPolygon)
     └─ 命中平面 → 取 hit.pose → 放置预览体
         └─ 表单：标题 / 描述 / 优先级(high/medium/low) / 位置(自动取 pose 的 x/y/z)
             └─ 「提交」→ UnityWebRequest POST
                 ├─ code==0：预览体转实体色，标记可点击查看输入
                 └─ 失败：toast + 重试
 └─ 点击已放置的标记 → 弹出显示该标记的标题/描述/优先级/位置
```

---

## 6. 三端实现检查清单（对齐任务书）

- [ ] **Go**：6 端点 + healthz + 防重放（dedup_hash 唯一索引）+ status/priority 枚举
- [ ] **Unity**：真机运行 + 检测水平面 + 点击放置 + 表单(标题/描述/优先级/位置) + 提交 + 点击标记查看
- [ ] **React**：列表(标题/优先级/状态) + 修改状态(open/in_progress/resolved) + 后端不可用提示(Error Boundary)
- [ ] **交付**：源码 + README + AI_log.md
