extern alias GSD2;

using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PKMatildaGlitchRestoration.Patches;

/// <summary>
/// Restores the classic PS1 Matilda Gate push glitch in Highway Village / Muse-Matilda Border (vi11)
/// and attaches the HD gate texture ('t_vi11_00_obj_gate01_01') to the moving 2D gate object (eventobj[6]).
///
/// Grounding & Mechanics:
/// - 2D Simulation Layer: In vi11.json, mapeventdat[0].eventobj[6] (at x=16, y=51, otyp=3, ano=12)
///   is the closed wooden border gate. Konami changed its 'wt' field from 1 to 255 in the HD Remaster
///   to prevent pushing it. Setting wt = 1 in MACHICON.EventInit restores the pushable gate behavior.
/// - HD Visual Layer: Every frame, the 2D engine calls MapBGManagerHD.RefleshObject(id: 6, pos, ...),
///   passing the gate's live position in Unity HD local-space coordinates. In the vanilla vi11_00 HD prefab,
///   't_vi11_00_obj_gate01_01' is not wired with positionSync=true on a calibrated MapEventObjectHD for objID=6,
///   causing the HD gate sprite to remain stationary while the 2D hitbox moves.
/// - By capturing the initial HD localPosition and initial RefleshObject pos on map load, we apply the exact
///   live movement delta (pos - initialPos) to the HD gate transform every frame as it is pushed.
/// </summary>
public static class MatildaGatePatch
{
    public const int GateObjectIndex = 6;
    public const int OpenGateObjectIndex = 7;
    public const int GateEventMapNo = 0;

    public static string CurrentMapId = "";

