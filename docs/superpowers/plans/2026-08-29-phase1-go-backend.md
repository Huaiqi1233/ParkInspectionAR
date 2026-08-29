# Phase 1: Go 后端实施计划（park-inspection/server）

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development (recommended) or executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 Gin + SQLite 的标注存储 API（6 端点 + healthz），7 条 curl 全通过、重启数据不丢。

**Architecture:** 单包 `main` 四文件（main/models/store/handlers），models 严格对齐契约 JSON，store 负责 SQLite 行↔结构体映射，handlers 负责校验与信封响应；`modernc.org/sqlite` 免 CGO，Windows 无 gcc 可编译。

**Tech Stack:** Go 1.22+ / Gin / modernc.org/sqlite / google/uuid；验收用 PowerShell + curl.exe 脚本（遵循确认书第 8 条：不引入单测框架）。

**Spec:** `docs/superpowers/specs/2026-08-29-三端开发顺序与接口契约确认书.md` + `docs/api-contract.md`（v0.1）

---

## Global Constraints

1. 关键代码含中文注释，解释"为什么"而非逐行翻译。
2. 每个端点 curl 自测；验收脚本 `scripts/acceptance.ps1` 必须全绿。
3. 严禁编造 Go/Gin/SQLite 库函数；不确定必须询问。
4. 不引入 Clean Architecture / 框架 / 单测库。
5. 响应信封 `{"code":0,"message":"ok","data":...}`；错误非 0 code + 4xx/5xx。
6. 错误码：40001 参数 / 40401 不存在 / 40501 方法 / 50001 内部。
7. SQLite 单文件 `park-inspection.db`，重启不丢。
8. 每任务结束 `git commit`，信息含任务编号。
9. Go 工具链：便携版 `%USERPROFILE%\go-sdk\go\bin`（免管理员）；所有构建设 `CGO_ENABLED=0`（无 gcc 环境，纯 Go 编译）。

---

### Task 1: 工程骨架 + 依赖 + /healthz

**Files:**
- Create: `server/go.mod`, `server/go.sum`
- Create: `server/main.go`（最小版：Gin + /healthz）

**Interfaces:**
- Consumes: Go 便携版工具链；`docs/api-contract.md` 的 healthz 契约（`{"status":"ok"}`，无信封）。
- Produces: 可启动的 `:8080` HTTP 服务与 `/healthz` 端点；后续任务在此基础上追加路由与 handler。

- [ ] **Step 1: 初始化模块并拉取依赖**

```powershell
# 在 PowerShell 会话中（go 便携版不在 PATH）
$env:PATH = "$env:USERPROFILE\go-sdk\go\bin;" + $env:PATH
$env:CGO_ENABLED = '0'
cd D:\桌面\小作业\server
go mod init park-inspection/server
go get github.com/gin-gonic/gin
go get modernc.org/sqlite
go get github.com/google/uuid
```

- [ ] **Step 2: 写最小 main.go**

`server/main.go`：

```go
package main

import (
	"log"

	"github.com/gin-gonic/gin"
)

func main() {
	// 为什么 ReleaseMode：原型对外服务，避免每请求打印调试日志刷屏
	gin.SetMode(gin.ReleaseMode)
	r := gin.Default()

	// 健康检查：React Error Boundary 探活 + 验收脚本前置检查；
	// 契约规定该端点返回 {"status":"ok"}，不走信封（与 api-contract.md 一致）
	r.GET("/healthz", func(c *gin.Context) {
		c.JSON(200, gin.H{"status": "ok"})
	})

	log.Println("park-inspection server listening on :8080")
	if err := r.Run(":8080"); err != nil {
		log.Fatalf("server exit: %v", err)
	}
}
```

- [ ] **Step 3: 编译验证**

```powershell
cd D:\桌面\小作业\server
$env:PATH = "$env:USERPROFILE\go-sdk\go\bin;" + $env:PATH
$env:CGO_ENABLED = '0'
go build ./...
```

Expected: 无输出、exit 0（生成 `server.exe` 于当前目录或临时缓存）。

- [ ] **Step 4: 启动 + curl 验证 /healthz**

```powershell
cd D:\桌面\小作业\server
$env:PATH = "$env:USERPROFILE\go-sdk\go\bin;" + $env:PATH
$p = Start-Process -FilePath ".\server.exe" -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
curl.exe -s http://localhost:8080/healthz
Stop-Process -Id $p.Id -Force
```

