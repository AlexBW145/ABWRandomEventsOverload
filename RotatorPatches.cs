using ABWEvents.Events;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace ABWEvents.Patches;

[HarmonyPatch(typeof(AnimatedSpriteRotator))]
class RotatorPatches
{
    [HarmonyPatch("LateUpdate"), HarmonyPostfix]
    private static void GuideMapUpdates(AnimatedSpriteRotator __instance, ref SpriteRenderer ___renderer, SpriteRotationMap[] ___spriteMap, bool ___bypassRotation,
        int ___currentMapId, int ___currentSpriteId, int ___spriteIdOffset)
    {
        if (!___bypassRotation && CoreGameManager.Instance.GetCamera(0) != null)
        {
            if (___spriteMap[___currentMapId] is SpriteRotationGuideMap)
            {
                var guidemap = (SpriteRotationGuideMap)___spriteMap[___currentMapId];
                var prop = new MaterialPropertyBlock();
                ___renderer.GetPropertyBlock(prop);
                prop.SetTexture("_ColorGuide", guidemap.GuideSprite(___currentSpriteId + ___spriteIdOffset).texture);
                ___renderer.SetPropertyBlock(prop);
            }
        }
    }
}