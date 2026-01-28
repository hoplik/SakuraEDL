# 分区操作

## 概述

SakuraEDL 支持对设备分区进行读取、写入、擦除等操作。

## 分区类型

### 系统分区

| 分区名 | 说明 | 可否刷写 |
|--------|------|----------|
| boot | 启动镜像 | ✅ 可刷写 |
| recovery | 恢复模式 | ✅ 可刷写 |
| system | 系统分区 | ✅ 可刷写 |
| vendor | 供应商分区 | ✅ 可刷写 |
| product | 产品分区 | ✅ 可刷写 |
| odm | ODM 分区 | ✅ 可刷写 |

### 关键分区 (谨慎操作)

| 分区名 | 说明 | 风险级别 |
|--------|------|----------|
| persist | 校准数据 | ⚠️ 高风险 |
| modemst1/2 | 基带校准 | ⚠️ 高风险 |
| fsg | 基带文件 | ⚠️ 高风险 |
| nvram | NV 数据 (MTK) | ⚠️ 高风险 |
| nvdata | NV 数据 (MTK) | ⚠️ 高风险 |
| frp | 恢复出厂保护 | ⚠️ 中风险 |

### 引导分区 (禁止操作)

| 分区名 | 说明 | 风险级别 |
|--------|------|----------|
| xbl | 引导加载器 | ❌ 极高风险 |
| abl | 安卓引导 | ❌ 极高风险 |
| preloader | 预加载器 (MTK) | ❌ 极高风险 |
| lk | Little Kernel (MTK) | ❌ 极高风险 |

## 基本操作

### 读取分区表

1. 连接设备并加载 Loader
2. 点击 **"读取分区表"**
3. 分区列表显示所有分区信息

### 备份分区

1. 选择要备份的分区
2. 点击 **"读取分区"**
3. 选择保存位置
4. 等待备份完成

**建议备份：**
- boot
- recovery
- persist
- modemst1/modemst2 (高通)
- nvram/nvdata (MTK)

### 刷写分区

1. 选择要刷写的分区
2. 点击 **"写入分区"**
3. 选择镜像文件
4. 确认并等待完成

### 擦除分区

1. 选择要擦除的分区
2. 点击 **"擦除分区"**
3. 确认操作

⚠️ **警告：** 擦除关键分区可能导致设备变砖！

## A/B 分区

新设备使用 A/B 分区方案：

```
boot_a / boot_b
system_a / system_b
vendor_a / vendor_b
```

### 查看当前槽位

```bash
fastboot getvar current-slot
```

### 切换槽位

```bash
fastboot set_active a
# 或
fastboot set_active b
```

### 刷写 A/B 分区

建议同时刷写两个槽位确保完整性：

```bash
fastboot flash boot_a boot.img
fastboot flash boot_b boot.img
```

## Super 分区

Android 10+ 使用动态分区，系统分区位于 super 分区内：

```
super
├── system
├── vendor
├── product
└── odm
```

### 刷写 Super 分区

1. 直接刷写完整 super.img
2. 或使用 fastbootd 模式刷写子分区

## 常见问题

### Q: 刷写后设备无法开机？

1. 检查镜像文件是否正确
2. 确认分区名称无误
3. 尝试恢复备份
4. 刷写完整固件包

### Q: 如何恢复误擦除的分区？

1. 使用之前的备份恢复
2. 从官方固件提取对应分区
3. 联系售后获取帮助

### Q: IMEI 丢失怎么办？

1. 恢复 modemst1/modemst2 (高通) 或 nvram (MTK) 备份
2. 使用工程模式写入
3. 联系官方售后
