using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AquaSys.AquaEffect
{
    public class AquaBloomRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class AquaBloomSettings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public AquaBloomSettings settings = new AquaBloomSettings();

        AquaBloomRenderPass m_ScriptablePass;

        [SerializeField]
        Shader shader;

        Material material;

        public override void Create()
        {
            m_ScriptablePass = new AquaBloomRenderPass("AquaBloom");
            m_ScriptablePass.renderPassEvent = settings.renderPassEvent;
#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("AquaBloom t:Shader"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("Shaders/AquaBloom"))
                {
                    shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(path);
                    break;
                }
            }
#endif
            if (material == null)
            {
                material = CoreUtils.CreateEngineMaterial(shader);
            }
            if (material == null)
                Debug.LogError("Error,material is Null!");
        }


        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var src = renderer.cameraColorTarget;
            m_ScriptablePass.Setup(src, material);
            renderer.EnqueuePass(m_ScriptablePass);
        }

        class AquaBloomRenderPass : ScriptableRenderPass
        {
            AquaBloom aquaBloom;

            Material material;

            public string targetName;
            string profilerTag;

            bool FantasticBloom;

            public int Iteration;
            public float RTDownScaling;

            public float Intensity;

            public bool BlurOnly;

            bool allowMSAA;

            float DirtIntensity;

            Texture DirtTexture;

            bool LensDirt;

            static class ShaderIDs
            {

                internal static readonly int MainTex = Shader.PropertyToID("_MainTex");
                internal static readonly int OriginTex = Shader.PropertyToID("_OriginTex");
                internal static readonly int GlowTex = Shader.PropertyToID("_GlowTex");

                internal static readonly int _Bloom_Params = Shader.PropertyToID("_Bloom_Params");
                internal static readonly int Filtering_Params = Shader.PropertyToID("_Filtering_Params");

                internal static readonly int BlurOffset = Shader.PropertyToID("_BlurOffset");
                internal static readonly int FullScreenBloomIntensity = Shader.PropertyToID("_FullScreenBloomIntensity");
                internal static readonly int FantasticIntensity = Shader.PropertyToID("_FantasticIntensity");
                internal static readonly int FantasticTint = Shader.PropertyToID("_FantasticTint");

                
                internal static readonly int LensDirt = Shader.PropertyToID("_LensDirt");
                internal static readonly int DirtTexture = Shader.PropertyToID("_LensDirt_Texture");
                internal static readonly int DirtIntensity = Shader.PropertyToID("_LensDirtIntensity");
                internal static readonly int LensDirt_Params = Shader.PropertyToID("_LensDirt_Params");

                internal static readonly int SourceTexLowMip = Shader.PropertyToID("_SourceTexLowMip");
            }

            struct Level
            {
                internal int down;
                internal int up;
            }

            Level[] blurPyramids;
            Level[] glowPyramids;
            const int maxPyramidSize = 8;
            const int maxBlurPyramidSize = 3;

            private RenderTargetIdentifier source { get; set; }
            int sourceTmp;

            public void Setup(RenderTargetIdentifier source, Material material)
            {
                this.source = source;
                this.material = material;
            }

            public AquaBloomRenderPass(string profilerTag)
            {
                this.profilerTag = profilerTag;

                var stack = VolumeManager.instance.stack;
                aquaBloom = stack.GetComponent<AquaBloom>();

                glowPyramids = new Level[maxPyramidSize];

                blurPyramids = new Level[maxBlurPyramidSize];

                for (int i = 0; i < maxPyramidSize; i++)
                {
                    glowPyramids[i] = new Level
                    {
                        down = Shader.PropertyToID("_GlowMipDown" + i),
                        up = Shader.PropertyToID("_GlowMipUp" + i)
                    };
                }

                for (int i = 0; i < maxBlurPyramidSize; i++)
                {
                    blurPyramids[i] = new Level
                    {
                        down = Shader.PropertyToID("_FantasyBloomMipDown" + i),
                        up = Shader.PropertyToID("_FantasyBloomMipUp" + i)
                    };
                }
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get(profilerTag);
                Render(cmd, ref renderingData);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            void Render(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ref var cameraData = ref renderingData.cameraData;
                if (aquaBloom.IsActive() && !cameraData.isSceneViewCamera && cameraData.postProcessEnabled)
                {
                    SetParamters();
                    allowMSAA = cameraData.camera.allowMSAA;
                    SetupAquaBloom(cmd, ref renderingData, material);
                }
            }

            void SetParamters()
            {
                #region Bloom
                var tint = aquaBloom.tint.value.linear;
                var luma = ColorUtils.Luminance(tint);
                tint = luma > 0f ? tint * (1f / luma) : Color.white;
                var bloomParams = new Vector4(aquaBloom.Intensity.value, tint.r, tint.g, tint.b);
                material.SetVector(ShaderIDs._Bloom_Params, bloomParams);
                #endregion

                #region Pre-filtering parameters
                float clamp = 65472f;
                float scatter = Mathf.Lerp(0.05f, 0.95f, aquaBloom.scatter.value);
                float threshold = Mathf.GammaToLinearSpace(aquaBloom.Threshold.value);
                float thresholdKnee = threshold * 0.5f;
                material.SetVector(ShaderIDs.Filtering_Params, new Vector4(scatter, clamp, threshold, thresholdKnee));
                #endregion

                #region Quality
                Iteration = aquaBloom.Iteration.value;
                RTDownScaling = aquaBloom.RTDownScaling.value;
                material.SetFloat(ShaderIDs.BlurOffset, aquaBloom.BlurOffset.value);
                #endregion

                #region Lens Dirt
                DirtIntensity = aquaBloom.dirtIntensity.value;
                DirtTexture = aquaBloom.dirtTexture.value;

                if (DirtIntensity == 0 || DirtTexture == null)
                {
                    material.SetFloat(ShaderIDs.LensDirt, 0);
                    LensDirt = false;
                }
                else
                {
                    material.SetFloat(ShaderIDs.LensDirt, 1);
                    material.SetFloat(ShaderIDs.DirtIntensity, DirtIntensity);
                    material.SetTexture(ShaderIDs.DirtTexture, DirtTexture);
                    LensDirt = true;
                }

                #endregion

                #region Fantastic Bloom
                FantasticBloom = aquaBloom.FantasticBloom.value;
                if (FantasticBloom)
                {
                    material.SetColor(ShaderIDs.FantasticTint, aquaBloom.FantasticTint.value);
                    material.SetFloat(ShaderIDs.FantasticIntensity, aquaBloom.FantasticIntensity.value);
                    material.SetFloat(ShaderIDs.FullScreenBloomIntensity, aquaBloom.FullScreenBloomIntensity.value);
                }
                #endregion

                #region Others
                BlurOnly = aquaBloom.BlurOnly.value;
                #endregion
            }

            public void SetupAquaBloom(CommandBuffer cmd, ref RenderingData renderingData, Material material)
            {
                RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
                
                opaqueDesc.graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                float screenRatio = opaqueDesc.width / (float)opaqueDesc.height;
                if (LensDirt)
                {
                    float dirtRatio = DirtTexture.width / (float)DirtTexture.height;
                    var dirtScaleOffset = new Vector4(1f, 1f, 0f, 0f);

                    if (dirtRatio > screenRatio)
                    {
                        dirtScaleOffset.x = screenRatio / dirtRatio;
                        dirtScaleOffset.z = (1f - dirtScaleOffset.x) * 0.5f;
                    }
                    else if (screenRatio > dirtRatio)
                    {
                        dirtScaleOffset.y = dirtRatio / screenRatio;
                        dirtScaleOffset.w = (1f - dirtScaleOffset.y) * 0.5f;
                    }

                    material.SetVector(ShaderIDs.LensDirt_Params, dirtScaleOffset);
                }

                if (!allowMSAA)
                {
                    sourceTmp = Shader.PropertyToID("_SourceTmp");
                    cmd.GetTemporaryRT(sourceTmp, opaqueDesc.width, opaqueDesc.height, 0, FilterMode.Bilinear);
                    cmd.Blit(source, sourceTmp);
                }

                #region FullScreenBloom
                int lastUp = 0;

                if (FantasticBloom || BlurOnly)
                {
                    #region DownSample
                    int downWidth = 640;
                    int downHeight = (int)(640 / screenRatio);
                    RenderTargetIdentifier lastDown = allowMSAA ? source : sourceTmp;

                    for (int i = 0; i < maxBlurPyramidSize; i++)
                    {
                        int mipDown = blurPyramids[i].down;
                        int mipUp = blurPyramids[i].up;

                        cmd.GetTemporaryRT(mipDown, downWidth, downHeight, 0, FilterMode.Bilinear);
                        cmd.GetTemporaryRT(mipUp, downWidth, downHeight, 0, FilterMode.Bilinear);

                        Blit(ShaderIDs.MainTex, cmd, lastDown, mipDown, material, 1);

                        lastDown = mipDown;

                        downWidth = Mathf.Max(1, downWidth >> 1);
                        downHeight = Mathf.Max(1, downHeight >> 1);
                    }
                    #endregion

                    #region UpSample
                    lastUp = blurPyramids[maxBlurPyramidSize - 1].down;

                    for (int i = maxBlurPyramidSize - 2; i >= 0; i--)
                    {
                        int mipUp = blurPyramids[i].up;

                        Blit(ShaderIDs.MainTex, cmd, lastUp, mipUp, material, 2);

                        lastUp = mipUp;
                    }
                    #endregion
                }

                #endregion

                #region Glow
                if (BlurOnly)
                {
                    cmd.Blit(lastUp, allowMSAA ? source : sourceTmp);

                    #region ReleaseTemporary
                    for (int i = 0; i < maxBlurPyramidSize; i++)
                    {
                        cmd.ReleaseTemporaryRT(blurPyramids[i].down);
                        cmd.ReleaseTemporaryRT(blurPyramids[i].up);
                    }

                    if (!allowMSAA)
                        cmd.ReleaseTemporaryRT(sourceTmp);
                    #endregion
                }
                else
                {
                    #region DownScaling
                    int tw = (int)(opaqueDesc.width / RTDownScaling);
                    int th = (int)(opaqueDesc.height / RTDownScaling);

                    int lastDownGlow = glowPyramids[0].down;
                    int lastUpGlow = glowPyramids[0].up;

                    cmd.GetTemporaryRT(lastDownGlow, tw, th, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                    cmd.GetTemporaryRT(lastUpGlow, tw, th, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);

                    Blit(ShaderIDs.MainTex, cmd, allowMSAA ? source : sourceTmp, lastDownGlow, material, 0);
                    #endregion

                    #region DownSample
                    for (int i = 1; i < Iteration; i++)
                    {
                        int mipDownGlow = glowPyramids[i].down;
                        int mipUpGlow = glowPyramids[i].up;

                        tw = Mathf.Max(1, tw >> 1);
                        th = Mathf.Max(1, th >> 1);

                        cmd.GetTemporaryRT(mipDownGlow, tw, th, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                        cmd.GetTemporaryRT(mipUpGlow, tw, th, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);

                        Blit(ShaderIDs.MainTex, cmd, lastDownGlow, mipDownGlow, material, 1);

                        lastDownGlow = mipDownGlow;
                    }
                    #endregion

                    #region UpSample
                    for (int i = Iteration - 2; i >= 0; i--)
                    {
                        int lowMipGlow = (i == Iteration - 2) ? glowPyramids[i + 1].down : glowPyramids[i + 1].up;
                        int highMipGlow = glowPyramids[i].down;
                        lastUpGlow = glowPyramids[i].up;
                        cmd.SetGlobalTexture(ShaderIDs.SourceTexLowMip, lowMipGlow);
                        Blit(ShaderIDs.MainTex, cmd, highMipGlow, lastUpGlow, material, 3);
                    }
                    #endregion

                    #region Final
                    cmd.SetGlobalTexture(ShaderIDs.OriginTex, allowMSAA ? source : sourceTmp);

                    if (FantasticBloom)
                    {
                        cmd.SetGlobalTexture(ShaderIDs.GlowTex, lastUpGlow);
                        Blit(ShaderIDs.MainTex, cmd, lastUp, source, material, 5);
                    }
                    else
                    {
                        Blit(ShaderIDs.MainTex, cmd, lastUpGlow, source, material, 4);
                    }
                    #endregion

                    #region ReleaseTemporary
                    if (FantasticBloom)
                    {
                        for (int i = 0; i < maxBlurPyramidSize; i++)
                        {
                            cmd.ReleaseTemporaryRT(blurPyramids[i].down);
                            cmd.ReleaseTemporaryRT(blurPyramids[i].up);
                        }
                    }

                    for (int i = 0; i < Iteration; i++)
                    {
                        cmd.ReleaseTemporaryRT(glowPyramids[i].down);
                        cmd.ReleaseTemporaryRT(glowPyramids[i].up);
                    }

                    if (!allowMSAA)
                        cmd.ReleaseTemporaryRT(sourceTmp);
                    #endregion
                }
                #endregion
            }

            void Blit(int texID, CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier destination, Material material, int passIndex = 0)
            {
                cmd.SetGlobalTexture(texID, source);
                cmd.SetRenderTarget(destination, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
                cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, material, 0, passIndex);
                //cmd.Blit(source, destination, material, passIndex);
            }
        }
    }
}