    public static readonly HashSet<string> TargetGateSpriteNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "t_vi11_00_obj_gate01_01"
    };

    public static readonly HashSet<string> TargetAllGateSpriteNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "t_vi11_00_obj_gate01_01",
        "t_vi11_00_obj_gate01_02"
    };

    private static Texture2D _transparent1x1;
    private static Sprite _transparent1x1Sprite;

    private struct GateTransformEntry
    {
        public Transform Transform;
        public Vector3 InitialLocalPosition;
    }

    private static readonly List<GateTransformEntry> _gateTargets = new();
    private static bool _hasInitialPos;
    private static Vector2 _initialPos;
    private static bool _loggedMovementStart;

    private static bool IsDisableGateCompletelyEnabled =>
        Plugin.Config != null && Plugin.Config.IsOpenMode();

    private static bool IsPushGlitchEnabled =>
        Plugin.Config != null && Plugin.Config.IsPushMode();

    public static Texture2D GetTransparent1x1()
    {
        if (_transparent1x1 == null)
        {
            _transparent1x1 = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _transparent1x1.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            _transparent1x1.Apply(false, true);
            _transparent1x1.name = "Transparent1x1_MatildaGate";
            UnityEngine.Object.DontDestroyOnLoad(_transparent1x1);
        }
        return _transparent1x1;
    }

    public static Sprite GetTransparent1x1Sprite()
    {
        if (_transparent1x1Sprite == null)
        {
            var tex = GetTransparent1x1();
            _transparent1x1Sprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
            _transparent1x1Sprite.name = "Transparent1x1Sprite_MatildaGate";
            UnityEngine.Object.DontDestroyOnLoad(_transparent1x1Sprite);
        }
        return _transparent1x1Sprite;
    }

    public static void Initialize(Harmony harmony)
    {
        try
        {
            harmony.PatchAll(typeof(EventInitPatch));
            harmony.PatchAll(typeof(OnMapInitPatch));
            harmony.PatchAll(typeof(SetMapPatch));
            harmony.PatchAll(typeof(MapSpriteHDAwakePatch));
            harmony.PatchAll(typeof(RefleshObjectPatch));
            Plugin.Log.LogInfo("[MatildaGatePatch] Registered EventInit, OnMapInit, SetMap, MapSpriteHD & RefleshObject hooks successfully.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[MatildaGatePatch] Failed to register hooks: {ex}");
        }
    }

    private static bool IsMatildaBorderMap()
    {
        return !string.IsNullOrEmpty(CurrentMapId) &&
               CurrentMapId.StartsWith("vi11", StringComparison.OrdinalIgnoreCase);
    }

    private static void ResetGateSyncState()
    {
        _gateTargets.Clear();
        _hasInitialPos = false;
        _initialPos = Vector2.zero;
        _loggedMovementStart = false;
    }

    public static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        string clean = System.Text.RegularExpressions.Regex.Replace(name, @"\s*\(\d+\)", "");
        clean = clean.Replace("(Clone)", "").Trim();
        return clean;
    }

    private static bool IsGateGameObject(GameObject go, HashSet<string> targetSet = null)
    {
        if (go == null) return false;
        targetSet ??= TargetGateSpriteNames;

        string cleanGoName = NormalizeName(go.name);
        if (targetSet.Contains(cleanGoName))
            return true;

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            string spriteName = NormalizeName(sr.sprite.name);
            if (targetSet.Contains(spriteName))
                return true;

            if (sr.sprite.texture != null)
            {
                string texName = NormalizeName(sr.sprite.texture.name);
                if (targetSet.Contains(texName))
                    return true;
            }
        }

        var r = go.GetComponent<Renderer>();
        if (r != null && r.sharedMaterial != null && r.sharedMaterial.mainTexture != null)
        {
            string matTexName = NormalizeName(r.sharedMaterial.mainTexture.name);
            if (targetSet.Contains(matTexName))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Force-loads a 1x1 transparent pixel texture and sprite onto target HD gate GameObjects,
    /// scales them to zero, moves them off-screen, and disables their renderers.
    /// </summary>
    public static bool ForceTransparent1x1(GameObject go)
    {
        if (go == null) return false;

        string cleanName = NormalizeName(go.name);
        bool match = TargetAllGateSpriteNames.Contains(cleanName);

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;

            if (!match && r.sharedMaterial != null && r.sharedMaterial.mainTexture != null)
            {
                string texClean = NormalizeName(r.sharedMaterial.mainTexture.name);
                if (TargetAllGateSpriteNames.Contains(texClean))
                    match = true;
            }

            if (!match && r is SpriteRenderer srCheck && srCheck.sprite != null)
            {
                string spriteClean = NormalizeName(srCheck.sprite.name);
                string sprTexClean = srCheck.sprite.texture != null ? NormalizeName(srCheck.sprite.texture.name) : "";
                if (TargetAllGateSpriteNames.Contains(spriteClean) || (!string.IsNullOrEmpty(sprTexClean) && TargetAllGateSpriteNames.Contains(sprTexClean)))
                {
                    match = true;
                }
            }

            if (match)
            {
                if (r.material != null)
                    r.material.mainTexture = GetTransparent1x1();
                if (r.sharedMaterial != null)
                    r.sharedMaterial.mainTexture = GetTransparent1x1();

                if (r is SpriteRenderer sr)
                {
                    sr.sprite = GetTransparent1x1Sprite();
                    sr.color = Color.clear;
                }

                r.enabled = false;
            }
        }

        if (match)
        {
            go.transform.position = new Vector3(99999f, 99999f, 99999f);
            go.transform.localScale = Vector3.zero;
            go.SetActive(false);
            Plugin.Log.LogInfo($"[MatildaGatePatch] Disabled HD gate texture & GameObject '{go.name}' (normalized: '{cleanName}').");
            return true;
        }

        return false;
    }

    public static void CleanHdGateVisuals(GSD2::MapBGManagerHD bgManager = null)
    {
        try
        {
            if (!IsDisableGateCompletelyEnabled || !IsMatildaBorderMap())
                return;

            int hiddenCount = 0;

            if (bgManager != null && bgManager.eventObjects != null)
            {
                for (int i = 0; i < bgManager.eventObjects.Count; i++)
                {
                    var hdObj = bgManager.eventObjects[i];
                    if (hdObj != null &&
                        (hdObj.objID == GateObjectIndex || hdObj.objID == OpenGateObjectIndex) &&
                        (hdObj.eventMapNo == -1 || hdObj.eventMapNo == GateEventMapNo) &&
                        hdObj.gameObject != null)
                    {
                        hdObj.gameObject.transform.position = new Vector3(99999f, 99999f, 99999f);
                        hdObj.gameObject.transform.localScale = Vector3.zero;
                        hdObj.gameObject.SetActive(false);
                        hiddenCount++;
                    }
                }
            }

            Renderer[] renderers = null;
            if (bgManager != null && bgManager.asset != null)
            {
                renderers = bgManager.asset.GetComponentsInChildren<Renderer>(true);
            }
            else
            {
                renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
            }

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null || r.gameObject == null) continue;
                    if (ForceTransparent1x1(r.gameObject))
                    {
                        hiddenCount++;
                    }
                }
            }

            if (hiddenCount > 0)
            {
                Plugin.Log.LogInfo($"[MatildaGatePatch] Cleaned {hiddenCount} HD gate visual(s) on map '{CurrentMapId}'.");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"[MatildaGatePatch] Error in CleanHdGateVisuals: {ex}");
        }
    }

    private static void Apply2DGateDataPatch(GSD2::MACHIDAT mdat)
    {
        var mapEvents = mdat?.eventdata?.mapeventdat;
        if (mapEvents == null || mapEvents.Count <= GateEventMapNo || mapEvents[GateEventMapNo]?.eventobj == null)
            return;

        var room0 = mapEvents[GateEventMapNo];
        var objs = room0.eventobj;

        if (IsDisableGateCompletelyEnabled)
        {
            // Fully disable both closed gate (6) and open gate (7) in 2D map data
            if (objs.Length > GateObjectIndex && objs[GateObjectIndex] != null)
            {
                objs[GateObjectIndex].disp = 0;
                objs[GateObjectIndex].ozok = 0;
                objs[GateObjectIndex].wt = 0;
                objs[GateObjectIndex].x = 0;
                objs[GateObjectIndex].y = 0;
            }

            if (objs.Length > OpenGateObjectIndex && objs[OpenGateObjectIndex] != null)
            {
                objs[OpenGateObjectIndex].disp = 0;
                objs[OpenGateObjectIndex].ozok = 0;
                objs[OpenGateObjectIndex].wt = 0;
                objs[OpenGateObjectIndex].x = 0;
                objs[OpenGateObjectIndex].y = 0;
            }

            // In vi11 Room 0, eventdat[2..5] are auto-init scripts (etyp=2) that execute opcode [0, 14]
            // (turn on closed gate ehuman[14]) or [0, 15] (turn on open gate ehuman[15]) on frame 1.
            // Replace opcode 0 (DispOn) with 1 (DispOff) for slots 14 and 15 while preserving 100% exact byte length.
            var eventDatList = room0.eventdat;
            if (eventDatList != null)
            {
                for (int e = 2; e <= 5 && e < eventDatList.Length; e++)
                {
                    var ed = eventDatList[e];
                    if (ed?.eventCom == null) continue;
                    var com = ed.eventCom;
                    for (int b = 0; b + 1 < com.Length; b++)
                    {
                        if (com[b] == 0 && (com[b + 1] == 14 || com[b + 1] == 15))
                        {
                            com[b] = 1;
                        }
                    }
                }
            }
        }
        else if (IsPushGlitchEnabled)
        {
            if (objs.Length > GateObjectIndex && objs[GateObjectIndex] != null)
            {
                objs[GateObjectIndex].wt = 1;
            }
        }
    }

    private static void DiscoverGateTargets(GSD2::MapBGManagerHD bgManager)
    {
        _gateTargets.Clear();
        var candidateTransforms = new List<Transform>();

        // 1. Check MapEventObjectHD entries for objID == 6 in room 0
        if (bgManager != null && bgManager.eventObjects != null)
        {
            for (int i = 0; i < bgManager.eventObjects.Count; i++)
            {
                var hdObj = bgManager.eventObjects[i];
                if (hdObj != null &&
                    hdObj.objID == GateObjectIndex &&
                    (hdObj.eventMapNo == -1 || hdObj.eventMapNo == GateEventMapNo) &&
                    hdObj.transform != null)
                {
                    if (!candidateTransforms.Contains(hdObj.transform))
                    {
                        candidateTransforms.Add(hdObj.transform);
                    }
                }
            }
        }

        // 2. Scan renderers under MapBGManagerHD.asset (or active scene fallback) for 't_vi11_00_obj_gate01_01'
        Renderer[] renderers = null;
        if (bgManager != null && bgManager.asset != null)
        {
            renderers = bgManager.asset.GetComponentsInChildren<Renderer>(true);
        }
        else
        {
            renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
        }

        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r != null && r.gameObject != null && IsGateGameObject(r.gameObject, TargetGateSpriteNames))
                {
                    if (!candidateTransforms.Contains(r.transform))
                    {
                        candidateTransforms.Add(r.transform);
                    }
                }
            }
        }

        // 3. Filter out any transform whose ancestor is already in candidateTransforms so we never double-move children
        for (int i = 0; i < candidateTransforms.Count; i++)
        {
            var t = candidateTransforms[i];
            if (t == null) continue;

            bool hasMatchedAncestor = false;
            Transform parent = t.parent;
            while (parent != null)
            {
                if (candidateTransforms.Contains(parent))
                {
                    hasMatchedAncestor = true;
                    break;
                }
                parent = parent.parent;
            }

            if (!hasMatchedAncestor)
            {
                _gateTargets.Add(new GateTransformEntry
                {
                    Transform = t,
                    InitialLocalPosition = t.localPosition
                });
                Plugin.Log.LogInfo($"[MatildaGatePatch] Attached HD gate target '{t.name}' at initial localPosition={t.localPosition}.");
            }
        }
    }

    [HarmonyPatch(typeof(GSD2::MACHICON), "EventInit")]
    public static class EventInitPatch
    {
        [HarmonyPrefix]
        public static void Prefix(GSD2::MACHICON __instance)
        {
            try
            {
                if (!IsDisableGateCompletelyEnabled && !IsPushGlitchEnabled)
                    return;

                if (__instance?.mdat?.eventdata?.mapeventdat == null)
                    return;

                string mapId = __instance.mdat.name;
                if (string.IsNullOrEmpty(mapId))
                {
                    string raw = __instance.GetMapName();
                    if (!string.IsNullOrEmpty(raw))
                    {
                        mapId = raw.Split(' ')[0];
                    }
                }

                if (!string.IsNullOrEmpty(mapId) && mapId.StartsWith("vi11", StringComparison.OrdinalIgnoreCase))
                {
                    Apply2DGateDataPatch(__instance.mdat);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[MatildaGatePatch] Error in EventInitPatch.Prefix: {ex}");
            }
        }

        [HarmonyPostfix]
        public static void Postfix(GSD2::MACHICON __instance, GSD2::EVENTCON __result)
        {
            try
            {
                if (!IsDisableGateCompletelyEnabled && !IsPushGlitchEnabled)
                    return;

                if (__result == null || __result.mdat == null)
                    return;

                string mapId = __result.mdat.name;
                if (string.IsNullOrEmpty(mapId) && __instance != null)
                {
                    string raw = __instance.GetMapName();
                    if (!string.IsNullOrEmpty(raw))
                    {
                        mapId = raw.Split(' ')[0];
                    }
                }

                CurrentMapId = mapId ?? "";
                ResetGateSyncState();

                if (!IsMatildaBorderMap())
                    return;

                Apply2DGateDataPatch(__result.mdat);

                int closedGateSlot = GateObjectIndex + 8;
                int openGateSlot = OpenGateObjectIndex + 8;

                if (IsDisableGateCompletelyEnabled)
                {
                    if (__result.ehuman != null)
                    {
                        if (__result.ehuman.Count > closedGateSlot && __result.ehuman[closedGateSlot] != null)
                        {
                            var h = __result.ehuman[closedGateSlot];
                            h.disp = 0;
                            h.ozok = 0;
                            h.wt = 0;
                        }

                        if (__result.ehuman.Count > openGateSlot && __result.ehuman[openGateSlot] != null)
                        {
                            var h = __result.ehuman[openGateSlot];
                            h.disp = 0;
                            h.ozok = 0;
                            h.wt = 0;
                        }
                    }

                    CleanHdGateVisuals();
                    Plugin.Log.LogInfo($"[MatildaGatePatch] Fully disabled Matilda Gate collision & 2D objects on '{CurrentMapId}'.");
                }
                else if (IsPushGlitchEnabled)
                {
                    if (__result.ehuman != null && __result.ehuman.Count > closedGateSlot && __result.ehuman[closedGateSlot] != null)
                    {
                        byte oldLiveWt = __result.ehuman[closedGateSlot].wt;
                        __result.ehuman[closedGateSlot].wt = 1;
                        Plugin.Log.LogInfo($"[MatildaGatePatch] Restored pushable Matilda Gate on '{CurrentMapId}': eventobj[{GateObjectIndex}].wt=1, live ehuman[{closedGateSlot}].wt {oldLiveWt} -> 1.");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[MatildaGatePatch] Error in EventInitPatch.Postfix: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(GSD2::MACHICON), "OnMapInit")]
    public static class OnMapInitPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (IsDisableGateCompletelyEnabled && IsMatildaBorderMap())
            {
                CleanHdGateVisuals();
            }
        }
    }

    [HarmonyPatch(typeof(GSD2::MapBGManagerHD), "SetMap")]
    public static class SetMapPatch
    {
        [HarmonyPostfix]
        public static void Postfix(GSD2::MapBGManagerHD __instance)
        {
            try
            {
                ResetGateSyncState();

                if (!IsMatildaBorderMap())
                    return;

                if (IsDisableGateCompletelyEnabled)
                {
                    CleanHdGateVisuals(__instance);
                }
                else if (IsPushGlitchEnabled)
                {
                    DiscoverGateTargets(__instance);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[MatildaGatePatch] Error in SetMapPatch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(MapSpriteHD), "Awake")]
    public static class MapSpriteHDAwakePatch
    {
        [HarmonyPostfix]
        public static void Postfix(MapSpriteHD __instance)
        {
            try
            {
                if (!IsDisableGateCompletelyEnabled)
                    return;

                if (__instance == null || __instance.gameObject == null)
                    return;

                ForceTransparent1x1(__instance.gameObject);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[MatildaGatePatch] Error in MapSpriteHDAwakePatch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(GSD2::MapBGManagerHD), "RefleshObject")]
    public static class RefleshObjectPatch
    {
        [HarmonyPrefix]
        public static void Prefix(GSD2::MapBGManagerHD __instance, int id, Vector2 pos, int eventMapNo)
        {
            try
            {
                if (!IsPushGlitchEnabled)
                    return;

                if (!IsMatildaBorderMap() || id != GateObjectIndex || eventMapNo != GateEventMapNo)
                    return;

                // Capture untouched initial localPosition before RefleshObject / SetPosition runs
                if (_gateTargets.Count == 0)
                {
                    DiscoverGateTargets(__instance);
                }

                if (!_hasInitialPos)
                {
                    _initialPos = pos;
                    _hasInitialPos = true;

                    // If a MapEventObjectHD component exists on objID == 6, calibrate its positionOffset & positionSync
                    if (__instance != null && __instance.eventObjects != null)
                    {
                        for (int i = 0; i < __instance.eventObjects.Count; i++)
                        {
                            var hdObj = __instance.eventObjects[i];
                            if (hdObj != null &&
                                hdObj.objID == GateObjectIndex &&
                                (hdObj.eventMapNo == -1 || hdObj.eventMapNo == GateEventMapNo) &&
                                hdObj.transform != null)
                            {
                                Vector3 initLocal = hdObj.transform.localPosition;
                                hdObj.doorObject = false;
                                hdObj.positionSync = true;
                                hdObj.positionOffset = new Vector2(initLocal.x - _initialPos.x, initLocal.y - _initialPos.y);
                            }
                        }
                    }

                    Plugin.Log.LogInfo($"[MatildaGatePatch] Captured initial 2D gate RefleshObject pos={_initialPos} for {_gateTargets.Count} HD gate target(s).");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[MatildaGatePatch] Error in RefleshObjectPatch.Prefix: {ex}");
            }
        }

        [HarmonyPostfix]
        public static void Postfix(GSD2::MapBGManagerHD __instance, int id, Vector2 pos, int eventMapNo)
        {
            try
            {
                if (!IsPushGlitchEnabled)
                    return;

                if (!IsMatildaBorderMap() || id != GateObjectIndex || eventMapNo != GateEventMapNo || !_hasInitialPos)
                    return;

                Vector2 delta = pos - _initialPos;

                for (int i = _gateTargets.Count - 1; i >= 0; i--)
                {
                    var entry = _gateTargets[i];
                    if (entry.Transform == null)
                    {
                        _gateTargets.RemoveAt(i);
                        continue;
                    }

                    Vector3 initLocal = entry.InitialLocalPosition;
                    entry.Transform.localPosition = new Vector3(
                        initLocal.x + delta.x,
                        initLocal.y + delta.y,
                        initLocal.z
                    );
                }

                if (!_loggedMovementStart && delta.sqrMagnitude > 0.001f)
                {
                    _loggedMovementStart = true;
                    Plugin.Log.LogInfo($"[MatildaGatePatch] Syncing pushed HD gate sprite 't_vi11_00_obj_gate01_01' with 2D gate object (delta={delta}).");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[MatildaGatePatch] Error in RefleshObjectPatch.Postfix: {ex}");
            }
        }
    }
}
