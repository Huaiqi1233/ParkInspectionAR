# 园区巡检AR标注 —— 三端共享契约 v0.1

> 本文件是 Unity(AR标记上报) → Go(存储) → React(管理展示) 三端的**唯一数据契约**。
> 任何一端新增/修改字段，必须先改本文件，再同步三端代码。

## 1. 核心实体 `Marker`（AR巡检标注）

```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "type": "hazard",
  "title": "3号配电箱外壳破损",
  "description": "箱体右下角变形，存在漏电风险",
  "position": { "x": 12.5, "y": 0.0, "z": -8.2 },
  "rotation": { "x": 0.0, "y": 0.7071, "z": 0.0, "w": 0.7071 },
  "geo": { "lat": 39.9042, "lng": 116.4074 },
  "status": "pending",
  "reporter": "张巡检",
  "photoUrl": null,
  "createdAt": "2026-08-29T06:40:00+08:00",
  "updatedAt": "2026-08-29T06:40:00+08:00"
}
```

### 字段说明

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `id` | string (UUID) | 服务端生成 | 避免客户端并发冲突；Unity 上报时**不带**此字段 |
| `type` | enum | ✅ | `equipment` 设备 / `hazard` 隐患 / `route_point` 巡检点 / `other` |
| `title` | string | ✅ | 标注标题，≤64字符 |
| `description` | string | ❌ | 补充描述，可为空字符串 |
| `position` | {x,y,z} (float) | ✅ | **AR会话空间坐标**（相对园区原点），非真实经纬度 |
| `rotation` | 四元数 {x,y,z,w} (float) | ✅ | AR Foundation Pose 原始输出，用于还原摆放姿态 |
| `geo` | {lat,lng} (double) 或 `null` | ❌ | 可选 GPS；Unity 用 LocationService 读取，读不到传 `null` |
| `status` | enum | 服务端默认 | `pending` 待处理 / `processing` 处理中 / `resolved` 已解决 / `closed` 关闭 |
| `reporter` | string | ✅ | 巡检员名，≤32字符 |
| `photoUrl` | string 或 `null` | ❌ | 现场照片 URL，原型阶段固定 `null` |
| `createdAt` | string (RFC3339) | 服务端生成 | 如 `2026-08-29T06:40:00+08:00` |
| `updatedAt` | string (RFC3339) | 服务端生成 | 每次更新重写 |

### 状态机

```
pending(待处理) ──> processing(处理中) ──> resolved(已解决)
      └──────────────────────> closed(关闭)
```

- `PATCH` 修改 `status` 时按上述方向流转，反向跳转（如 resolved→pending）由服务端校验拦截（原型阶段仅警告，不强制）。

### 响应信封（三端统一）

```
成功: HTTP 200/201 + {"code":0, "message":"ok", "data": <实体或列表>}
失败: HTTP 4xx/5xx + {"code":<非0>, "message":"<人类可读错误>"}
```

- `code=0` 恒为成功；非 0 为业务/参数错误码，HTTP 状态码与之一致语义。
- React 端 Axios 拦截器统一解信封：`code !== 0` 一律走 Error Boundary / 全局错误提示。
- 列表响应 `data` 固定为 `{"total": <int>, "items": [Marker, ...]}`。

---

## 2. API 端点清单（Gin，基址 `http://localhost:8080`）

| Method | Path | Request | Response |
|---|---|---|---|
| `POST` | `/api/v1/markers` | MarkerCreate（无 id/status/时间） | `201` Marker |
| `GET` | `/api/v1/markers` | Query: `status`? `type`? `page=1` `pageSize=20` | `200` `{total, items[]}` |
| `GET` | `/api/v1/markers/:id` | — | `200` Marker / `404` |
| `PATCH` | `/api/v1/markers/:id` | 部分字段（`status`/`title`/`description`） | `200` Marker |
| `DELETE` | `/api/v1/markers/:id` | — | `204` 空 |
| `GET` | `/healthz` | — | `200` `{"status":"ok"}` |

### 请求/响应示例

**POST /api/v1/markers** —— Unity 上报新标注

```json
// Request
{
  "type": "hazard",
  "title": "3号配电箱外壳破损",
  "description": "箱体右下角变形，存在漏电风险",
  "position": { "x": 12.5, "y": 0.0, "z": -8.2 },
  "rotation": { "x": 0.0, "y": 0.7071, "z": 0.0, "w": 0.7071 },
  "geo": { "lat": 39.9042, "lng": 116.4074 },
  "reporter": "张巡检"
}

// Response 201
{
  "code": 0,
  "message": "ok",
  "data": {
    "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    "type": "hazard",
    "title": "3号配电箱外壳破损",
    "description": "箱体右下角变形，存在漏电风险",
    "position": { "x": 12.5, "y": 0.0, "z": -8.2 },
    "rotation": { "x": 0.0, "y": 0.7071, "z": 0.0, "w": 0.7071 },
    "geo": { "lat": 39.9042, "lng": 116.4074 },
    "status": "pending",
    "reporter": "张巡检",
    "photoUrl": null,
    "createdAt": "2026-08-29T06:40:00+08:00",
    "updatedAt": "2026-08-29T06:40:00+08:00"
  }
}
```

**GET /api/v1/markers?status=pending&type=hazard&page=1&pageSize=20** —— 管理端列表

```json
// Response 200
{
  "code": 0,
  "message": "ok",
  "data": {
    "total": 1,
    "items": [ /* Marker 数组，同上结构 */ ]
  }
}
```

**PATCH /api/v1/markers/f47ac10b-58cc-4372-a567-0e02b2c3d479** —— 管理端改状态

