# API 文档

## 概述

SakuraEDL 提供 REST API 用于云端 Loader 匹配和下载。

**API 地址**: `https://api.sakuraedl.org/api`

## 认证

公开 API 无需认证，管理 API 需要在请求头中携带 Token：

```
X-Admin-Token: your-token
```

## 公开 API

### 获取 Loader 列表

获取所有可用的 Loader 列表。

**请求**
```http
GET /api/loaders/list
```

**参数**
| 参数 | 类型 | 说明 |
|------|------|------|
| vendor | string | 可选，按厂商筛选 |
| storage_type | string | 可选，ufs 或 emmc |

**响应**
```json
{
  "code": 0,
  "message": "获取成功",
  "data": {
    "count": 2,
    "loaders": [
      {
        "id": 1,
        "filename": "Xiaomi_SM8150_common.elf",
        "vendor": "xiaomi",
        "chip": "SM8150",
        "auth_type": "miauth",
        "storage_type": "ufs",
        "file_size": 712928,
        "display_name": "[xiaomi] SM8150 - Xiaomi_SM8150_common.elf"
      }
    ]
  }
}
```

---

### 下载 Loader

下载指定 ID 的 Loader 文件。

**请求**
```http
GET /api/loaders/{id}/download
```

**响应**

返回二进制文件流。

**示例**
```bash
curl -O https://api.sakuraedl.org/api/loaders/1/download
```

---

### 下载 Digest 文件

下载 VIP Loader 的 Digest 认证文件。

**请求**
```http
GET /api/loaders/{id}/digest
```

**响应**

返回二进制文件流，如果 Loader 不是 VIP 类型则返回 404。

---

### 下载 Sign 文件

下载 VIP Loader 的 Sign 认证文件。

**请求**
```http
GET /api/loaders/{id}/sign
```

**响应**

返回二进制文件流，如果 Loader 不是 VIP 类型则返回 404。

---

### 匹配 Loader

根据设备信息匹配合适的 Loader。

**请求**
```http
POST /api/loaders/match
Content-Type: application/json

{
  "msm_id": "009600E1",
  "pk_hash": "ABCD1234...",
  "oem_id": "0001",
  "storage_type": "ufs"
}
```

**响应**
```json
{
  "code": 0,
  "message": "匹配成功",
  "data": {
    "loader_id": 1,
    "filename": "Xiaomi_SM8150_common.elf",
    "auth_type": "miauth",
    "download_url": "/api/loaders/1/download"
  }
}
```

---

## 错误码

| 错误码 | 说明 |
|--------|------|
| 0 | 成功 |
| 400 | 请求参数错误 |
| 401 | 未授权 |
| 404 | 资源不存在 |
| 500 | 服务器错误 |

## 请求示例

### cURL

```bash
# 获取列表
curl https://api.sakuraedl.org/api/loaders/list

# 下载 Loader
curl -O https://api.sakuraedl.org/api/loaders/1/download

# 匹配 Loader
curl -X POST https://api.sakuraedl.org/api/loaders/match \
  -H "Content-Type: application/json" \
  -d '{"msm_id":"009600E1","storage_type":"ufs"}'
```

### C#

```csharp
using var client = new HttpClient();

// 获取列表
var response = await client.GetAsync("https://api.sakuraedl.org/api/loaders/list");
var json = await response.Content.ReadAsStringAsync();

// 下载 Loader
var loaderData = await client.GetByteArrayAsync("https://api.sakuraedl.org/api/loaders/1/download");
```

### Python

```python
import requests

# 获取列表
response = requests.get("https://api.sakuraedl.org/api/loaders/list")
data = response.json()

# 下载 Loader
response = requests.get("https://api.sakuraedl.org/api/loaders/1/download")
with open("loader.elf", "wb") as f:
    f.write(response.content)
```
