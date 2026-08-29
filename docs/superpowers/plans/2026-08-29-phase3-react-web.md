# Phase 3: React 管理端实施计划（web/）

> **For agentic workers:** REQUIRED SUB-SKILL: executing-plans（Inline 逐任务执行，用户已确认）。

**Goal:** Vite + TS + Axios 管理端：列表/筛选/分页/状态流转/删除，Error Boundary 兜底后端宕机，验收覆盖"宕机→恢复"场景。

**Architecture:** Vite dev proxy 转发 `/api` → `:8080`（绕 CORS，不改 Go）；Axios 实例统一解信封；Error Boundary 类组件仅包裹数据区；状态流转行内乐观更新。

**Tech Stack:** React 18 + Vite 6 + TypeScript 5 + Axios；npm registry 用 npmmirror（国内网络）。

**Spec:** `docs/superpowers/specs/2026-08-29-三端开发顺序与接口契约确认书.md` + `docs/api-contract.md` v0.1

---

## Global Constraints

1. 关键代码中文注释（为什么，非逐行翻译）。
2. 禁止 Redux / 状态管理库 / 复杂 UI / 组件库（如 antd 不引入，极简手写样式）。
3. TS 接口字段与 Go 契约逐字对齐（`web/src/api/types.ts`）。
4. Error Boundary 仅包数据区；宕机显示"服务不可用 + 重试"，不弹原生 alert。
5. 每任务结束 git commit。

---

### Task 1: 工程骨架（手写，不跑交互式脚手架）

**Files:**
- Create: `web/package.json`、`web/tsconfig.json`、`web/tsconfig.node.json`、`web/vite.config.ts`、`web/index.html`、`web/src/vite-env.d.ts`、`web/src/main.tsx`、`web/src/index.css`

**Interfaces:**
- Produces: 可 `npm run dev` 的 Vite 工程（占位 App），proxy 配置 `'/api' → 'http://localhost:8080'`。

- [ ] Step 1: 写全部骨架文件（见正文代码）。
- [ ] Step 2: `npm install`（registry= npmmirror）。
- [ ] Step 3: `npm run build` 通过（tsc + vite build 校验类型）。
- [ ] Step 4: git commit `feat(web): Task1 Vite+TS 工程骨架`。

### Task 2: types.ts + client.ts（契约类型 + Axios 封装）

**Files:**
- Create: `web/src/api/types.ts`、`web/src/api/client.ts`

**Interfaces:**
- Produces: `Marker/MarkerType/MarkerStatus/Envelope<T>/MarkerListData` 类型；`apiClient`（baseURL '/api'、响应拦截器解信封、错误归一化：`ApiError` 携带 message/code/status）。
- Consumes: 契约字段（Step 1 中已确认）。

- [ ] Step 1: 写 types.ts（字段逐字对齐契约）。
- [ ] Step 2: 写 client.ts（拦截器 + ApiError）。
- [ ] Step 3: `npm run build` 通过 + commit。

### Task 3: ErrorBoundary.tsx

**Files:**
- Create: `web/src/components/ErrorBoundary.tsx`

**Interfaces:**
- Produces: `ErrorBoundary` 类组件：`componentDidCatch` 记录错误，`reset()` 方法重置并重挂子树；渲染"⚠ 后端服务不可用 + 重试"（非数据错误时显示具体 message）。
- Consumes: 无（独立）。

- [ ] Step 1: 写组件 + 样式。
- [ ] Step 2: `npm run build` + commit。

### Task 4: MarkerList.tsx（列表 + 筛选 + 分页 + 状态流转 + 删除）

**Files:**
- Create: `web/src/components/MarkerList.tsx`

**Interfaces:**
- Consumes: `apiClient`、`Marker/Envelope/MarkerListData` 类型、`ErrorBoundary`。
- Produces: 自管理状态的列表组件：加载/错误/空态；status/type 下拉筛选；分页（total 驱动）；行内状态下拉（PATCH 乐观更新）；删除（confirm + DELETE）；中文映射表。

- [ ] Step 1: 写组件。
- [ ] Step 2: `npm run build` + commit。

### Task 5: App.tsx 组装 + 端到端验收 + README

**Files:**
- Modify: `web/src/App.tsx`
- Create: `web/README.md`

**Interfaces:**
- Consumes: `MarkerList` + `ErrorBoundary`。
- Produces: `App` = 标题栏 + `<ErrorBoundary><MarkerList/></ErrorBoundary>`；验收脚本步骤 + README。

- [ ] Step 1: App.tsx 组装。
- [ ] Step 2: 启动 Go + Vite，浏览器验收：列表/筛选/分页/流转/删除全通。
- [ ] Step 3: 宕机验收：停 Go → 页面"服务不可用"；起 Go → 点重试恢复。
- [ ] Step 4: README + 最终 commit。

---

## 验收清单（Task 5 执行）

1. Go 运行中：`GET /api/v1/markers` 经 proxy 正常，列表显示 curl 造的数据。
2. 筛选 status=pending / type=hazard 生效；分页 total 正确。
3. 状态流转 pending→processing 后刷新保持；删除后 404 不报错。
4. 停 Go：页面显示"⚠ 后端服务不可用"，无白屏、无原生 alert。
5. 起 Go：点"重试"恢复列表。
