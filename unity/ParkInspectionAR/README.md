# park-inspection/unity —— AR 上报端（Unity 2022.3.62f2 + AR Foundation 5.1.5）

> 注意：本工程因 Unity Android 构建工具不支持非 ASCII 路径，**实际工程位于 ASCII 路径
> `D:\UnityProjects\ParkInspectionAR`**，工作区 `unity/ParkInspectionAR` 是指向它的 junction 链接
> （git 正常跟踪）。

## 打开工程

1. 打开 Unity Hub → Open → 选择 `D:\UnityProjects\ParkInspectionAR`
2. 首次打开会自动解析 AR Foundation 5.1.5 / ARCore 5.1.5 / Input System 依赖（需联网）

## 一键搭建场景

菜单栏 **Tools → 园区巡检AR → 一键搭建场景**

自动创建（全部 AR Foundation 5.1.5 官方组件）：
- `XR Origin`（含 Main Camera + ARCameraManager + ARCameraBackground + TrackedPoseDriver）
- `AR Session`（ARSession + ARInputManager）
- `AR Plane Manager`（Horizontal|Vertical 平面检测）
- `AR Raycast Manager`（屏幕点 → 平面射线）
- `EventSystem` + `StandaloneInputModule`（UI 点击）
- `UICanvas`（表单面板）

运行时脚本自动装配（无需手摆）：`ARMarkerController` / `MarkerSubmitter` / `ReportPanelUI`。

## 配置服务器地址

打开 `MarkerSubmitter` 组件（挂在 XR Origin 上），Inspector 里改 `Server Base Url`：

```
http://<电脑局域网IP>:8080
```

> ⚠️ 真机不能填 `localhost`（那是手机自己）。查电脑 IP：`ipconfig` 找 IPv4 地址。
> 同一 Wi-Fi 下手机才能访问；电脑防火墙需放行 8080 端口。

## 命令行构建 APK（验收方式）

```powershell
$env:PATH = "D:\Unity\Editors\2022.3.62f2\Editor;$env:PATH"
# 1) 配置工具链（SDK/JDK/NDK 路径写入 Unity）
Unity.exe -batchmode -quit -projectPath D:\UnityProjects\ParkInspectionAR `
  -executeMethod ParkInspectionAR.EditorTools.AndroidToolchain.Configure -logFile unity_tc.log
# 2) 构建 APK
Unity.exe -batchmode -quit -projectPath D:\UnityProjects\ParkInspectionAR `
  -executeMethod ParkInspectionAR.EditorTools.BuildScript.BuildAndroid -logFile unity_build.log
# 产物：Builds/ParkInspectionAR.apk
```

或 Unity 内菜单 **Tools → 园区巡检AR → 构建 Android APK**。

## 真机走查步骤（闭环验收）

1. 电脑：启动 Go 后端（`server/server.exe`，监听 8080）+ 记录局域网 IP
2. 手机（支持 ARCore 的 Android，开 USB 调试）：
   - `adb install Builds/ParkInspectionAR.apk`
   - 打开 App，授权相机权限
3. 扫描平面（缓慢移动手机）→ 出现平面网格
4. 点 AR 平面 → 半透明预览体出现 → 底部弹表单
5. 选 type / 填 title / 确认上报 → toast「已上报」，预览体转实体色
6. 电脑验证闭环：`curl http://localhost:8080/api/v1/markers` 看到新数据 →
   打开 React 管理端 `http://localhost:5173` 列表可见该标注

## 失败重试

上报失败（后端宕机/超时）：toast 显示原因，表单出现「重试」按钮，
本地缓存最后一条未上报数据（`MarkerSubmitter.cachedJson`），点重试重新 POST。

## 关键实现说明

- **UI/AR 点击区分**：`EventSystem.IsPointerOverGameObject(touch.fingerId)` 带触点参数
  （无参重载只对鼠标有效，真机触摸永远返回 false，点 UI 会穿透发射线）
- **射线检测**：`ARRaycastManager.Raycast(screenPos, hits, TrackableType.PlaneWithinPolygon)`
  只命中平面多边形内部，忽略点云/平面外区域
- **上报编码**：`Content-Type: application/json; charset=utf-8` + UTF-8 字节（Go 端中文不乱码）
- **坐标语义**：`position/rotation` 直接取 AR 射线命中 Pose（AR 会话空间，契约语义）
- **geo**：原型阶段传 null（不启用 GPS，避免权限配置过度设计）
