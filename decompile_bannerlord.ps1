# Bannerlord DLL 批量反编译脚本
# 使用 ILSpyCmd 将 Bannerlord DLL 导出到 D:\Bannerlord_Decompiled
# 包含所有重要的核心 DLL 和模块 DLL，方便以后查阅

$gameBin = "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client"
$modulesPath = "D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules"
$outRoot = "D:\Bannerlord_Decompiled"

# 需要反编译的 DLL 列表（核心系统 + 游戏模式）
# 格式：@{Path="路径"; Name="DLL名称"; Category="分类"}
$dlls = @(
    # === 核心系统 DLL（bin\Win64_Shipping_Client） ===
    @{Path=$gameBin; Name="TaleWorlds.Core.dll"; Category="核心系统"},
    @{Path=$gameBin; Name="TaleWorlds.Library.dll"; Category="核心系统"},
    @{Path=$gameBin; Name="TaleWorlds.MountAndBlade.dll"; Category="核心系统"},
    @{Path=$gameBin; Name="TaleWorlds.CampaignSystem.dll"; Category="核心系统"},
    @{Path=$gameBin; Name="TaleWorlds.MountAndBlade.ViewModelCollection.dll"; Category="核心系统"},
    @{Path=$gameBin; Name="TaleWorlds.CampaignSystem.ViewModelCollection.dll"; Category="核心系统"},
    @{Path=$gameBin; Name="TaleWorlds.GauntletUI.dll"; Category="UI系统"},
    @{Path=$gameBin; Name="TaleWorlds.MountAndBlade.GauntletUI.Widgets.dll"; Category="UI系统"},
    @{Path=$gameBin; Name="TaleWorlds.ScreenSystem.dll"; Category="核心系统"},
    @{Path=$gameBin; Name="TaleWorlds.Localization.dll"; Category="核心系统"},
    @{Path=$gameBin; Name="TaleWorlds.ObjectSystem.dll"; Category="核心系统"},
    
    # === SandBox 模块 DLL ===
    @{Path=(Join-Path $modulesPath "SandBox\bin\Win64_Shipping_Client"); Name="SandBox.dll"; Category="游戏模式"},
    @{Path=(Join-Path $modulesPath "SandBox\bin\Win64_Shipping_Client"); Name="SandBox.ViewModelCollection.dll"; Category="游戏模式"},
    @{Path=(Join-Path $modulesPath "SandBox\bin\Win64_Shipping_Client"); Name="SandBox.GauntletUI.dll"; Category="游戏模式"},
    
    # === StoryMode 模块 DLL ===
    @{Path=(Join-Path $modulesPath "StoryMode\bin\Win64_Shipping_Client"); Name="StoryMode.dll"; Category="游戏模式"},
    @{Path=(Join-Path $modulesPath "StoryMode\bin\Win64_Shipping_Client"); Name="StoryMode.ViewModelCollection.dll"; Category="游戏模式"},
    @{Path=(Join-Path $modulesPath "StoryMode\bin\Win64_Shipping_Client"); Name="StoryMode.GauntletUI.dll"; Category="游戏模式"},
    
    # === NavalDLC 模块 DLL ===
    @{Path=(Join-Path $modulesPath "NavalDLC\bin\Win64_Shipping_Client"); Name="NavalDLC.dll"; Category="DLC"},
    @{Path=(Join-Path $modulesPath "NavalDLC\bin\Win64_Shipping_Client"); Name="NavalDLC.ViewModelCollection.dll"; Category="DLC"},
    @{Path=(Join-Path $modulesPath "NavalDLC\bin\Win64_Shipping_Client"); Name="NavalDLC.GauntletUI.dll"; Category="DLC"}
)

Write-Host "=== Bannerlord DLL 批量反编译工具 ===" -ForegroundColor Cyan
Write-Host ""

# 检查 ilspycmd 是否安装
$ilspycmd = Get-Command ilspycmd -ErrorAction SilentlyContinue
if (-not $ilspycmd) {
    Write-Host "错误: 未找到 ilspycmd 工具" -ForegroundColor Red
    Write-Host ""
    Write-Host "请先安装 ILSpyCmd:" -ForegroundColor Yellow
    Write-Host "  dotnet tool install --global ilspycmd" -ForegroundColor White
    Write-Host ""
    exit 1
}

