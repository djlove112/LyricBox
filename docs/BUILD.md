# 源码导览与构建

LyricBox 1.0.4 已公开的模块位于 [src](../src)    完整桌面界面和播放器接入组件暂未随本次源码发布

| 功能 | 源文件 |
| --- | --- |
| 歌词行与逐字进度 | [歌词行](../src/LyricLine.cs)　[进度计算](../src/LyricProgressCalculator.cs) |
| 多行滚动 | [显示时间轴](../src/LyricDisplayTimeline.cs) |
| 壁纸摆动 | [摆动轨迹](../src/NativeCameraSwayMotion.cs) |
| 星轨与拖影 | [星轨运动历史](../src/StarMotionHistory.cs)　[拖影运动](../src/TrailMotionEnvelope.cs) |
| 音乐律动 | [律动响应](../src/RhythmSpeakerDrive.cs) |
| 星空绘制 | [背景着色器](../src/Background.hlsl) |
| 三份出厂布局 | [布局预设](../src/FactoryLayouts.json) |

## 构建类库

安装 .NET 10 SDK 后在仓库根目录运行

```powershell
dotnet build LyricBox.Core.csproj -c Release
```

生成的类库位于 `bin/Release/net10.0/LyricBox.Core.dll`