Expected: 输出 `{"status":"ok"}`。

- [ ] **Step 5: 提交**

```powershell
cd D:\桌面\小作业
git add server/go.mod server/go.sum server/main.go
git commit -m "feat(server): Task1 工程骨架 + /healthz"
```

---

### Task 2: models.go —— 契约结构体 + 信封

**Files:**
- Create: `server/models.go`

**Interfaces:**
- Consumes: `docs/api-contract.md` v0.1 实体字段（id/type/title/description/position/rotation/geo/status/reporter/photoUrl/createdAt/updatedAt）；`CreateMarkerRequest` 语义（POST 不带 id/status/时间）。
- Produces:
  - `type Marker struct{...}`（json tag 与契约完全一致；`Geo *Geo` / `PhotoURL *string` 指针保证 null 语义）
  - `type CreateMarkerRequest struct{...}`
  - `type Envelope struct{...}` + `func ok(data interface{}) Envelope` + `func fail(code int, msg string) Envelope`
  - 校验常量：`var validTypes = map[string]bool{...}`、`var validStatuses = map[string]bool{...}`
  - `func nowRFC3339() string`

- [ ] **Step 1: 写 models.go**

`server/models.go`：

```go
package main

import "time"

// Marker 三端共享实体。json tag 必须与 docs/api-contract.md v0.1 完全一致，
// 否则 Unity/React 解包字段名会错位。
type Marker struct {
	ID          string   `json:"id"`
	Type        string   `json:"type"`
	Title       string   `json:"title"`
	Description string   `json:"description"`
	Position    Position `json:"position"`
	Rotation    Rotation `json:"rotation"`
	Geo         *Geo     `json:"geo"`        // 指针：nil → JSON null（无 GPS），契约允许空
	Status      string   `json:"status"`
	Reporter    string   `json:"reporter"`
	PhotoURL    *string  `json:"photoUrl"`   // 原型恒 nil → null
	CreatedAt   string   `json:"createdAt"`
	UpdatedAt   string   `json:"updatedAt"`
}

// Position 对应契约 position{x,y,z}，AR 会话空间坐标
type Position struct {
	X float64 `json:"x"`
	Y float64 `json:"y"`
	Z float64 `json:"z"`
}

// Rotation 对应契约 rotation 四元数{x,y,z,w}
type Rotation struct {
	X float64 `json:"x"`
	Y float64 `json:"y"`
	Z float64 `json:"z"`
	W float64 `json:"w"`
}

// Geo 可选 GPS；lat/lng 必成对出现
type Geo struct {
	Lat float64 `json:"lat"`
	Lng float64 `json:"lng"`
}

// CreateMarkerRequest POST 入参：id/status/createdAt/updatedAt 由服务端生成，
// 客户端（Unity）只允许提交业务字段。
type CreateMarkerRequest struct {
	Type        string   `json:"type"`
	Title       string   `json:"title"`
	Description string   `json:"description"`
	Position    Position `json:"position"`
	Rotation    Rotation `json:"rotation"`
	Geo         *Geo     `json:"geo"`
	Reporter    string   `json:"reporter"`
}

// Envelope 三端统一响应信封。data 用 omitempty：失败响应不带 data 字段。
type Envelope struct {
	Code    int         `json:"code"`
	Message string      `json:"message"`
	Data    interface{} `json:"data,omitempty"`
}

// ok / fail 生成信封，code=0 恒成功（契约第 1 节）
func ok(data interface{}) Envelope   { return Envelope{Code: 0, Message: "ok", Data: data} }
func fail(code int, msg string) Envelope { return Envelope{Code: code, Message: msg} }

// 合法枚举白名单：非法 type/status 一律 40001
var validTypes = map[string]bool{
	"equipment":   true,
	"hazard":      true,
	"route_point": true,
	"other":       true,
}

var validStatuses = map[string]bool{
	"pending":    true,
	"processing": true,
	"resolved":   true,
	"closed":     true,
}

// nowRFC3339 统一时间格式（契约 createdAt/updatedAt 为 RFC3339）
func nowRFC3339() string { return time.Now().Format(time.RFC3339) }
```

