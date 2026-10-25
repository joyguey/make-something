using System.Collections.Generic;

namespace RoomOrchestra
{
    /// <summary>One controller's buttons for one frame (either hand; trigger is left to Spatial Input).</summary>
    public struct ControllerFrame
    {
        public bool Connected;
        public float StickX;
        public float StickY;
        public bool Primary;   // A (right) / X (left)
        public bool Secondary; // B (right) / Y (left)
        public bool StickClick;
        public bool Grip;      // grip squeezed past half way: toggles echo edit mode (one press, no hold)
        public bool Menu;      // menu button (left controller on PICO): recenters the stage
    }

    public enum ControllerCommand
    {
        NextOrb,
        PreviousOrb,
        NextTarget,
        PreviousTarget,
        Place,
        CycleIntensity,
        CyclePetal,
        ToggleEchoEdit,
        Recenter
    }

    /// <summary>
    /// Turns raw controller frames into discrete commands. Each push or press yields exactly one
    /// command; holding never repeats, so no sustained hold or rapid input is ever needed.
    /// Either controller alone can drive the whole place flow.
    /// </summary>
    public sealed class ControllerCommandMapper
    {
        public const float PressThreshold = 0.6f;
        public const float ReleaseThreshold = 0.3f; // hysteresis so a wobbly stick can't double-fire

        struct HandState
        {
            public bool StickArmedX, StickArmedY, Primary, Secondary, StickClick, Grip, Menu, Initialized;
        }

        readonly HandState[] m_Hands = new HandState[2];
        readonly List<ControllerCommand> m_Buffer = new();

        /// <summary>Processes one frame for each hand (0 = left, 1 = right) and returns new commands.</summary>
        public IReadOnlyList<ControllerCommand> Update(ControllerFrame left, ControllerFrame right)
        {
            m_Buffer.Clear();
            Process(0, left);
            Process(1, right);
            return m_Buffer;
        }

        void Process(int hand, ControllerFrame f)
        {
            ref var s = ref m_Hands[hand];
            if (!f.Connected)
            {
                s = default; // reconnecting starts fresh, no phantom command
                return;
            }
            if (!s.Initialized)
            {
                // Buttons already held when a controller appears must be released before they count.
                s.Initialized = true;
                s.Primary = f.Primary;
                s.Secondary = f.Secondary;
                s.StickClick = f.StickClick;
                s.Grip = f.Grip;
                s.Menu = f.Menu;
                s.StickArmedX = Abs(f.StickX) < ReleaseThreshold;
                s.StickArmedY = Abs(f.StickY) < ReleaseThreshold;
                return;
            }

            // Use the dominant axis so a diagonal push is one command, not two.
            bool horizontal = Abs(f.StickX) >= Abs(f.StickY);
            if (s.StickArmedX && horizontal && Abs(f.StickX) >= PressThreshold)
            {
                m_Buffer.Add(f.StickX > 0 ? ControllerCommand.NextOrb : ControllerCommand.PreviousOrb);
                s.StickArmedX = false;
            }
            else if (!s.StickArmedX && Abs(f.StickX) < ReleaseThreshold)
            {
                s.StickArmedX = true;
            }

            if (s.StickArmedY && !horizontal && Abs(f.StickY) >= PressThreshold)
            {
                m_Buffer.Add(f.StickY > 0 ? ControllerCommand.NextTarget : ControllerCommand.PreviousTarget);
                s.StickArmedY = false;
            }
            else if (!s.StickArmedY && Abs(f.StickY) < ReleaseThreshold)
            {
                s.StickArmedY = true;
            }

            if (f.Primary && !s.Primary) m_Buffer.Add(ControllerCommand.Place);
            if (f.Secondary && !s.Secondary) m_Buffer.Add(ControllerCommand.CycleIntensity);
            if (f.StickClick && !s.StickClick) m_Buffer.Add(ControllerCommand.CyclePetal);
            if (f.Grip && !s.Grip) m_Buffer.Add(ControllerCommand.ToggleEchoEdit);
            if (f.Menu && !s.Menu) m_Buffer.Add(ControllerCommand.Recenter);
            s.Primary = f.Primary;
            s.Secondary = f.Secondary;
            s.StickClick = f.StickClick;
            s.Grip = f.Grip;
            s.Menu = f.Menu;
        }

        public void Reset()
        {
            m_Hands[0] = default;
            m_Hands[1] = default;
        }

        static float Abs(float v) => v < 0f ? -v : v;
    }
}
