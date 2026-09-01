# 现场AR问题标注 —— 园区巡检 AR 标注原型

园区巡检人员用手机在**现场放置 AR 标记并上报问题**（设备破损/异常/安全隐患），管理人员在 **Web 管理端**查看并流转处理状态。

核心闭环：`Unity AR 标记上报 → Go 后端保存 → React 查看/改状态`

---

## 1. 目录结构

```
├─ unity/            Unity AR 移动端（工程真身在 D:\UnityProjects\ParkInspectionAR，ASCII 路径）
│   └─ ParkInspectionAR   ← junction 链接到真身
├─ server/           Go 后端（Gin + SQLite）
├─ web/              React 管理端（Vite + TS + Axios）
├─ docs/             接口契约 + 开发计划
├─ scripts/          Go 后端验收脚本
├─ AI_log.md         AI 使用记录
└─ README.md         本文件
```

---

## 2. 版本与依赖

| 组件 | 版本 |
|---|---|
| Unity Editor | 2022.3.62f2（中国区版，含 Android Build Support） |
| AR Foundation | 5.1.5 |
| ARCore XR Plugin | 5.1.5 |
| Input System | 1.7.0 |
| Android 工具链 | JDK 11 + SDK（cmdline-tools 6.0）+ NDK r23b |
| Go | 1.27.0（Windows，CGO_ENABLED=0 免 gcc） |
| Node / npm | 24.x / 11.x |
| React / Vite / TS | 18.3.1 / 6.0.7 / 5.7.2 |

---

## 3. 三端启动方式

### 3.1 Go 后端（监听 `:8080`，SQLite 落盘 `park-inspection.db`）

```powershell
$env:PATH = "$env:USERPROFILE\go-sdk\go\bin;" + $env:PATH   # 便携版 Go 路径（如用系统 Go 可省略）
$env:CGO_ENABLED = '0'
cd server
go build -o server.exe .
.\server.exe            # 首次运行自动建库建表
```

验证：浏览器打开 `http://localhost:8080/healthz` → `{"status":"ok"}`。

### 3.2 React 管理端（监听 `:5173`，`/api` 代理到 Go `:8080`）

```powershell
cd web
npm install            # 首次
npm run dev            # 开发服务器 http://localhost:5173
```

### 3.3 Unity AR 移动端

- 打开工程：`D:\UnityProjects\ParkInspectionAR`
- 一键搭场景：菜单 `Tools → 园区巡检AR → 一键搭建场景`（自动生成 `Assets/Scenes/Main.unity`）
- **编辑器内验证（无需真机/GMS）**：`Edit → Project Settings → XR Plug-in Management → 勾选 XR Simulation`，然后直接 Play
- 构建 Android APK：菜单 `Tools → 园区巡检AR → 构建 Android APK`（产物 `Builds/ParkInspectionAR.apk`，或命令行 `-executeMethod ParkInspectionAR.EditorTools.BuildScript.BuildAndroid`）

---

## 4. Unity 真机如何配置 Go 后端地址

后端地址在 `Assets/Scripts/MarkerSubmitter.cs` 的 `serverBaseUrl` 字段：

```csharp
public string serverBaseUrl = "http://192.168.8.111:8080";
```

| 场景 | 取值 |
|---|---|
| **真机走查** | 电脑的**局域网 IP**（如 `http://192.168.8.111:8080`），**不能用 localhost**（localhost 是手机自己） |
| **XR Simulation（同机）** | `http://localhost:8080` |

前置条件（真机连得通后端）：
1. 手机与电脑在**同一网段**（同一 WiFi/路由）；
2. 电脑防火墙放行 **8080 入站**（已有规则 `ParkInspection-Go-8080`）；
3. 手机已装 **Google Play Services for AR（ARCore）**——国行 iQOO/OriginOS 无 GMS，需手动装 GMS 三件套 + ARCore，否则平面检测不工作。

改完 `serverBaseUrl` 后需**重新构建 APK**（或直接在 Inspector 里改场景里的序列化值）。

---

## 5. 已实现功能

- **Unity AR**：真机检测水平平面 → 点击放置立方体标记 → 表单（标题必填/描述可选/优先级 high·medium·low 三按钮/位置自动）→ 提交成功/失败提示 → 点击已放标记查看输入 → 顶部状态栏显示 AR 状态与平面数 → **自动读取手机 GPS（含精度）+ 截取现场照片一并上报（方案 A+C：跨设备定位）**
- **Go 后端**：`/healthz` + POST/GET 列表/GET 详情/PATCH 状态/DELETE + UUID 唯一 ID + 默认 `open` + 标题必填校验 + 描述可选（≤256）+ 优先级/状态白名单 + SQLite 持久化 + 防重放（409）+ **可选 `location{lat,lng,accuracy}` GPS + `photo` 照片（base64≤1MB）**
- **React 管理端**：列表（标题/优先级/状态/位置/照片/时间/操作）+ 状态流转（open→in_progress→resolved）+ 筛选 + 分页 + 删除 + 后端不可用 ErrorBoundary 兜底（不白屏）+ **GPS 坐标（±精度）与「📍 地图」链接（高德）+ 现场照片缩略图（点击看大图）**
- **三端闭环已真机验证**：提交 → React 刷新可见 → 改状态 → 重启 Go 数据仍在

---

## 5.1 坐标说明（两个"位置"的区别）

| 字段 | 含义 | 别人能找到吗 |
|---|---|---|
| `position{x,y,z}` | **AR 会话内相对坐标**：原点=本次 AR 会话开始时设备的位置，单位米。仅同一会话内有效 | ❌ 不能，重启/换设备即失效（任务书已豁免恢复物理位置） |
| `location{lat,lng,accuracy}` | **GPS 经纬度 + 精度**（方案 A，可选，(0,0)=未定位） | ✅ 能（精度 ±5~10m），管理端「📍 地图」链接可直接导航（高德） |
| `photo` | **现场照片**（方案 C，可选，base64 JPEG） | ✅ 导航到附近后，看图精确确认具体点位（哪个墙角/哪块地面） |

> 简单说：`position` 是 AR 世界里的空间点（给 AR 用），`location` 是真实世界的经纬度（把人带到附近），`photo` 是现场照片（帮人精确认出点位）。

---

## 6. 未完成 / 已知问题

- [ ] **演示视频**未录制（需 2 段 3-5 分钟：手机 + 电脑）
- [ ] **交付**未提供 GitHub/Gitee/网盘链接
- [ ] 真机依赖 **GMS + ARCore**（国行机需手动安装，见 §4）
- [ ] ARCore 对**纯色/无纹理/反光**表面检测不到平面（环境限制，非代码问题）
- [ ] AR 标记**不要求重启后恢复到相同物理位置**（任务书已明确豁免）

---

## 7. 验收

```powershell
# Go 后端验收（注意：会清空并重建 park-inspection.db，仅限全新环境跑）
powershell -ExecutionPolicy Bypass -File scripts\acceptance.ps1
```

接口契约详见 `docs/api-contract.md`（信封 `{code,message,data}`，code=0 恒成功）。
