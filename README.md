# Unity-ImageLoader
用于Unity中的图片异步显示加载库  async load and display image in unity

build in Unity 5.3.4f

## 用法 Usage

```c#
ImageLoader.GetInstance().Display(...);
```

## 说明 Instructions

1、可以用于显示的图片类型有：网络图片、本地图片、字节流、Sprite、Texture

Can be used to display the image types are: network image, local image, byte streams, Sprite, Texture

2、带内存及磁盘缓存，默认实现：LRUDiscCache、LRUMemoryCache，也可以自定义实现缓存策略

With memory and disk cache, the default implementation: LRUDiscCache, LRUMemoryCache, can also custom caching strategies

3、默认使用BestHttp请求网络图片，可以自定义实现网络请求

By default use BestHttp to request network images, and you can customize the network request implement

4、图片异步加载使用线程池，默认最大线程数为5

Asynchronous loading images using thread pool，and the default maximum count of threads is 5

## License

MIT License

Copyright (c) 2017 Hcq

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