```json
// Request（部分字段即可）
{ "status": "processing" }
```

**GET /api/v1/markers/:id** —— 不存在时

```json
// Response 404
{ "code": 40401, "message": "marker not found: f47ac10b-58cc-4372-a567-0e02b2c3d479" }
```

### 错误码约定

| HTTP | code | 含义 |
|---|---|---|
| 400 | 40001 | 参数/字段校验失败（message 指明具体字段） |
| 404 | 40401 | 资源不存在 |
| 405 | 40501 | 方法不允许 |
| 500 | 50001 | 服务端内部错误（SQLite 写入失败等） |

---

## 3. SQLite 存储（Go 端）

- 驱动：`modernc.org/sqlite`（纯 Go，免 CGO，Windows 可直接编译）。
- 数据文件：`park-inspection.db`，放 Go 服务运行目录，单文件持久化，重启不丢。

```sql
CREATE TABLE IF NOT EXISTS markers (
  id          TEXT PRIMARY KEY,             -- UUID，服务端生成
  type        TEXT NOT NULL,                -- equipment|hazard|route_point|other
  title       TEXT NOT NULL,
  description TEXT NOT NULL DEFAULT '',
  pos_x REAL NOT NULL, pos_y REAL NOT NULL, pos_z REAL NOT NULL,  -- AR 空间坐标
  rot_x REAL NOT NULL, rot_y REAL NOT NULL,
  rot_z REAL NOT NULL, rot_w REAL NOT NULL,                        -- 四元数
  lat REAL, lng REAL,                       -- 可选 GPS，NULL 表示无
  status      TEXT NOT NULL DEFAULT 'pending',
  reporter    TEXT NOT NULL,
  photo_url   TEXT,                         -- 原型阶段恒 NULL
  created_at  TEXT NOT NULL,                -- RFC3339
  updated_at  TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_markers_status ON markers(status);
CREATE INDEX IF NOT EXISTS idx_markers_type   ON markers(type);
```

---

## 4. curl 自测示例（Go 接口验收用）

> Windows cmd 不支持单引号，以下示例用 PowerShell / Git Bash 执行；或把 `'...'` 换成 `"{...}"` 并转义内部双引号。

```bash
# 1) 健康检查
curl http://localhost:8080/healthz

# 2) 上报新标注（Unity 上报路径的等价调用）
curl -X POST http://localhost:8080/api/v1/markers \
  -H "Content-Type: application/json" \
  -d '{
    "type": "hazard",
    "title": "3号配电箱外壳破损",
    "description": "箱体右下角变形",
    "position": {"x": 12.5, "y": 0.0, "z": -8.2},
    "rotation": {"x": 0.0, "y": 0.7071, "z": 0.0, "w": 0.7071},
    "geo": {"lat": 39.9042, "lng": 116.4074},
    "reporter": "张巡检"
  }'

# 3) 列表（带筛选 + 分页）
curl "http://localhost:8080/api/v1/markers?status=pending&type=hazard&page=1&pageSize=20"

# 4) 详情（把 id 换成第2步返回的 id）
curl http://localhost:8080/api/v1/markers/f47ac10b-58cc-4372-a567-0e02b2c3d479

# 5) 更新状态（管理端流转）
curl -X PATCH http://localhost:8080/api/v1/markers/f47ac10b-58cc-4372-a567-0e02b2c3d479 \
  -H "Content-Type: application/json" \
  -d '{"status": "processing"}'

# 6) 删除
curl -X DELETE http://localhost:8080/api/v1/markers/f47ac10b-58cc-4372-a567-0e02b2c3d479

# 7) 非法参数验证（应返回 400 + code 40001）
curl -X POST http://localhost:8080/api/v1/markers \
  -H "Content-Type: application/json" \
  -d '{"type": "unknown_type", "title": "x"}'
```

---

## 5. Unity 端交互与射线检测约定（实现时对齐）

```
Touch 开始
 └─ EventSystem.IsPointerOverGameObject(fingerId) == true ?   # UI 点击，忽略，不发射 AR 射线
 └─ false → ARRaycastManager.Raycast(touchPos, hits, TrackableType.PlaneWithinPolygon)
     └─ 命中平面 → 取 hit.pose → 放置「标注预览体」(Cube + 标题牌，半透明)
         └─ 面板选 type / 填 title → 「确认上报」→ UnityWebRequest POST /api/v1/markers
             └─ code==0：预览体转实体色，提示"已上报"
             └─ 否则：toast 失败原因 + 重试按钮（本地缓存最后一条未上报数据）
```

- **必须用 `IsPointerOverGameObject(fingerId)` 带触点参数**：无参重载仅编辑器有效，真机一律返回 false，会穿透 UI 误发射线。
- AR 射线只检测 `PlaneWithinPolygon`，忽略平面外区域与点云。
- 坐标语义：`position/rotation` 取 AR 会话空间值（相对园区原点），由现场标定原点后才有绝对意义，原型阶段原样上报即可。

---

## 6. 三端实现检查清单（后续开发逐项对照）

- [ ] **Go**：`main.go`（Gin + SQLite + 6 端点 + 信封）+ `docs` 内 curl 全通过
- [ ] **Unity**：AR Foundation 5.x 工程，UI/AR 点击区分、放置预览、POST 上报、失败重试
- [ ] **React**：TS + Axios 拦截器解信封，列表/筛选/状态流转页面，Error Boundary 兜底后端宕机
- [ ] 闭环验收：Unity 上报 → Go 落库 → React 可见并改状态 → curl 复核 SQLite 数据
