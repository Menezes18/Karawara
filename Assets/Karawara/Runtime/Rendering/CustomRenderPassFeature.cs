using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CustomRenderPassFeature : ScriptableRendererFeature
{
    class CustomRenderPass : ScriptableRenderPass
    {
        private Material material;

        private class PassData
        {
            public TextureHandle source;
            public TextureHandle temporary;
            public Material material;
        }

        public CustomRenderPass(Material material)
        {
            this.material = material;
            // The effect samples camera color, which cannot be the back buffer.
            requiresIntermediateTexture = true;
        }

        public void SetMaterial(Material newMaterial)
        {
            material = newMaterial;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
            {
                Debug.LogWarning("Material não configurado para o Custom Render Pass.");
                return;
            }

            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer)
            {
                Debug.LogWarning("Custom Render Pass requires an intermediate camera color texture.");
                return;
            }

            var source = resources.activeColorTexture;
            var descriptor = renderGraph.GetTextureDesc(source);
            descriptor.name = "_TemporaryColorTexture";
            descriptor.clearBuffer = false;
            descriptor.depthBufferBits = DepthBits.None;
            descriptor.msaaSamples = MSAASamples.None;
            var temporary = renderGraph.CreateTexture(descriptor);

            using (var builder = renderGraph.AddUnsafePass<PassData>("CustomBlitRenderPass", out var data))
            {
                data.source = source;
                data.temporary = temporary;
                data.material = material;
                builder.UseTexture(source, AccessFlags.ReadWrite);
                builder.UseTexture(temporary, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (PassData pass, UnsafeGraphContext context) =>
                {
                    var commandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    RTHandle color = pass.source;
                    RTHandle temp = pass.temporary;
                    // Preserve the existing shader's _MainTex and vert_img contract.
                    // Blitter's procedural geometry requires a different vertex shader.
                    commandBuffer.Blit(color.nameID, temp.nameID, pass.material, 0);
                    commandBuffer.Blit(temp.nameID, color.nameID);
                });
            }
        }
    }

    [System.Serializable]
    public class Settings
    {
        public Material material = null;
    }
    public Settings settings = new Settings();
    CustomRenderPass m_ScriptablePass;
    private readonly System.Collections.Generic.List<MaterialLease> materialOwners = new System.Collections.Generic.List<MaterialLease>();
    private Material runtimeBaseMaterial;
    private bool runtimeBaseAssigned;
    public Material CurrentMaterial => materialOwners.Count > 0 ? materialOwners[materialOwners.Count - 1].material : runtimeBaseAssigned ? runtimeBaseMaterial : settings.material;
    public System.IDisposable AcquireMaterial(object owner, Material material)
    {
        if (owner == null || material == null) throw new System.ArgumentNullException(owner == null ? nameof(owner) : nameof(material));
        var lease = new MaterialLease(this, material); materialOwners.Add(lease);
        m_ScriptablePass?.SetMaterial(CurrentMaterial); return lease;
    }
    private sealed class MaterialLease : System.IDisposable
    {
        private CustomRenderPassFeature feature;
        internal readonly Material material;
        internal MaterialLease(CustomRenderPassFeature feature, Material material) { this.feature = feature; this.material = material; }
        public void Dispose()
        {
            var owner = feature; if (ReferenceEquals(owner, null)) return; feature = null;
            owner.materialOwners.Remove(this); owner.m_ScriptablePass?.SetMaterial(owner.CurrentMaterial);
        }
    }

    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass(CurrentMaterial);
        m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }

    public void UpdateMaterial(Material newMaterial)
    {
        runtimeBaseMaterial = newMaterial; runtimeBaseAssigned = true;
        if (m_ScriptablePass != null)
        {
            m_ScriptablePass.SetMaterial(CurrentMaterial);
        }
    }
}
