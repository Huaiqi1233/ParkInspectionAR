package main

import "time"

// Marker 三端共享实体。json tag 必须与 docs/api-contract.md v2.0 完全一致，
// 否则 Unity/React 解包字段名会错位。
// v2.0 严格对齐任务书：priority 字段、status 三态、position 仅 x/y/z，无 rotation/geo/reporter/photoUrl/type。
type Marker struct {
	ID          string   `json:"id"`
	Title       string   `json:"title"`
	Description string   `json:"description"`
	Priority    string   `json:"priority"`
	Status      string   `json:"status"`
	Position    Position `json:"position"`
	CreatedAt   string   `json:"createdAt"`
	UpdatedAt   string   `json:"updatedAt"`
}

// Position 对应契约 position{x,y,z}，AR 会话空间坐标（任务书：位置 x/y/z）
type Position struct {
	X float64 `json:"x"`
	Y float64 `json:"y"`
	Z float64 `json:"z"`
}

// CreateMarkerRequest POST 入参：id/status/createdAt/updatedAt 由服务端生成，
// 客户端（Unity）只允许提交业务字段（任务书 3.1 表单：标题/描述/优先级/位置）。
type CreateMarkerRequest struct {
	Title       string   `json:"title"`
	Description string   `json:"description"`
	Priority    string   `json:"priority"`
	Position    Position `json:"position"`
}

// Envelope 三端统一响应信封。data 用 omitempty：失败响应不带 data 字段。
type Envelope struct {
	Code    int         `json:"code"`
	Message string      `json:"message"`
	Data    interface{} `json:"data,omitempty"`
}

// ok / fail 生成信封，code=0 恒成功（契约第 1 节）
func ok(data interface{}) Envelope { return Envelope{Code: 0, Message: "ok", Data: data} }

func fail(code int, msg string) Envelope { return Envelope{Code: code, Message: msg} }

// 合法优先级白名单（任务书：high/medium/low）
var validPriorities = map[string]bool{
	"high":   true,
	"medium": true,
	"low":    true,
}

// 合法状态白名单（任务书 3.2：open/in_progress/resolved 三态）
var validStatuses = map[string]bool{
	"open":        true,
	"in_progress": true,
	"resolved":    true,
}

// nowRFC3339 统一时间格式（契约 createdAt/updatedAt 为 RFC3339）
func nowRFC3339() string { return time.Now().Format(time.RFC3339) }
