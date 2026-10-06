# RTTUnitEditor 现行设计基线入口

基线标识：DB-2026-10-06-40。本地规范正文来自RTTGame对应基线；来源路径、源及副本SHA256与改编范围见docs/BASELINE_SOURCES.json。

本入口仅适配本项目文档路径与职责，不改变游戏规则。副本不表示现有编辑器已经符合该版本。代码当前仍使用DB33来源标识；实现差异见PROJECT_STATUS.md。冲突、未决与缺失值不得由历史实现或测试成功自动裁决。

项目约束见[AGENTS.md](AGENTS.md)；当前状态见[PROJECT_STATUS.md](PROJECT_STATUS.md)；正式参数见[RTT_GAME_DATA.xlsx](docs/RTT_GAME_DATA.xlsx)。这些是本地独立副本，不运行时依赖外部工程。用户显式指令优先，游戏工程授权及交接不转移至本项目。

## 规范正文

| 范围 | 正文 |
| --- | --- |
| 核心配置定义 | [单位、武器与配置](design/UNIT_CONFIGURATION.md) |
| 配置可用性与卡的边界 | [阵营、专精与作战群](design/OUT_OF_MATCH.md) |
| 必填、能力与机制待定 | [待定设计与冲突](design/PENDING_DECISIONS.md) |
| 武器及操作关系 | [伤亡、武器与战斗能力](design/COMBAT_RULES.md) |
| 弹药静态字段与发射状态边界 | [弹道与碰撞](design/PROJECTILES.md) |
| 重量与容量 | [运输与后勤](design/TRANSPORT_LOGISTICS.md) |
| 游戏显示与编辑器的边界 | [界面与输入](design/UI_INPUT.md) |
| 部署边界 | [经济与部署](design/DEPLOYMENT_ECONOMY.md) |
| 移动边界 | [移动与任务](design/MOVEMENT_COMMANDS.md) |
| 观察及隐蔽 | [场景与隐蔽](design/WORLD_VISIBILITY.md) |
| 任务与合作边界 | [任务与平衡](design/MATCH_MISSION.md) |
| 长期愿景 | [PVP](design/FUTURE_PVP.md) |

直接配置审阅优先读取配置定义、待定项、战斗与后勤；其他正文用于完整规则引用及职责边界，不授权在编辑器实现游戏系统。

## 本地副本边界

保留规范正文的已确认规则、待定项和冲突。UNIT_CONFIGURATION仅移除外部数据库来源叙述和来源字段列，本项目规则列不变；不恢复外部数据、工具或派生测试单位。

正文中缺失的设计意图文档、游戏HANDOFF以及弹丸／回放等工程契约引用保留来源语义，不作为编辑器独立权威或本轮待办。P01属于游戏弹丸工程边界，本项目不执行该契约；不以缺失外部契约裁决规则。若后续任务确实依赖这些文件，应另行明确资料与授权，不跨项目写入或扩大范围。
