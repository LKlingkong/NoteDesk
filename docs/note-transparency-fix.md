# 便签透明度修复（工作版本）

用户报告背景透明度无法调整。旧实现仅改变 surface 的渐变 Alpha，但 NoteWindow.AllowsTransparency=false，DesktopGlass.Apply 又添加窗口底色与 DWM Acrylic，遮挡了真实桌面。此前离屏预览不会触发完整原生合成链路，未检出此问题。

| Before | After | Why |
| --- | --- | --- |
| 非分层窗口叠加 Acrylic 和窗口底色 | 便签使用分层透明窗口，窗口及原生合成背景清空，表层单独控制 Alpha | 让真实桌面能透过背景 |
| 菜单直接放置 Slider | 显式常开菜单容器，显示百分比和说明，关闭时立即保存 | 调整过程稳定且状态明确 |
| 只验证离屏控件和画刷 | 增加隐藏 HWND 的 WS_EX_LAYERED 与合成背景验证，再检查最终像素 Alpha | 防止回归盲点 |

背景不透明度 25%–100%，越低越透明；正文、按钮和整个窗口的 Opacity 始终为 1。高对比度模式保持实色并显示原因。便签不再启用遮挡 Alpha 的原生 Acrylic；管理窗口和辅助窗口的材质策略保持原有实现。

新增 tests/NoteTransparencyTests.cs，接入 tests/run.ps1。旧实现先失败于原生透明窗口断言，修复后通过。验证还覆盖浅色/深色、圆角外透明、固定前景探针完全不透明、方向键调整、菜单关闭保存和非法 Alpha 回退。未向真实桌面注入按键或操作真实便签数据。

独立构建位于 dist/note-transparency-fix/NoteDesk.exe。本轮为本地修复，便签的进一步视觉美化等待设计取舍确认。