Write-Host "✓ 找到 ilspycmd: $($ilspycmd.Source)" -ForegroundColor Green
Write-Host ""

# 检查游戏目录
if (-not (Test-Path $gameBin)) {
    Write-Host "错误: 游戏目录不存在: $gameBin" -ForegroundColor Red
    exit 1
}

# 创建输出目录
New-Item -ItemType Directory -Force -Path $outRoot | Out-Null
Write-Host "输出目录: $outRoot" -ForegroundColor Green
Write-Host ""

# 开始反编译
$successCount = 0
$failCount = 0
$skipCount = 0

# 按分类分组显示
$categories = $dlls | Group-Object -Property Category
Write-Host "将反编译以下 DLL（按分类）:" -ForegroundColor Yellow
foreach ($cat in $categories) {
    Write-Host "  [$($cat.Name)]: $($cat.Count) 个 DLL" -ForegroundColor Cyan
}
Write-Host ""

foreach ($dllInfo in $dlls) {
    $dllPath = $dllInfo.Path
    $dllName = $dllInfo.Name
    $category = $dllInfo.Category
    
    $src = Join-Path $dllPath $dllName
    if (-not (Test-Path $src)) {
        Write-Host "⚠  跳过（文件不存在）: [$category] $dllName" -ForegroundColor Yellow
        $skipCount++
        continue
    }

    $dst = Join-Path $outRoot ([IO.Path]::GetFileNameWithoutExtension($dllName))
    New-Item -ItemType Directory -Force -Path $dst | Out-Null

    Write-Host "正在反编译: [$category] $dllName ..." -ForegroundColor Cyan
    
    try {
        # -p: project export（生成 .cs 文件和目录结构）
        # -o: output folder
        ilspycmd -p -o $dst $src 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            $size = (Get-ChildItem -Path $dst -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
            Write-Host "✓  完成: $dllName -> $dst (${size:N2} MB)" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "✗  失败: $dllName (退出码: $LASTEXITCODE)" -ForegroundColor Red
            $failCount++
        }
    }
    catch {
        Write-Host "✗  异常: $dllName - $($_.Exception.Message)" -ForegroundColor Red
        $failCount++
    }
    
    Write-Host ""
}

Write-Host "=== 反编译完成 ===" -ForegroundColor Cyan
Write-Host "成功: $successCount" -ForegroundColor Green
Write-Host "失败: $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host "跳过: $skipCount" -ForegroundColor $(if ($skipCount -gt 0) { "Yellow" } else { "Green" })
Write-Host ""

# 统计信息
$totalSize = (Get-ChildItem -Path $outRoot -Recurse -Directory | ForEach-Object {
    (Get-ChildItem -Path $_.FullName -Recurse -File -ErrorAction SilentlyContinue | 
     Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum
} | Measure-Object -Sum).Sum / 1MB

Write-Host "总输出大小: ${totalSize:N2} MB" -ForegroundColor Cyan
Write-Host ""

# 提示下一步操作
Write-Host "=== 使用提示 ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "所有反编译的源码已保存到: $outRoot" -ForegroundColor White
Write-Host ""
Write-Host "搜索示例（PowerShell）:" -ForegroundColor White
Write-Host "  # 搜索 CharacterCreation 相关类型" -ForegroundColor Gray
Write-Host "  Get-ChildItem -Path `"$outRoot`" -Recurse -Filter `"*.cs`" | Select-String -Pattern `"CharacterCreation`"" -ForegroundColor Cyan
Write-Host ""
Write-Host "  # 搜索特定类名" -ForegroundColor Gray
Write-Host "  Get-ChildItem -Path `"$outRoot`" -Recurse -Filter `"*.cs`" | Select-String -Pattern `"class.*InitialMenuVM`"" -ForegroundColor Cyan
Write-Host ""
Write-Host "  # 搜索特定方法" -ForegroundColor Gray
Write-Host "  Get-ChildItem -Path `"$outRoot`" -Recurse -Filter `"*.cs`" | Select-String -Pattern `"TrySwitchToNextMenu`"" -ForegroundColor Cyan
Write-Host ""
Write-Host "如果已安装 ripgrep (rg):" -ForegroundColor White
Write-Host "  rg -n `"CharacterCreation|InitialMenuVM|TrySwitchToNextMenu`" $outRoot" -ForegroundColor Cyan
Write-Host ""

