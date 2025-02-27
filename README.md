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

# Reference

1. The Tus, ["Unity URP14.0自定义后处理系统"](https://zhuanlan.zhihu.com/p/621840900)
2. Unity手册["Unity Documentation"](https://docs.unity3d.com/2022.3/Documentation/Manual/)
