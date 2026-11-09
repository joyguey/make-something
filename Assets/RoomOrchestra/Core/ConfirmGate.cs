namespace RoomOrchestra
{
    /// <summary>
    /// Two plain taps for anything that ends or wipes (George, 2026-11-03: "for Quit and other actions that end or
    /// clear something, ask for confirmation"). The first tap only asks; the same button again does it; any other
    /// press cancels. No time limit, so nobody has to be quick (accessibility: no fast reflexes).
    /// </summary>
    public sealed class ConfirmGate
    {
        const int None = 0;

        /// <summary>The button waiting for its second tap (an id), or 0.</summary>
        public int Asking { get; private set; } = None;

        /// <summary>A tap on button <paramref name="id"/> (non-zero): true means go ahead; false means it now asks.</summary>
        public bool Request(int id)
        {
            if (id != None && Asking == id)
            {
                Asking = None;
                return true;
            }
            Asking = id;
            return false;
        }

        /// <summary>Anything else was pressed: stop asking.</summary>
        public void Cancel() => Asking = None;
    }
}
