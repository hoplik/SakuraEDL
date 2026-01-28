# 云端 Loader 匹配

## 概述

SakuraEDL 支持云端自动匹配 Loader，无需手动寻找和下载 Programmer 文件。

## 工作原理

```
设备连接 → 读取芯片信息 → 云端查询 → 自动下载 → 加载 Loader
```

### 匹配优先级

1. **PK Hash 精确匹配** - 最高优先级，100% 匹配
2. **MSM ID 匹配** - 根据芯片 ID 匹配
3. **芯片型号匹配** - 根据芯片型号匹配

## 使用方法

### 自动匹配

1. 连接设备进入 9008 模式
2. 程序自动读取芯片信息
3. 云端自动匹配并下载 Loader
4. 无需手动操作

### 手动选择

如果自动匹配失败：

1. 点击 **"选择 Loader"** 按钮
2. 从云端列表中选择
3. 或手动指定本地 Loader 文件

## 支持的认证类型

| 类型 | 说明 | 适用品牌 |
|------|------|----------|
| `none` | 无需认证 | 通用 |
| `miauth` | 小米认证 | 小米/红米 |
| `demacia` | 一加认证 | 一加/OPPO |
| `vip` | VIP 认证 | 需要 Digest + Sign |

## API 接口

云端 Loader 服务提供 REST API：

```
API 地址: https://api.sakuraedl.org/api
```

### 获取 Loader 列表

```bash
GET /api/loaders/list
```

### 匹配 Loader

```bash
POST /api/loaders/match
Content-Type: application/json

{
  "msm_id": "009600E1",
  "pk_hash": "...",
  "storage_type": "ufs"
}
```

详细 API 文档请参考 [API 文档](/api/)。

## 常见问题

### Q: 云端匹配失败怎么办？

1. 检查网络连接
2. 尝试手动选择 Loader
3. 从其他渠道获取 Loader 文件

### Q: 可以离线使用吗？

可以，手动指定本地 Loader 文件即可离线使用。

### Q: 如何贡献 Loader？

欢迎向我们提交新的 Loader 文件，联系方式见 [关于我们](/guide/faq#联系支持)。
