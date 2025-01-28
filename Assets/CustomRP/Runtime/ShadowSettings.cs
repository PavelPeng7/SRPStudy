using UnityEngine;

[System.Serializable]
public class ShadowSettings
{
    // 定义最大阴影距离，默认为 100，最小值为 0
    [Min(0f)]
    public float maxDistance = 100f;

    // 定义阴影贴图大小，使用 2 的幂，范围从 256 到 8192
    public enum MapSize
    {
        _256 = 256, _512 = 512, _1024 = 1024,
        _2048 = 2048, _4096 = 4096, _8192 = 8192
    }

    [System.Serializable]
    public struct Directional
    {
        // 方向光阴影贴图大小，默认为 1024
        public MapSize atlasSize;
    }

    // 创建默认方向光阴影设置
    public Directional directional = new Directional
    {
        atlasSize = MapSize._1024
    };
}