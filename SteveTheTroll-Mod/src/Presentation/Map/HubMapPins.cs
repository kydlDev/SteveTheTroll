using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using KydlCore.Common;
using SteveTheTroll.Configuration;
using SteveTheTroll.Features.Hubs.Infrastructure;
using UnityEngine;

namespace SteveTheTroll.Presentation.Map;

/// <summary>
///     2026-11-06 (user: "add a map marker for the hub, the word HUB, even the hub's icon; all hubs get an icon; players cannot remove hub pins; a pin goes only when the hub is removed"):
///     one map pin per Troll Hub that the registry knows (every owner's), with the hub's own plaque icon (the word HUB) and its name. The pins are not saved with the map (made again every
///     session). A player cannot remove one: the game's RemovePin is refused for them (HubsPatches), and a pin that was lost anyway is made again at the next refresh.
/// </summary>
internal static class HubMapPins
{
    private const float RefreshSeconds = 2f;
    private static readonly Dictionary<string, Minimap.PinData> Pins = new();
    private static readonly MethodInfo AddPinMethod = AccessTools.Method(typeof(Minimap), "AddPin");
    private static readonly FieldInfo PinsField = AccessTools.Field(typeof(Minimap), "m_pins");
    private static Minimap _map;
    private static Sprite _icon;
    private static bool _iconLooked;
    private static float _next;

    /// True while this class removes a pin itself (the hub is gone); the patch lets that one through.
    internal static bool Removing;

    /// Is this pin a hub pin (the patch refuses the player's removal)? Expected outcome: true for a pin made here.
    internal static bool IsHubPin(Minimap.PinData pin)
    {
        return pin != null && Pins.ContainsValue(pin);
    }

    /// Plugin.Update. Expected outcome: how many hub pins exist after this refresh (-1 = nothing done this frame).
    internal static int Tick()
    {
        if (Time.time < _next)
        {
            return -1;
        }

        _next = Time.time + RefreshSeconds;
        Minimap map = Minimap.instance;
        if (map != _map)
        {
            Pins.Clear();
            _map = map;
        }

        if (map == null || Player.m_localPlayer == null || !ModSettings.On)
        {
            return 0;
        }

        Sprite icon = Icon();
        var alive = new HashSet<string>();
        foreach (HubInfo h in HubRegistry.All())
        {
            string key = h.Zdo != null ? h.Zdo.m_uid.ToString() : h.Number + "@" + h.Pos.ToString("F0");
            alive.Add(key);
            string label = h.Name;
            Minimap.PinData pin;
            bool known = Pins.TryGetValue(key, out pin) && pin != null;
            var onMap = PinsField != null ? PinsField.GetValue(map) as List<Minimap.PinData> : null;
            if (known && onMap != null && !onMap.Contains(pin))
            {
                known = false; // the pin was taken off the map some other way: made again
                Pins.Remove(key);
            }

            if (!known)
            {
                pin = Add(map, h.Pos, label);
                if (pin == null)
                {
                    continue;
                }

                if (icon != null)
                {
                    pin.m_icon = icon;
                }

                Pins[key] = pin;
                ModLog.Info("HubMapPins: pin '" + label + "' at " + h.Pos.ToString("F0") + " (icon " + (icon != null) + ")");
                continue;
            }

            pin.m_pos = h.Pos;
            if (pin.m_name != label)
            {
                pin.m_name = label;
            }
        }

        var gone = new List<string>();
        foreach (KeyValuePair<string, Minimap.PinData> kv in Pins)
        {
            if (!alive.Contains(kv.Key))
            {
                gone.Add(kv.Key);
            }
        }

        foreach (string key in gone)
        {
            Removing = true;
            try
            {
                map.RemovePin(Pins[key]);
            }
            finally
            {
                Removing = false;
            }

            Pins.Remove(key);
            ModLog.Info("HubMapPins: the hub of pin " + key + " is gone - its pin is removed");
        }

        return Pins.Count;
    }

    private static Sprite Icon()
    {
        if (_iconLooked)
        {
            return _icon;
        }

        _iconLooked = true;
        _icon = SteveTheTroll.Integrations.Unity.EmbeddedImages.Sprite(HubPrefab.IconResource);
        if (_icon == null)
        {
            ModLog.Warn("HubMapPins: the hub icon is not embedded - the game's pin icon is used");
        }

        return _icon;
    }

    private static Minimap.PinData Add(Minimap map, Vector3 pos, string name)
    {
        if (AddPinMethod == null)
        {
            ModLog.Warn("HubMapPins: Minimap.AddPin not found");
            return null;
        }

        ParameterInfo[] ps = AddPinMethod.GetParameters();
        object[] args = new object[ps.Length];
        args[0] = pos;
        args[1] = Minimap.PinType.Icon3;
        args[2] = name;
        args[3] = false;
        args[4] = false;
        for (int i = 5; i < ps.Length; i++)
        {
            args[i] = ps[i].ParameterType == typeof(long) ? 0L :
                ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
        }

        return AddPinMethod.Invoke(map, args) as Minimap.PinData;
    }
}