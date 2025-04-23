using System;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public class CameraSettings
{
    public bool overridePostFX = false;
    public PostFXSettings PostFXSettings = default;
    public int renderingLayerMask = -1;
    public bool maskLights = false;
    
    [Serializable]
    public struct FinalBlendMode
    {
        public BlendMode source, destination;
    }

    public FinalBlendMode finalBlendMode = new FinalBlendMode {
        source = BlendMode.One,
        destination = BlendMode.Zero
    };
}
