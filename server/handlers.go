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

// CreateMarker POST /api/v1/markers —— Unity 上报入口（任务书 3.3：接收 Unity 上报 + 防重放）
func (h *Handlers) CreateMarker(c *gin.Context) {
	var req CreateMarkerRequest
	if err := c.ShouldBindJSON(&req); err != nil {
		c.JSON(http.StatusBadRequest, fail(40001, "请求体不是合法 JSON: "+err.Error()))
		return
	}
	// 校验：title/description/priority 必填且限长（任务书表单字段）
	req.Title = strings.TrimSpace(req.Title)
	req.Description = strings.TrimSpace(req.Description)
	if req.Title == "" || utf8.RuneCountInString(req.Title) > 64 {
		c.JSON(http.StatusBadRequest, fail(40001, "title 必填且不超过 64 字符"))
		return
	}
	if req.Description == "" {
		// 描述：可选（任务书 3.1 只要求"标题：必填；描述：可选"），空描述直接存空串
		req.Description = ""
	} else if utf8.RuneCountInString(req.Description) > 256 {
		c.JSON(http.StatusBadRequest, fail(40001, "description 不超过 256 字符"))
		return
	}
	if !validPriorities[req.Priority] {
		c.JSON(http.StatusBadRequest, fail(40001, "priority 非法（应为 high/medium/low）: "+req.Priority))
		return
	}
	// 位置可选（方案 A：GPS 跨设备定位）：(0,0) 视为未定位；非零则校验经纬度范围
	if req.Location.Lat != 0 || req.Location.Lng != 0 {
		if req.Location.Lat < -90 || req.Location.Lat > 90 || req.Location.Lng < -180 || req.Location.Lng > 180 {
			c.JSON(http.StatusBadRequest, fail(40001, "location 非法：lat∈[-90,90], lng∈[-180,180]"))
			return
		}
	}
	// 现场照片可选（方案 C）：base64 字符串，限 1MB，防止超大请求
	if len(req.Photo) > 1_000_000 {
		c.JSON(http.StatusBadRequest, fail(40001, "photo 过大（base64 超过 1MB）"))
		return
	}

	now := nowRFC3339()
	m := &Marker{
		ID:          uuid.NewString(), // 任务书 3.3：为新问题生成唯一 ID
		Title:       req.Title,
		Description: req.Description,
		Priority:    req.Priority,
		Position:    req.Position,
		Location:    req.Location, // 可选 GPS，未定位为 (0,0)
		Photo:       req.Photo,    // 可选现场照片
		Status:      "open",        // 新标记默认 open（任务书状态机起点）
		CreatedAt:   now,
		UpdatedAt:   now,
	}
	if err := h.Store.Insert(m); err != nil {
		if err == ErrDuplicate {
			// 防重放：相同标题+描述+位置的重复上报 → 409
			c.JSON(http.StatusConflict, fail(40901, "重复上报：相同位置已有相同描述的问题"))
			return
		}
		c.JSON(http.StatusInternalServerError, fail(50001, "写入失败: "+err.Error()))
		return
	}
	c.JSON(http.StatusCreated, ok(m))
}

// ListMarkers GET /api/v1/markers?status=&priority=&page=&pageSize= —— React 列表
func (h *Handlers) ListMarkers(c *gin.Context) {
	page, _ := strconv.Atoi(c.DefaultQuery("page", "1"))
	pageSize, _ := strconv.Atoi(c.DefaultQuery("pageSize", "20"))
	if page < 1 {
		page = 1
	}
	if pageSize < 1 || pageSize > 100 {
		pageSize = 20
	}
	items, total, err := h.Store.List(c.Query("status"), c.Query("priority"), page, pageSize)
	if err != nil {
		c.JSON(http.StatusInternalServerError, fail(50001, "查询失败: "+err.Error()))
		return
	}
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

// UpdateMarker PATCH /api/v1/markers/:id —— 管理端状态流转（任务书 3.2：修改状态）
func (h *Handlers) UpdateMarker(c *gin.Context) {
	var patch map[string]interface{}
	if err := c.ShouldBindJSON(&patch); err != nil {
		c.JSON(http.StatusBadRequest, fail(40001, "请求体不是合法 JSON: "+err.Error()))
		return
	}

	// 白名单：status/title/description/priority，未知字段忽略（防注入）
	allowed := map[string]bool{"status": true, "title": true, "description": true, "priority": true}
	filtered := map[string]interface{}{}
	for k, v := range patch {
		if allowed[k] {
			filtered[k] = v
		}
	}

	if v, ok := filtered["status"].(string); ok {
		if !validStatuses[v] {
			c.JSON(http.StatusBadRequest, fail(40001, "status 非法（应为 open/in_progress/resolved）: "+v))
			return
		}
	}
	if v, ok := filtered["priority"].(string); ok {
		if !validPriorities[v] {
			c.JSON(http.StatusBadRequest, fail(40001, "priority 非法: "+v))
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
