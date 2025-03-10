# CustomPostProcessing

Custom Post Processing System with Unity URP.

Unity有两种使用后处理的方法。第一种使用Global Volume,但仅限于使用内置后处理，自定义后处理需要修改URP,很麻烦；第二种使用自定义RenderFeature添加自定义RenderPass,但一个后处理效果对应两个脚本，并且会带来多个RenderPass不停申请和获取纹理的消耗。<br>
在此基础上创建一个自定义后处理系统，优化渲染流程，创建一个后处理基类CustomPostProcessing.cs，所有的后处理效果都继承这个基类，实现自己的渲染逻辑。然后创建一个CustomRenderFeature挂载到Renderer上，获取所有Volume中继承CustomPostProcessing.cs的子类，根据注入点和顺序创建对应的RenderPass(相同注入点的后处理放到一个RenderPass中，在相同纹理签名间Blit,节省性能消耗)，再调用EnqueuePass函数，每个RenderPass在Execute函数中调用对应CustomPostProcessing的Render函数渲染。

# Features
* Depth of Field
* Color Adjustment
* Split Toning
* Channel Mixer
* Chromatic Aberration
* Bloom （some unknown error）

# Gallery

原图
![room](https://github.com/user-attachments/assets/87c1068c-e130-4e97-b345-2fe0f434c301)
ChannelMixer
![ChannelMixer](https://github.com/user-attachments/assets/ea074c12-bb88-4b38-8323-ca1d18e56c82)
ChromaticAberration
![ChromaticAberration](https://github.com/user-attachments/assets/af447a04-213c-47a2-9838-1ecba1e429bc)
ColorAdjustments
![ColorAdjustment](https://github.com/user-attachments/assets/236de2fb-ffa0-4d16-a945-d4a9efb1d10c)
SplitToning
![SplitToning](https://github.com/user-attachments/assets/094712a8-0492-4925-88dd-61746b8b59c2)
DepthOfField
![DepthOfField](https://github.com/user-attachments/assets/ccc63b51-9ee0-4eed-a983-1acbcc9e4b5b)

Unity URP自带SSAO
![自带SSAO](https://github.com/user-attachments/assets/ad224e96-4be0-4315-ac3e-dfda118dc575)
Custom SSAO
![SSAO](https://github.com/user-attachments/assets/ad5f2242-8f49-4875-bede-f71dcfa93395)
Custom HBAO
![HBAO](https://github.com/user-attachments/assets/50b4b9b6-1814-479e-a1be-e094e2a94f09)


# Reference

1. The Tus, ["Unity URP14.0自定义后处理系统"](https://zhuanlan.zhihu.com/p/621840900)
2. Unity手册["Unity Documentation"](https://docs.unity3d.com/2022.3/Documentation/Manual/)
3. 张亚坤, ["游戏中的后处理(六) 景深"](https://zhuanlan.zhihu.com/p/146143501)
4. 闫令琪, ["Games202"](https://www.bilibili.com/video/BV1YK4y1T7yY/?p=7)
5. ["Learn OpenGL"](https://learnopengl-cn.github.io/05%20Advanced%20Lighting/09%20SSAO/)
