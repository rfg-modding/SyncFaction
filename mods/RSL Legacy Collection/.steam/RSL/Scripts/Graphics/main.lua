-------------------------------------------
--GRAPHICS IMPROVEMENT SCRIPT BY CAMO v1.00
-------------------------------------------

local function SetGraphicsSettings()
--LOGGING    
    rsl.Log("Graphics Improvement v1.00 15-11-2022 Build 1219 Loaded\n")

--MESSAGE POPUPS 
    rfg.AddUiMessage ("GRAPHICS: To apply fixes:\nDisable SHADOWS and turn them back to high", 5, true)

--GENERAL
    rfg.Misc.DistortionScale = 0                                      --Default 0.09
    rfg.Misc.DistortionBlurScale = 0                                  --Default 2.00

--DEPTH OF FIELD
    rfg.Dof.FocusStartA = 15.0                                           --Default 1.0
    rfg.Dof.FocusStartB = 25.0                                        --Default 3.0
    rfg.Dof.FocusEndA = 100.0                                         --Default 3.0
    rfg.Dof.FocusEndB = 150.0                                         --Default 100.0
    
--SHADOWS
    rfg.SetShadowResolutions(5120, 7168, 10240, 10240)                --Resolution of shadow cascades from right to left
    rfg.Shadows.ShadowMapFadePercent = 0.25                           --Default 0.8
    rfg.Shadows.DropShadowPercent = 1.0                               --Default 0.4
    rfg.Shadows.ShadowPercent = 0.75                                  --Default 1.0
                                            
--CLOUD SHADOWS
    rfg.Shadows.CloudShadowScale = 0.5                                --Default 0.5
    rfg.Shadows.CloudShadowIntensityScale = 4.5                       --Default 4.5
                                                                      
--HDR AND BLOOM
    rfg.Hdr.BloomAmount = 0.2                                            --Default 2.5
    rfg.Hdr.LuminanceMin = 0.001                                      --Default 0.001
    rfg.Hdr.LuminanceMax = 2.5                                          --Default 4.0
    --rfg.Lighting.CoronaAdaptionRate = 0.3                              --Default 0.6
    rfg.Hdr.HdrLevel = 0.25                                                  --Default 0.6
    rfg.Hdr.EyeAdaptionBase = 0.15                                      --Default 0.2
    rfg.Hdr.EyeAdaptionAmount = 0.35                                  --Default 0.5

--LIGHTING
    rfg.Lighting.SubstanceSpecAlphaScale = 9.0                          --Default 7.0
    rfg.Lighting.SubstanceSpecPowerScale = 18.0                       --Default 16.0
    rfg.Lighting.SubstanceFresnelAlphaScale = 1.50                      --Default 1.0
    rfg.Lighting.SubstanceFresnelPowerScale = 0.95                    --Default 0.6
    rfg.Lighting.AmbientSpecularScale = 0.95                          --Default 0.57
    rfg.Lighting.GlassFresnelPower = 2.8                              --Default 1.4
    rfg.Lighting.IndirectLightScale = 0.035                           --Default 0.019
    rfg.Lighting.IndirectLightAmount = 0.75                           --Default 0.5
    
--SSAO
    rfg.Ssao.Intensity = 8.25                                            --Default 4.0
    rfg.Ssao.ImagePlanePixelsPerMeterFactor = 1.25                      --Default 0.32
    rfg.Ssao.Radius = 1.75                                              --Default 1.2                      
    rfg.Ssao.DepthFadeRange = 7.5                                        --Default 1.0

--SUN SHAFTS
    rfg.SunShafts.Scale = 33.0                                           --Default 1.0
    rfg.SunShafts.Radius = 33.0                                       --Default 5.0
    rfg.SunShafts.BlurMultiplier = 2.0                                --Default 1.0                                  
    rfg.SunShafts.BaseLum = 1.30                                      --Default 1.0
    rfg.SunShafts.LumStepScale = 1.30                                 --Default 1.0
    rfg.SunShafts.UseHalfResSource = false                              --Default true
    
--LOD
    rfg.Terrain.FadeStart = 5120                                        --Default 140.0, Max 11562.0 for anything LOD related.                  
    rfg.Terrain.FadeEnd = 5120                                        --Default 250.0
    rfg.SetFarClip(5120)               
    rfg.SetHighLodFarClip(5120)
    rfg.LodInfo.Dist = 5120
    rfg.ObjectRenderDistance = 5120
    
--SHADOW LOD
    rfg.Shadows.ShadowMapMaxDist = 132.0                              --Default 100
    rfg.Shadows.TerrainShadowMaxDist = 267.0                          --Default 240.0        
    
--MISC
    rfg.Terrain.AnisotropyLevel = 0                                      --Default 8, force it in your graphics driver
    rfg.Misc.FxaaEnabled = false                                       --Default true, reduces blur when off
    
--TEST 
    --rfg.WorldZone.CurSize()
    rfg.WorldZone.MaxSize = 2147483647
end    
local function OnLoad()
    SetGraphicsSettings()
end
local function Initialized()
    SetGraphicsSettings()
end
rfg.RegisterEvent("Initialized", Initialized, "[GFXI]")
rfg.RegisterEvent("Load", OnLoad, "[GFXI]")







