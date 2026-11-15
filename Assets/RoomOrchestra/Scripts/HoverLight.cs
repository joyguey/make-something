using UnityEngine;

namespace RoomOrchestra
{
    /// <summary>
    /// The thing under the pointer lights up (George, headset 2026-11-03: "Highlight buttons and other objects when I
    /// target them so I know what I can pinch"): the button, tile, character or ball a controller's ray or the player's
    /// gaze rests on is drawn a little lighter until the pointer leaves it. Its own colour, only lighter, through a
    /// property block: no material is copied or changed, and nothing moves. The pointer's gold ray, its dot and the hover
    /// tick still say the same thing, so the cue never rests on colour alone.
    /// </summary>
    public static class HoverLight
    {
        /// <summary>How far toward white the hovered thing is drawn (0..1): clearly lighter, its white label still readable.</summary>
        public const float Lighten = 0.18f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");
        static readonly Renderer[] s_Lit = new Renderer[2]; // per hand
        static MaterialPropertyBlock s_Block;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Lit[0] = s_Lit[1] = null;

        /// <summary>What <paramref name="hand"/>'s pointer lights now (tests).</summary>
        public static Renderer Lit(int hand) => s_Lit[hand];

        /// <summary>The visible part to light for a tap area: its own renderer, else the first under it (never a label).</summary>
        public static Renderer RendererOf(Collider tapArea)
        {
            if (tapArea == null) return null;
            if (tapArea.TryGetComponent<Renderer>(out var own) && !own.TryGetComponent<TMPro.TMP_Text>(out _)) return own;
            foreach (var r in tapArea.GetComponentsInChildren<Renderer>())
                if (!r.TryGetComponent<TMPro.TMP_Text>(out _)) return r;
            return null;
        }

        /// <summary><paramref name="hand"/>'s pointer rests on <paramref name="target"/> (null: on nothing pressable).</summary>
        public static void Set(int hand, Renderer target)
        {
            var old = s_Lit[hand];
            if (old == target) return;
            s_Lit[hand] = target;
            if (old != null && old != s_Lit[1 - hand]) old.SetPropertyBlock(null); // the other hand may still be on it
            if (target == null || target.sharedMaterial == null) return;
            var material = target.sharedMaterial;
            var colour = material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId)
                : material.HasProperty(ColorId) ? material.GetColor(ColorId) : Color.white;
            var lighter = Color.Lerp(colour, Color.white, Lighten);
            lighter.a = colour.a;
            s_Block ??= new MaterialPropertyBlock();
            s_Block.Clear();
            s_Block.SetColor(BaseColorId, lighter);
            s_Block.SetColor(ColorId, lighter);
            target.SetPropertyBlock(s_Block);
        }
    }
}
