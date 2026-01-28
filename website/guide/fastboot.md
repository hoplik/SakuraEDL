# Fastboot 模式教程

## 概述

Fastboot 是 Android 设备的标准刷机模式，支持大部分 Android 设备。SakuraEDL 提供了友好的图形界面来执行 Fastboot 操作。

## 进入 Fastboot 模式

### 通用方法

1. 完全关机
2. 同时按住 **音量-** + **电源键**
3. 出现 Fastboot 界面后松开

### ADB 命令

```bash
adb reboot bootloader
```

### 品牌特定

| 品牌 | 组合键 |
|------|--------|
| 小米 | 音量- + 电源 |
| 华为 | 音量- + 电源 |
| 三星 | 音量- + Bixby + 电源 |
| 一加 | 音量+ + 音量- + 电源 |
| Google | 音量- + 电源 |

## 验证连接

设备管理器中应显示：
- `Android Bootloader Interface`

命令行验证：
```bash
fastboot devices
```

## 使用 SakuraEDL

### 1. 选择 Fastboot 平台

打开 SakuraEDL，点击 **Fastboot** 选项卡。

### 2. 连接设备

1. 让设备进入 Fastboot 模式
2. 连接 USB 数据线
3. 软件自动检测并显示设备信息

### 3. 常用操作

#### 刷写分区

1. 选择目标分区
2. 选择镜像文件
3. 点击 **刷写**

#### 解锁 Bootloader

1. 确认设备已开启 OEM 解锁
2. 点击 **解锁 BL**
3. 按设备提示确认

#### 锁定 Bootloader

1. 点击 **锁定 BL**
2. 按设备提示确认

::: danger 警告
锁定 BL 会清除所有数据！
:::

## AB 分区

新款设备使用 AB 分区系统：

### 分区对照

| 传统分区 | AB 分区 |
|----------|---------|
| boot | boot_a / boot_b |
| system | system_a / system_b |
| vendor | vendor_a / vendor_b |

### 刷写 AB 分区

1. 软件自动检测 AB 分区
2. 选择要刷写的 slot (a 或 b)
3. 或选择 **自动** 刷写当前活动分区

### 切换活动分区

```bash
fastboot set_active a
# 或
fastboot set_active b
```

## Payload 刷机

OTA 包使用 `payload.bin` 格式：

### 使用方法

1. 点击 **加载 Payload**
2. 选择 `payload.bin` 文件
3. 选择要刷写的分区
4. 点击 **开始**

### 支持的格式

- `payload.bin` (OTA 包)
- `*.zip` (自动提取 payload.bin)

## 常用命令参考

### 基本命令

```bash
# 查看设备
fastboot devices

# 刷写分区
fastboot flash boot boot.img
fastboot flash system system.img

# 擦除分区
fastboot erase userdata
fastboot erase cache

# 重启
fastboot reboot
fastboot reboot bootloader
fastboot reboot recovery
```

### 解锁命令

```bash
# 通用
fastboot oem unlock
fastboot flashing unlock

# 小米
fastboot oem unlock-go
```

### 其他命令

```bash
# 获取变量
fastboot getvar all
fastboot getvar product
fastboot getvar serialno

# 启动镜像（不刷写）
fastboot boot boot.img
```

## 常见问题

### Q: 设备不识别？

1. 检查 Fastboot 驱动是否安装
2. 尝试不同 USB 端口
3. 检查 USB 数据线

### Q: 刷写失败？

1. 检查镜像文件是否正确
2. 检查分区名称
3. 确认 BL 是否已解锁

### Q: 解锁失败？

1. 确认已在开发者选项中开启 OEM 解锁
2. 小米设备需要绑定账号并等待
3. 部分设备不支持解锁

## 注意事项

::: warning 警告
- 解锁 BL 会清除所有数据
- 刷写前备份重要数据
- 使用正确版本的镜像
- 不要随意刷写未知文件
:::

::: tip 提示
建议先备份以下分区：
- `boot` - 内核
- `recovery` - 恢复模式
- `persist` - 持久数据
- `efs` / `modem` - 基带
:::
