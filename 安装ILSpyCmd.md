# 安装 ILSpyCmd 工具

## 步骤 1: 检查 .NET SDK

确保已安装 .NET SDK（6.0 或更高版本）：

```powershell
dotnet --version
```

如果没有安装，请从 [Microsoft 官网](https://dotnet.microsoft.com/download) 下载安装。

## 步骤 2: 安装 ILSpyCmd

在 PowerShell 中运行：

```powershell
dotnet tool install --global ilspycmd
```

## 步骤 3: 验证安装

```powershell
ilspycmd --version
```

如果显示版本号，说明安装成功。

## 步骤 4: 运行反编译脚本

```powershell
cd "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\QuickStartMod"
.\decompile_bannerlord.ps1
```

## 常见问题

### Q: 提示 "dotnet 不是内部或外部命令"
A: 需要安装 .NET SDK，或将其添加到 PATH 环境变量。

### Q: 提示 "ilspycmd 不是内部或外部命令"
A: 确保已成功安装 ILSpyCmd，并且 `%USERPROFILE%\.dotnet\tools` 在 PATH 中。

### Q: 反编译失败
A: 检查 DLL 文件是否存在，以及是否有足够的磁盘空间。