- [ ] **Step 2: 编译验证**

```powershell
cd D:\桌面\小作业\server
$env:PATH = "$env:USERPROFILE\go-sdk\go\bin;" + $env:PATH
$env:CGO_ENABLED = '0'
go build ./...
```

Expected: exit 0。

- [ ] **Step 3: 提交**

```powershell
cd D:\桌面\小作业
git add server/models.go
git commit -m "feat(server): Task2 契约结构体 + 信封封装"
```

---

### Task 3: store.go —— SQLite 存储层

**Files:**
- Create: `server/store.go`

**Interfaces:**
- Consumes: `models.go` 的 `Marker/Position/Rotation/Geo`；`nowRFC3339()`；契约第 3 节 DDL。
- Produces:
  - `func OpenDB(path string) (*sql.DB, error)`（打开 + 建表 + 索引）
  - `type Store struct{ DB *sql.DB }`
  - `func (s *Store) Insert(m *Marker) error`
  - `func (s *Store) Get(id string) (Marker, error)`（无记录返回 `sql.ErrNoRows`）
  - `func (s *Store) List(status, typ string, page, pageSize int) ([]Marker, int, error)`（items, total）
  - `func (s *Store) Update(id string, fields map[string]interface{}) (Marker, error)`（白名单字段动态 SET，重写 updated_at）
  - `func (s *Store) Delete(id string) error`（无记录返回 `sql.ErrNoRows`）

- [ ] **Step 1: 写 store.go**

`server/store.go`：

```go
package main

import (
	"database/sql"
	"fmt"
	"strings"

	_ "modernc.org/sqlite" // 纯 Go 驱动：免 CGO，Windows 无 gcc 也能编译（确认书决策 7）
)

// Store 存储层：只负责 SQLite 行 ↔ Marker 结构体映射，不做 HTTP/校验逻辑
type Store struct {
	DB *sql.DB
}

// OpenDB 打开（不存在则创建）SQLite 单文件并建表。
// 为什么单文件：确认书 Global Constraints 6 —— 重启不丢数据且便于交付拷贝。
func OpenDB(path string) (*sql.DB, error) {
	db, err := sql.Open("sqlite", path)
	if err != nil {
		return nil, err
	}
	// 契约第 3 节 DDL：字段平铺，lat/lng/photo_url 可 NULL（geo/photoUrl 可空）
	_, err = db.Exec(`
