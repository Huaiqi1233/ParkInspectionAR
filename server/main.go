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
		api.POST("/markers", h.CreateMarker)      // Unity 上报
		api.GET("/markers", h.ListMarkers)        // React 列表（筛选+分页）
		api.GET("/markers/:id", h.GetMarker)      // 详情
		api.PATCH("/markers/:id", h.UpdateMarker) // 状态流转/编辑
		api.DELETE("/markers/:id", h.DeleteMarker) // 删除
	}

	log.Println("park-inspection server listening on :8080")
	if err := r.Run(":8080"); err != nil {
		log.Fatalf("server exit: %v", err)
	}
}
