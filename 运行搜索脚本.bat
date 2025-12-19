@echo off
chcp 65001 >nul
echo ========================================
echo Bannerlord 角色创建类型搜索工具
echo ========================================
echo.

cd /d "%~dp0"

echo 正在编译搜索脚本（64位）...
msbuild SearchCharacterCreationTypes.csproj /p:Configuration=Release /p:Platform=x64 /nologo /v:minimal

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ 编译失败！请检查：
    echo    1. 是否安装了 Visual Studio Build Tools
    echo    2. MSBuild 是否在 PATH 中
    echo.
    pause
    exit /b 1
)

echo.
echo ✅ 编译成功！
echo.
echo 正在运行搜索脚本...
echo.

bin\Release\SearchCharacterCreationTypes.exe

echo.
pause

