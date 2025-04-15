using UnityEngine;
using UnityEngine.Rendering;

public class PostFXStack : MonoBehaviour
{
    private const string bufferName = "Post FX";
    
    private CommandBuffer buffer = new CommandBuffer {
        name = bufferName
    };
    
    private ScriptableRenderContext context;

    private Camera camera;

    private PostFXSettings settings;

    public bool IsActive => settings != null;

    private int fxSourceId = Shader.PropertyToID("_PostFXSource");

    enum  Pass
    {
        Copy
    }

    public void Setup(ScriptableRenderContext context, Camera camera, PostFXSettings settings) {
        this.context = context;
        this.camera = camera;
        this.settings = camera.cameraType <= CameraType.SceneView ? settings : null;
    }

    public void Render(int sourceId) {
        Draw(sourceId, BuiltinRenderTextureType.CameraTarget, Pass.Copy);
        context.ExecuteCommandBuffer(buffer);
        buffer.Clear();
    }

    void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, Pass pass) {
        buffer.SetGlobalTexture(fxSourceId, from);
        buffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        buffer.DrawProcedural(Matrix4x4.identity, settings.Material, (int)pass, MeshTopology.Triangles, 3);
    }
    
    

}
