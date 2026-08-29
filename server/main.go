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
