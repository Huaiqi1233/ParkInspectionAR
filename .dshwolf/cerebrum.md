# Cerebrum

Learned preferences, project conventions, and corrections.

## Preferences

## Conventions

- 园区巡检AR标注原型：三端契约先行，Marker JSON 实体定义在 docs/api-contract.md；响应统一信封 {"code":0,"message":"ok","data":...}；错误用非零 code + 4xx/5xx 状态码。
## Do-Not-Repeat

## Environment

- Android 构建工具链（2026-08-29 配置）：SDK=D:\Android\Sdk（platform-tools/adb 1.0.41 + platforms;android-33 + build-tools;33.0.2）；JDK17=D:\Android\jdk17\jdk-17.0.20.1+1（Temurin）；sdkmanager=D:\Android\cmdline-tools\latest\bin\sdkmanager.bat；Unity 用 Mono 后端（无 NDK 也可构建，ARCore 要求 minSdk>=24 已配）。Unity EditorPrefs 已写入 AndroidSdkRoot/JdkRoot。license 接受需连续喂 y（20 个）。
- Unity 环境（2026-08-29 安装完成）：Unity Hub 3.3.6 在 %LOCALAPPDATA%\Programs\Unity\Unity Hub.exe；Unity Editor 2022.3.62f2 在 D:\Unity\Editors\2022.3.62f2\Editor\Unity.exe（注意：china CDN 无 62f3 用 62f2，AR Foundation 5.x 兼容）；Android Build Support 已装。下载源统一走 download.unitychina.cn（国内节点快）。下载缓存 D:\Unity\downloads（4.1GB 可删）。
- 本机 Go 环境：便携版 C:\Users\86139\go-sdk\go\bin（不在 PATH，需 $env:PATH 前置）；GOPROXY 已持久化为 https://goproxy.cn,direct（直连 Google 代理超时）；构建必须 CGO_ENABLED=0（无 gcc）。

## Decisions

- 开发顺序变更（2026-08-29 确认）：因本机无 Unity Editor，Phase 2 Unity 延后，先做 Phase 3 React 管理端。最终顺序 Go → React → Unity。Unity 环境就绪后再补 Phase 2。
