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
func ok(data interface{}) Envelope { return Envelope{Code: 0, Message: "ok", Data: data} }

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
