using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Docky.RiggleTools.Editor
{
    internal static class RiggleBodyParts
    {
        internal sealed class Scope
        {
            public Transform root;
            public string label;
            public HashSet<string> bones;
            public bool limb, separate;
        }

        internal static Scope Resolve(Transform source)
        {
            if (source.TryGetComponent<RiggleBodyPart>(out var settings))
                return Make(source, settings.kind, settings.side, settings.includeHandOrFoot,
                    settings.includeNeck, settings.keepSeparateRenderer);
            return TryName(source.name, out var kind, out var side, out var terminal, out var neck)
                ? Make(source, kind, side, terminal, neck, false) : null;
        }

        internal static bool TryName(string name, out RiggleBodyPart.Kind kind, out RiggleBodyPart.Side side,
            out bool terminal, out bool neck)
        {
            kind = RiggleBodyPart.Kind.Torso; side = RiggleBodyPart.Side.Left; terminal = neck = false;
            name = Regex.Replace(name.Trim(), @"(?:\s*\(\d+\)|\.\d{3})$", "");
            if (name.StartsWith("Riggle_", StringComparison.OrdinalIgnoreCase)) name = name.Substring(7);
            name = Regex.Replace(name, @"([a-z0-9])([A-Z])", "$1 $2");
            var tokens = Regex.Split(name.ToLowerInvariant(), "[^a-z0-9]+").Where(t => t.Length > 0).ToList();
            string markedSide = null;
            bool SideToken(string s) => s == "left" || s == "right" || s == "l" || s == "r" || s == "middle" || s == "m";
            string NormalizeSide(string s) => s == "left" || s == "l" ? "left" : s == "middle" || s == "m" ? "middle" : "right";
            if (tokens.Count > 1 && SideToken(tokens[0]))
            { markedSide = NormalizeSide(tokens[0]); tokens.RemoveAt(0); }
            if (tokens.Count > 1 && SideToken(tokens.Last()))
            {
                string suffix = NormalizeSide(tokens.Last());
                if (markedSide != null && markedSide != suffix) return false;
                markedSide = suffix; tokens.RemoveAt(tokens.Count - 1);
            }
            string part = string.Concat(tokens);
            if (markedSide != null)
            {
                side = markedSide == "left" ? RiggleBodyPart.Side.Left : markedSide == "middle" ? RiggleBodyPart.Side.Middle : RiggleBodyPart.Side.Right;
                if(part=="ponytail"){kind=RiggleBodyPart.Kind.Ponytail;return true;}
                if(markedSide=="middle")return false;
                if (part == "arm" || part == "armwithhand" || part == "armwithouthand")
                { kind = RiggleBodyPart.Kind.Arm; terminal = part == "armwithhand"; return true; }
                if (part == "leg" || part == "legwithfoot" || part == "legwithoutfoot")
                { kind = RiggleBodyPart.Kind.Leg; terminal = part == "legwithfoot"; return true; }
                if (part == "ear" || part == "bunnyear") { kind = RiggleBodyPart.Kind.Ear; return true; }
                if (part == "wing") { kind = RiggleBodyPart.Kind.Wing; return true; }
                return false;
            }
            if(part=="ponytail"){kind=RiggleBodyPart.Kind.Ponytail;side=RiggleBodyPart.Side.Middle;return true;}
            if (part == "tail") { kind = RiggleBodyPart.Kind.Tail; return true; }
            if (part == "torso" || part == "torsowithneck" || part == "torsowithoutneck")
            { kind = RiggleBodyPart.Kind.Torso; neck = part == "torsowithneck"; return true; }
            if (part == "head" || part == "headwithneck" || part == "headwithoutneck")
            { kind = RiggleBodyPart.Kind.Head; neck = part == "headwithneck"; return true; }
            return false;
        }

        static Scope Make(Transform root, RiggleBodyPart.Kind kind, RiggleBodyPart.Side side,
            bool terminal, bool neck, bool separate)
        {
            var names = new List<string>(); string prefix = side == RiggleBodyPart.Side.Left ? "Left" : side == RiggleBodyPart.Side.Middle ? "Middle" : "Right";
            bool limb = kind == RiggleBodyPart.Kind.Arm || kind == RiggleBodyPart.Kind.Leg;
            switch (kind)
            {
                case RiggleBodyPart.Kind.Arm:
                    names.Add(prefix + "UpperArm"); names.Add(prefix + "LowerArm");
                    if (terminal) names.Add(prefix + "Hand"); break;
                case RiggleBodyPart.Kind.Leg:
                    names.Add(prefix + "UpperLeg"); names.Add(prefix + "LowerLeg");
                    if (terminal) names.Add(prefix + "Foot"); break;
                case RiggleBodyPart.Kind.Torso:
                    names.AddRange(new[] { "Hips", "Spine", "Chest", "UpperChest" });
                    if (neck) names.Add("Neck"); break;
                case RiggleBodyPart.Kind.Head:
                    names.Add("Head"); if (neck) names.Add("Neck"); break;
                case RiggleBodyPart.Kind.Ear:
                case RiggleBodyPart.Kind.Tail:
                case RiggleBodyPart.Kind.Wing:
                case RiggleBodyPart.Kind.Ponytail:
                    string chain = kind == RiggleBodyPart.Kind.Tail ? "Tail" : prefix + kind;
                    var rig = root.GetComponentInParent<Riggle>();
                    if (!rig) throw new InvalidOperationException("Appendage parts must be children of a Riggle character.");
                    return new Scope { root = root, label = root.name, limb = true, separate = separate,
                        bones = RiggleAppendageBuilder.ScopeBones(rig, chain) };
                default: throw new InvalidOperationException("Unknown body part: " + kind);
            }
            return new Scope { root = root, label = root.name, limb = limb, separate = separate,
                bones = new HashSet<string>(names.Select(n => "Riggle_" + n)) };
        }

        internal static bool Allows(Scope scope, RiggleBuilder.BoneSpec bone) =>
            scope.bones.Contains(bone.twistSource ?? bone.name);
    }
}
