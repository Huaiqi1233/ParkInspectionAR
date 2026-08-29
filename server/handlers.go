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
