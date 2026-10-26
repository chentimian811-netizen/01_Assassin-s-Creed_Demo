@echo off
rem ============================================================================
rem  Luban 导出：Datas\Gameplay.xlsx  ->  生成 C# 代码 + JSON 数据
rem
rem  路径全部相对本脚本所在目录，因此工程放在任何位置都能跑，
rem  不写死 "C:\Unity\01_Assassin's Creed_Demo" 这种带空格和撇号的绝对路径。
rem
rem  Luban 生成器会【清空】代码和数据输出目录，所以只允许输出到下面两个
rem  专用生成目录；脚本会先做路径安全检查，不匹配直接拒绝执行。
rem ============================================================================
setlocal
cd /d "%~dp0"

set "LUBAN_DLL=../../Tools/Luban/Luban.dll"
set "OUT_CODE=../../Assets/GameMain/Scripts/Config/Generated"
set "OUT_DATA=../../Assets/GameMain/Resources/DataTables/Luban"

rem ---------- 输出目录安全检查 ----------
call :check_out "%OUT_CODE%" "Scripts/Config/Generated"
if errorlevel 1 goto :fail
call :check_out "%OUT_DATA%" "Resources/DataTables/Luban"
if errorlevel 1 goto :fail

if not exist "%LUBAN_DLL%" (
    echo [ERROR] 未找到 Luban.dll：%LUBAN_DLL%
    goto :fail
)
if not exist "Datas\__tables__.xlsx" (
    echo [ERROR] 未找到表注册文件：Datas\__tables__.xlsx
    goto :fail
)
if not exist "Datas\Gameplay.xlsx" (
    echo [ERROR] 未找到数据工作簿：Datas\Gameplay.xlsx
    goto :fail
)

rem 本机只有 .NET 9/10 运行时，Luban 5.1.0 目标框架是 net8.0，需要允许前滚
set DOTNET_ROLL_FORWARD=Major

echo [INFO] 开始导出 ...
dotnet "%LUBAN_DLL%" ^
    -t client ^
    -d json ^
    -c cs-simple-json ^
    --strict ^
    --conf "luban.conf" ^
    -x outputCodeDir=%OUT_CODE% ^
    -x outputDataDir=%OUT_DATA%
if errorlevel 1 goto :fail

echo [OK] Luban 导出完成
exit /b 0

rem ---------- 子过程：确认输出目录是本工程约定的专用生成目录 ----------
:check_out
rem %~1 实际输出目录，%~2 必须匹配的目录尾串
echo(%~1| findstr /i /e /c:"%~2" >nul
if errorlevel 1 (
    echo [ERROR] 拒绝执行：输出目录 "%~1" 不是专用生成目录（应形如 ...\%~2）
    exit /b 1
)
exit /b 0

:fail
echo [ERROR] Luban 导出失败，未产生可用配置；修表后重跑本脚本
exit /b 1
