# 项目目录与开发

- `src/`：C# / WPF 源码。`NoteDesk.cs` 保留模型、窗口与应用入口；外观、壁纸、标题栏、快捷键和设置分别在对应文件。
- `assets/`：图标与内置云朵壁纸，构建时嵌入 EXE。
- `tests/`：隔离的模型、窗口、真实按键、图片与布局验证。
- `scripts/`：版本化发布打包。只生成本地产物，不自动上传。
- `docs/releases/`：GitHub Release 更新说明。
- `docs/superpowers/`：历史设计、实施与验证记录。
- `.agents/`、`skills-lock.json`：用户安装的开发技能配置，运行程序不需要它们。
- `dist/`：本地构建与发布产物；`.qa/`：隔离测试数据和临时截图，二者均由 Git 忽略。

## 构建与验证

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/verify.ps1
```

原生按键与窗口验证应在交互式 Windows 桌面中执行。所有测试使用独立数据目录，不要指定真实便签目录。

## 发布

1. 更新程序集版本、README、RELEASE_NOTES 和 `docs/releases/<tag>.md`。
2. 通过完整验证，检查两套主题截图。
3. 提交源码后执行 `scripts/package-release.ps1 -Version <版本>`；打包要求工作区干净、EXE 与源码版本和构建输入一致，并校验包内文件。
4. 推送主分支和对应版本标签，将 EXE、Windows 完整包和 SHA256SUMS 上传到 Release；附件完成后再公开发布。

完整包包含 EXE、当前版本源码、资源、构建/测试/打包脚本、文档与验证记录，不包含真实便签、临时测试数据、旧版本二进制或 Git 目录。GitHub 也会为标签提供标准源码归档。
