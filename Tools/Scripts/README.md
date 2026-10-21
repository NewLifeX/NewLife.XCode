# Tools/Scripts

仓库级 PowerShell 脚本：CI 门禁与本地开发工具。与 `Doc/`、`Samples/` 下随项目走的脚本不同，这些脚本服务于整个仓库。

约定：脚本位于 `Tools/Scripts/`，用「上两级」定位仓库根目录；`check-english-exceptions.ps1` 与 `sync-readme-*.ps1` 另提供 `-Root` 供下游仓库复用。

## 脚本索引

| 脚本 | 用途 | 阻断 CI | 调用方 |
|---|---|---|---|
| `check-locales.ps1` | 多语言词表键集、占位符、格式说明符、孤儿键与源码消息覆盖校验 | 是 | `test.yml` |
| `check-english-exceptions.ps1` | 运行时消息（异常、日志、控制台、埋点标签）不得含中文 | 是 | `test.yml` |
| `collect-metadata-keys.ps1` | 从 `Model.xml`、`[DisplayName]`/`[Description]`/`[Category]` 特性与 csproj 收集可选键，生成 `metadata-keys.json`；`-All` 输出全部扫描键，`-OutFile` 导出覆盖矩阵 | 否 | 手工维护词表 |
| `sync-readme-languages.ps1` | 按 `readme-languages.json` 生成各 README 的语言切换行 | 是（`-Check`） | `readme.yml`、`test.yml` |
| `sync-readme-package.ps1` | 由 `README.md` 生成 NuGet 包内 `README.nuget.md`，相对链接改写为绝对地址 | 是（`-Check`） | `readme.yml` |

## 配置与产物

- 配置描述根目录的 README，留在仓库根：`readme-languages.json`、`readme-package.json`
- 词表位于 `XCode/Locales`，元数据键清单 `metadata-keys.json` 与词表同目录，`check-locales.ps1` 会自动识别

## 常用命令

```powershell
pwsh -File Tools/Scripts/check-locales.ps1 -LocaleDir XCode/Locales -SourceDir "XCode,XCodeTool" -RequireTranslated -MetadataSourceDir XCode
pwsh -File Tools/Scripts/check-english-exceptions.ps1
pwsh -File Tools/Scripts/collect-metadata-keys.ps1 -Model XCode/Membership/Member.xml -All -OutFile metadata-coverage.csv
pwsh -File Tools/Scripts/sync-readme-package.ps1 -Check
```

新增、重命名或删除脚本时，同步更新 `.github/workflows/` 中的引用与 `paths` 过滤，以及 `Doc/多语言本地化.md`。
