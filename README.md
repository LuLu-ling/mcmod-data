# mcmod-data

自动更新 [Plain Craft Launcher 2](https://github.com/PCL-Community/PCL-CE) 的模组数据库文件。

从 [Meloong-Git/PCL](https://github.com/Meloong-Git/PCL) 的 `moddata.txt` 生成 `mcmod.buf`，如有变更则自动发布 release 并提交 PR 到 [PCL-CE](https://github.com/PCL-Community/PCL-CE)。


| Secret | 说明 |
|---|---|
| `PAT_PCL_CE` | 有 PCL-CE 仓库 `contents: write` 权限的 classic PAT |
| `GPG_PRIVATE_KEY` | 用于签名 commit 的 GPG 私钥（`gpg --export-secret-key --armor` 导出） |

## License

[LGPL-3.0 license](https://github.com/PCL-Community/mcmod-data/blob/master/LICENSE)
