# 园区巡检AR标注原型 — 交接状态

## ✅ 已完成（三端闭环代码全部就绪）
- 契约：`docs/api-contract.md` v0.1 + 确认书 + 3 份实施计划
- **Phase 1 Go 后端**（server/）：6 端点 + healthz，验收 8/8 通过，重启持久化验证通过
- **Phase 3 React 管理端**（web/）：列表/筛选/分页/流转/删除 + ErrorBoundary，端到端验收通过
- **Phase 2 Unity AR 端**（unity/ParkInspectionAR → junction → D:\UnityProjects\ParkInspectionAR）：
  - AR Foundation 5.1.5 + ARCore 5.1.5 + Input System 1.7.0
  - SceneBuilder 一键搭场景（XR Origin/AR Session/Plane/Raycast/EventSystem/Canvas）
  - ARMarkerController（IsPointerOverGameObject(fingerId) 区分 UI/AR 点击 + PlaneWithinPolygon 射线）
  - MarkerSubmitter（POST + UTF-8 + 失败缓存重试）+ ReportPanelUI（表单）
  - **Android APK 构建成功：Builds/ParkInspectionAR.apk 20.6MB**

## ⏸ 剩余：Phase 4 闭环验收（真机走查，需用户操作）
1. 手机 adb install APK → 授权相机
2. 扫平面 → 点 AR 平面 → 预览体 + 表单 → 确认上报
3. 验证：curl GET 看到新数据 + React 5173 页面可见
4. 电脑需：Go 后端运行（:8080）+ 防火墙放行 8080 + 手机同一 Wi-Fi
5. 手机上报地址：电脑局域网 IP（改 MarkerSubmitter 的 ServerBaseUrl）

## 🔧 环境备忘（已记 wolf memory + buglog）
- Unity 2022.3.62f2 中国区版：D:\Unity\Editors\2022.3.62f2
- Android 工具链：SDK=D:\Android\Sdk（cmdline-tools 6.0 + platform-33 + build-tools 33.0.2 + NDK r23b）；JDK11=D:\Android\jdk11\jdk-11.0.32.1+1（Unity 2022.3 只认 JDK 11！）
- 关键坑（buglog）：① 工程路径必须 ASCII（中文路径报 Invalid project path，用 junction 解决）② Unity 2022.3 要求 JDK 11 / cmdline-tools 6.0 / NDK r23b ③ 工具链配置必须用 AndroidExternalToolsSettings 官方 API
- Go 1.27.0 便携版 / Node 24 + npmmirror / PowerShell 5.1 UTF-8 坑

## 🚀 Next phase
真机走查（用户操作）→ 闭环验收完成 → 项目收尾