CREATE TABLE IF NOT EXISTS markers (
	id          TEXT PRIMARY KEY,
	type        TEXT NOT NULL,
	title       TEXT NOT NULL,
	description TEXT NOT NULL DEFAULT '',
	pos_x REAL NOT NULL, pos_y REAL NOT NULL, pos_z REAL NOT NULL,
	rot_x REAL NOT NULL, rot_y REAL NOT NULL, rot_z REAL NOT NULL, rot_w REAL NOT NULL,
	lat REAL, lng REAL,
	status      TEXT NOT NULL DEFAULT 'pending',
	reporter    TEXT NOT NULL,
	photo_url   TEXT,
	created_at  TEXT NOT NULL,
	updated_at  TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_markers_status ON markers(status);
CREATE INDEX IF NOT EXISTS idx_markers_type ON markers(type);`)
	if err != nil {
		return nil, fmt.Errorf("migrate: %w", err)
	}
	return db, nil
}

// scanRow 把一行记录映射为 Marker；sql.Row 与 sql.Rows 都满足 scanner 接口。
// 为什么单独抽函数：Get/List 共用同一映射逻辑，避免列序写两遍出错。
type scanner interface{ Scan(dest ...interface{}) error }

func scanRow(row scanner) (Marker, error) {
	var m Marker
	var lat, lng sql.NullFloat64 // NULL → geo 为 nil（契约：geo 可空）
	var photo sql.NullString     // NULL → photoUrl 为 null
	err := row.Scan(
		&m.ID, &m.Type, &m.Title, &m.Description,
		&m.Position.X, &m.Position.Y, &m.Position.Z,
		&m.Rotation.X, &m.Rotation.Y, &m.Rotation.Z, &m.Rotation.W,
		&lat, &lng, &m.Status, &m.Reporter, &photo,
		&m.CreatedAt, &m.UpdatedAt,
	)
	if err != nil {
		return Marker{}, err
	}
	if lat.Valid && lng.Valid {
		m.Geo = &Geo{Lat: lat.Float64, Lng: lng.Float64}
	}
	if photo.Valid {
		p := photo.String
		m.PhotoURL = &p
	}
	return m, nil
}

// Insert 写入新标注。nil 指针字段以 nil 参数写入 → SQL NULL，保持可空语义。
func (s *Store) Insert(m *Marker) error {
	var lat, lng interface{}
	if m.Geo != nil {
		lat, lng = m.Geo.Lat, m.Geo.Lng
	}
	var photo interface{}
	if m.PhotoURL != nil {
		photo = *m.PhotoURL
	}
	_, err := s.DB.Exec(`
INSERT INTO markers
(id, type, title, description,
 pos_x, pos_y, pos_z,
 rot_x, rot_y, rot_z, rot_w,
 lat, lng, status, reporter, photo_url,
 created_at, updated_at)
VALUES (?,?,?,?, ?,?,?, ?,?,?,?, ?,?,?,?,?, ?,?)`,
		m.ID, m.Type, m.Title, m.Description,
		m.Position.X, m.Position.Y, m.Position.Z,
		m.Rotation.X, m.Rotation.Y, m.Rotation.Z, m.Rotation.W,
		lat, lng, m.Status, m.Reporter, photo,
		m.CreatedAt, m.UpdatedAt)
	return err
}

// Get 按 id 查询；无记录返回 sql.ErrNoRows（handler 据此映射 40401）
func (s *Store) Get(id string) (Marker, error) {
	row := s.DB.QueryRow(`
SELECT id,type,title,description,
       pos_x,pos_y,pos_z,
       rot_x,rot_y,rot_z,rot_w,
       lat,lng,status,reporter,photo_url,
       created_at,updated_at
FROM markers WHERE id = ?`, id)
	return scanRow(row)
}

// List 分页 + 可选过滤（status/type）。
// 为什么 WHERE 1=1：动态拼接 AND 条件时避免"首个条件前要不要 WHERE"的分支。
// 返回 (items, total, err)：total 供 React 分页器计算页数。
func (s *Store) List(status, typ string, page, pageSize int) ([]Marker, int, error) {
	cond := " WHERE 1=1"
	args := []interface{}{}
	if status != "" {
		cond += " AND status = ?"
		args = append(args, status)
	}
	if typ != "" {
		cond += " AND type = ?"
		args = append(args, typ)
	}

	var total int
	if err := s.DB.QueryRow("SELECT COUNT(*) FROM markers"+cond, args...).Scan(&total); err != nil {
		return nil, 0, err
	}

	// 分页：page 从 1 开始，offset=(page-1)*pageSize；按创建时间倒序（最新在前）
	rows, err := s.DB.Query(`
SELECT id,type,title,description,
       pos_x,pos_y,pos_z,
       rot_x,rot_y,rot_z,rot_w,
       lat,lng,status,reporter,photo_url,
       created_at,updated_at
FROM markers`+cond+` ORDER BY created_at DESC LIMIT ? OFFSET ?`,
		append(args, pageSize, (page-1)*pageSize)...)
	if err != nil {
		return nil, 0, err
	}
	defer rows.Close()

	items := []Marker{}
	for rows.Next() {
		m, err := scanRow(rows)
		if err != nil {
			return nil, 0, err
		}
		items = append(items, m)
	}
	return items, total, rows.Err()
}

// Update 部分更新：fields 只含 handler 白名单过滤后的键（status/title/description），
// 动态 SET + 参数化（防注入），并重写 updated_at。返回更新后的完整记录。
func (s *Store) Update(id string, fields map[string]interface{}) (Marker, error) {
	// 字段名→列名映射：二次防御，未知键直接忽略（handler 已过滤，这里兜底）
	colMap := map[string]string{
		"status":      "status",
		"title":       "title",
		"description": "description",
	}
	setCols := []string{}
	args := []interface{}{}
	for k, v := range fields {
		col, ok := colMap[k]
		if !ok {
			continue
		}
		setCols = append(setCols, col+" = ?")
		args = append(args, v)
	}
	if len(setCols) == 0 {
		// 无字段可更新（如空 body 或全是未知键）：直接返回现状
		return s.Get(id)
	}
	setCols = append(setCols, "updated_at = ?")
	args = append(args, nowRFC3339(), id)

	_, err := s.DB.Exec("UPDATE markers SET "+strings.Join(setCols, ", ")+" WHERE id = ?", args...)
	if err != nil {
		return Marker{}, err
	}
	return s.Get(id)
}

// Delete 按 id 删除；0 行受影响说明不存在 → sql.ErrNoRows（handler 映射 40401）
func (s *Store) Delete(id string) error {
	res, err := s.DB.Exec("DELETE FROM markers WHERE id = ?", id)
	if err != nil {
		return err
	}
	if n, _ := res.RowsAffected(); n == 0 {
		return sql.ErrNoRows
	}
	return nil
}
```

- [ ] **Step 2: 编译验证**

```powershell
cd D:\桌面\小作业\server
$env:PATH = "$env:USERPROFILE\go-sdk\go\bin;" + $env:PATH
$env:CGO_ENABLED = '0'
go build ./...
go vet ./...
```

Expected: 均 exit 0（数据行为在 Task 5 全量 curl 中验证）。

- [ ] **Step 3: 提交**

```powershell
cd D:\桌面\小作业
git add server/store.go
git commit -m "feat(server): Task3 SQLite 存储层"
```

---

### Task 4: handlers.go —— 六个端点 + 路由接线

**Files:**
- Create: `server/handlers.go`
- Modify: `server/main.go`（追加路由与 handler 注册）

**Interfaces:**
- Consumes: `Store`（Task 3）、`Marker/CreateMarkerRequest/Envelope/ok/fail/validTypes/validStatuses/nowRFC3339`（Task 2）、`sql.ErrNoRows`。
- Produces: `type Handlers struct{ Store *Store }` 及方法 `Health/CreateMarker/ListMarkers/GetMarker/UpdateMarker/DeleteMarker`，全部按契约信封响应；错误码 40001/40401/50001。

- [ ] **Step 1: 写 handlers.go**

`server/handlers.go`：

```go
package main

import (
	"database/sql"
	"net/http"
	"strconv"
	"strings"
	"unicode/utf8"

	"github.com/gin-gonic/gin"
	"github.com/google/uuid"
)

// Handlers HTTP 层：校验 + 调用 Store + 信封响应。不直接碰 SQL。
type Handlers struct {
	Store *Store
}

// Health /healthz（契约：{"status":"ok"}，无信封）
func (h *Handlers) Health(c *gin.Context) {
	c.JSON(http.StatusOK, gin.H{"status": "ok"})
}

// CreateMarker POST /api/v1/markers —— Unity 上报入口
func (h *Handlers) CreateMarker(c *gin.Context) {
	var req CreateMarkerRequest
	// ShouldBindJSON：body 非法 JSON 直接 40001，不落库
	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, fail(40001, "请求体不是合法 JSON: "+err.Error()))
		return
	}
	// 白名单校验：type 必须命中枚举；title/reporter 非空且限长
	if !validTypes[req.Type] {
		c.JSON(http.StatusBadRequest, fail(40001, "type 非法: "+req.Type))
		return
	}
	req.Title = strings.TrimSpace(req.Title)
	req.Reporter = strings.TrimSpace(req.Reporter)
	if req.Title == "" || utf8.RuneCountInString(req.Title) > 64 {
		c.JSON(http.StatusBadRequest, fail(40001, "title 必填且不超过 64 字符"))
		return
	}
	if req.Reporter == "" || utf8.RuneCountInString(req.Reporter) > 32 {
		c.JSON(http.StatusBadRequest, fail(40001, "reporter 必填且不超过 32 字符"))
		return
	}

	now := nowRFC3339()
	m := &Marker{
		ID:          uuid.NewString(), // 服务端生成：避免客户端并发冲突（契约决策）
		Type:        req.Type,
		Title:       req.Title,
		Description: strings.TrimSpace(req.Description),
		Position:    req.Position,
		Rotation:    req.Rotation,
		Geo:         req.Geo,
		Status:      "pending", // 新标注默认待处理（契约状态机起点）
		Reporter:    req.Reporter,
		CreatedAt:   now,
		UpdatedAt:   now,
	}
	if err := h.Store.Insert(m); err != nil {
		c.JSON(http.StatusInternalServerError, fail(50001, "写入失败: "+err.Error()))
		return
	}
	c.JSON(http.StatusCreated, ok(m))
}

// ListMarkers GET /api/v1/markers?status=&type=&page=&pageSize= —— React 列表
func (h *Handlers) ListMarkers(c *gin.Context) {
	// 分页参数：非法值回退默认（page=1, pageSize=20），不报错——容错优先
	page, _ := strconv.Atoi(c.DefaultQuery("page", "1"))
	pageSize, _ := strconv.Atoi(c.DefaultQuery("pageSize", "20"))
	if page < 1 {
		page = 1
	}
	if pageSize < 1 || pageSize > 100 {
		pageSize = 20
	}
	items, total, err := h.Store.List(c.Query("status"), c.Query("type"), page, pageSize)
	if err != nil {
		c.JSON(http.StatusInternalServerError, fail(50001, "查询失败: "+err.Error()))
		return
	}
	// 契约：列表 data 固定为 {total, items}
	c.JSON(http.StatusOK, ok(gin.H{"total": total, "items": items}))
}

// GetMarker GET /api/v1/markers/:id —— 详情
func (h *Handlers) GetMarker(c *gin.Context) {
	m, err := h.Store.Get(c.Param("id"))
	if err == sql.ErrNoRows {
		c.JSON(http.StatusNotFound, fail(40401, "marker not found: "+c.Param("id")))
		return
	}
	if err != nil {
		c.JSON(http.StatusInternalServerError, fail(50001, "查询失败: "+err.Error()))
		return
	}
	c.JSON(http.StatusOK, ok(m))
}

// UpdateMarker PATCH /api/v1/markers/:id —— 管理端状态流转/改标题等
func (h *Handlers) UpdateMarker(c *gin.Context) {
	// 部分更新：用 map 承接任意 JSON，随后白名单过滤，
	// 未知字段一律忽略——防止客户端注入不可控列（如篡改 created_at）
	var patch map[string]interface{}
	if err := c.ShouldBindJSON(&patch); err != nil {
		c.JSON(http.StatusBadRequest, fail(40001, "请求体不是合法 JSON: "+err.Error()))
		return
	}

	allowed := map[string]bool{"status": true, "title": true, "description": true}
	filtered := map[string]interface{}{}
	for k, v := range patch {
		if allowed[k] {
			filtered[k] = v
		}
	}

	// 值校验：status 必须命中枚举；title 非空限长（description 任意）
	if v, ok := filtered["status"].(string); ok {
		if !validStatuses[v] {
			c.JSON(http.StatusBadRequest, fail(40001, "status 非法: "+v))
			return
		}
	}
	if v, ok := filtered["title"].(string); ok {
		v = strings.TrimSpace(v)
		if v == "" || utf8.RuneCountInString(v) > 64 {
			c.JSON(http.StatusBadRequest, fail(40001, "title 必填且不超过 64 字符"))
			return
		}
		filtered["title"] = v
	}

	m, err := h.Store.Update(c.Param("id"), filtered)
	if err == sql.ErrNoRows {
		c.JSON(http.StatusNotFound, fail(40401, "marker not found: "+c.Param("id")))
		return
	}
	if err != nil {
		c.JSON(http.StatusInternalServerError, fail(50001, "更新失败: "+err.Error()))
		return
	}
	c.JSON(http.StatusOK, ok(m))
}

// DeleteMarker DELETE /api/v1/markers/:id —— 204 空响应（契约）
func (h *Handlers) DeleteMarker(c *gin.Context) {
	err := h.Store.Delete(c.Param("id"))
	if err == sql.ErrNoRows {
		c.JSON(http.StatusNotFound, fail(40401, "marker not found: "+c.Param("id")))
		return
	}
	if err != nil {
		c.JSON(http.StatusInternalServerError, fail(50001, "删除失败: "+err.Error()))
		return
	}
	c.Status(http.StatusNoContent)
}
```

- [ ] **Step 2: 更新 main.go 注册路由**

`server/main.go` 完整替换为：

```go
package main

import (
	"log"

	"github.com/gin-gonic/gin"
)

func main() {
	// 为什么 ReleaseMode：原型对外服务，避免每请求打印调试日志刷屏
	gin.SetMode(gin.ReleaseMode)

	// 打开 SQLite：单文件 park-inspection.db，重启不丢数据（确认书铁律 6）
	db, err := OpenDB("park-inspection.db")
	if err != nil {
		log.Fatalf("open db failed: %v", err)
	}
	defer db.Close()

	h := &Handlers{Store: &Store{DB: db}}

	r := gin.Default()
	// 健康检查（契约：{"status":"ok"}，不走信封）
	r.GET("/healthz", h.Health)

	// /api/v1 路由组：契约第 2 节全部端点
	api := r.Group("/api/v1")
	{
		api.POST("/markers", h.CreateMarker)     // Unity 上报
		api.GET("/markers", h.ListMarkers)       // React 列表（筛选+分页）
		api.GET("/markers/:id", h.GetMarker)     // 详情
		api.PATCH("/markers/:id", h.UpdateMarker) // 状态流转/编辑
		api.DELETE("/markers/:id", h.DeleteMarker) // 删除
	}

	log.Println("park-inspection server listening on :8080")
	if err := r.Run(":8080"); err != nil {
		log.Fatalf("server exit: %v", err)
	}
}
```

- [ ] **Step 3: 编译验证**

```powershell
cd D:\桌面\小作业\server
$env:PATH = "$env:USERPROFILE\go-sdk\go\bin;" + $env:PATH
$env:CGO_ENABLED = '0'
go build ./...
go vet ./...
```

Expected: 均 exit 0。

- [ ] **Step 4: 提交**

```powershell
cd D:\桌面\小作业
git add server/handlers.go server/main.go
git commit -m "feat(server): Task4 六端点 + 路由接线"
```

---

### Task 5: 全量验收脚本 + 重启持久化验证

**Files:**
- Create: `scripts/acceptance.ps1`（幂等、可重复执行、退出码非 0 表示失败）
- Create: `server/README.md`（如何启动 + curl 示例，供 Unity/React 端查阅）

**Interfaces:**
- Consumes: 已完成的服务端（Task 1–4）；契约第 4 节 7 条 curl 场景。
- Produces: 全绿验收报告（PowerShell 输出 + exit 0）；持久化验证通过。

- [ ] **Step 1: 写验收脚本**

`scripts/acceptance.ps1`：

```powershell
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
  $created = Invoke-RestMethod -Method Post "$base/api/v1/markers" -ContentType 'application/json' -Body $body
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

  Write-Host '`nALL CHECKS PASSED ✔' -ForegroundColor Green
} finally {
  # 兜底：无论成败都停掉服务器，避免占用 8080
  if ($p -and -not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
}
```

- [ ] **Step 2: 写 server/README.md**

`server/README.md`：

```markdown
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
```

- [ ] **Step 3: 编译 + 全量验收**

```powershell
cd D:\桌面\小作业\server
$env:PATH = "$env:USERPROFILE\go-sdk\go\bin;" + $env:PATH
$env:CGO_ENABLED = '0'
go build ./...
cd D:\桌面\小作业
powershell -ExecutionPolicy Bypass -File scripts/acceptance.ps1
```

Expected: 输出 `ALL CHECKS PASSED ✔`，exit 0（覆盖 7 条 curl 场景 + 重启持久化 + 删除 404）。

- [ ] **Step 4: 提交**

```powershell
cd D:\桌面\小作业
git add scripts/acceptance.ps1 server/README.md
git commit -m "feat(server): Task5 全量验收脚本 + 重启持久化验证"
```

---

## Self-Review（计划自检）

1. **Spec 覆盖**：确认书 Phase 1 全部要求已映射 —— 4 文件结构（Task 1–4）、6 端点 + healthz（Task 4）、信封与错误码（Task 2/4）、curl 验收（Task 5）、重启持久化（Task 5）、每任务提交（各 Task 末步）。✅
2. **占位符扫描**：无 TBD/TODO；每个代码步骤含完整可编译源码。✅
3. **类型一致性**：`Store` 方法签名在 Task 3 定义、Task 4 handler 调用，参数类型一致（`Get(id string) (Marker, error)`、`List(...) ([]Marker, int, error)`、`Update(id string, fields map[string]interface{})`、`Delete(id string) error`）；`ok/fail/nowRFC3339/validTypes/validStatuses` 在 Task 2 定义、Task 3/4 使用，命名一致。✅
