# Unity-ImageLoader
用于Unity中的图片异步显示加载库  async load and display image in unity

build in Unity 5.3.4f

## 用法 Usage

ImageLoader.GetInstance().Display(...);

## 说明 Instructions

1、可以用于显示的图片类型有：网络图片、本地图片、字节流、Sprite、Texture

Can be used to display the image types are: network image, local image, byte streams, Sprite, Texture

2、带内存及磁盘缓存，默认实现：LRUDiscCache、LRUMemoryCache，也可以自定义实现缓存策略

With memory and disk cache, the default implementation: LRUDiscCache, LRUMemoryCache, can also custom caching strategies

3、默认使用BestHttp请求网络图片，可以自定义实现网络请求

By default use BestHttp to request network images, and you can customize the network request implement

4、图片异步加载使用线程池，默认最大线程数为5

Asynchronous loading images using thread pool，and the default maximum count of threads is 5
