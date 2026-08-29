# park-inspection/web —— 管理端（React + Vite + TS + Axios）

## 启动

```powershell
# 1) 先启动 Go 后端（见 server/README.md），监听 :8080
# 2) 启动前端 dev server
cd web
npm install        # 首次
npm run dev        # http://localhost:5173
```

- Vite dev proxy 已配置：`/api` → `http://localhost:8080`（同源，绕 CORS，见 `vite.config.ts`）。
- 后端宕机时：列表区域显示「⚠ 后端服务不可用 + 重试」（Error Boundary 兜底），标题栏不受影响。

## 构建 / 预览

```powershell
npm run build      # tsc 类型检查 + vite build，产物在 dist/
npm run preview    # 本地预览构建产物
```

## 结构

```
src/
├─ main.tsx / App.tsx              # 入口 + 组装（ErrorBoundary 包数据区）
├─ index.css                       # 极简手写样式（无组件库）
├─ api/
│  ├─ types.ts                     # 契约类型（字段与 docs/api-contract.md 对齐）
│  └─ client.ts                    # Axios 实例 + 信封解包 + ApiError
└─ components/
   ├─ ErrorBoundary.tsx            # 类组件：宕机兜底 + 重试
   └─ MarkerList.tsx               # 列表/筛选/分页/状态流转/删除
```

## 验收（对应 docs/superpowers/plans/2026-08-29-phase3-react-web.md）

1. Go 运行中：列表显示 Go 库内数据（curl POST 造的）。
2. status/type 下拉筛选生效；分页 total 正确。
3. 行内状态下拉：改即 PATCH，刷新后保持。
4. 删除：confirm 后 DELETE，成功后刷新。
5. 停 Go → 页面显示「服务不可用」；起 Go → 点「重试」恢复。
