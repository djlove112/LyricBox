<p align="center"><img src="lyricbox.png" width="92" alt="LyricBox 图标"></p>

# LyricBox

让歌词漫过桌面

LyricBox 是一款免费的 Windows 桌面歌词应用  播放音乐时可以逐字高亮歌词  搭配翻译和音乐频谱  也可以用自己的图片或视频做壁纸

![星空壁纸上的双层歌词效果](preview-starfield.png)

## 当前版本与下载

桌面版 **1.0.4** 面向 Windows x64    本仓库首批公开歌词时间轴与桌面视觉效果的源码    Windows 安装包和便携包暂未在此发布

GitHub 的 **Download ZIP** 下载的是源码    如果想体验完整桌面程序  请留意本仓库后续的发布说明

## 桌面版能做什么

| 功能 | 使用方式 |
| --- | --- |
| 桌面歌词 | 连接主流音乐播放器  选择单行或多行显示  有逐字时间信息的歌词可随播放进度扫光 |
| 歌词排版 | 分别设置原文和翻译的字体与颜色  调整它们在桌面上的位置  将常用布局保存为预设 |
| 壁纸 | 使用星空效果或选择本地图片与视频  开启壁纸摆动后可调整幅度和周期 |
| 音乐频谱 | 在桌面显示随音乐变化的频谱  搭配歌词和壁纸使用 |
| 界面语言 | 可以在桌面程序设置中切换 29 种界面语言 |

<details>
<summary>查看布局编辑器</summary>

![歌词布局编辑器中的字体与颜色设置](preview-layout.png)

</details>

## 阅读与构建源码

从 [src](src) 开始    [歌词进度计算](src/LyricProgressCalculator.cs) 把播放位置换算为当前行的进度    [显示时间轴](src/LyricDisplayTimeline.cs) 决定多行模式应滚动到哪一行

| 想了解的效果 | 对应文件 |
| --- | --- |
| 壁纸如何摆动 | [摆动轨迹](src/NativeCameraSwayMotion.cs) |
| 星轨与歌词拖影如何延伸 | [星轨运动历史](src/StarMotionHistory.cs) 和 [拖影运动](src/TrailMotionEnvelope.cs) |
| 声音如何带动桌面元素 | [律动响应](src/RhythmSpeakerDrive.cs) |
| 星空如何绘制 | [背景着色器](src/Background.hlsl) |
| 三份出厂布局如何配置 | [布局预设](src/FactoryLayouts.json) |

安装 .NET 10 SDK 后在仓库根目录运行

```powershell
dotnet build LyricBox.Core.csproj -c Release
```

构建产物是包含上述 C# 模块的类库    完整桌面界面和播放器接入组件暂未随本次源码发布

## 用户数据

安装版将配置和缓存保存在 `%LOCALAPPDATA%\LyricBox\UserData`    便携版保存在程序旁的 `UserData` 文件夹    卸载时可选择保留或删除

## 授权

本仓库中的 LyricBox 自有源码按 **GPL-3.0-only** 发布    歌词与第三方素材的权利归各自权利人所有    [查看许可证全文](LICENSE)
