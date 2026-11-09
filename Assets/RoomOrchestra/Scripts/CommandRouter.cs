namespace RoomOrchestra
{
    /// <summary>
    /// Sends one discrete command to the right place: echo editing when edit mode is on, otherwise
    /// select-and-place. Shared by controller buttons and the editor keyboard simulator.
    /// </summary>
    public static class CommandRouter
    {
        public static void Dispatch(ControllerCommand command, OrbSelector selector)
        {
            if (command == ControllerCommand.Recenter) // works in any mode
            {
                if (StageAnchor.Instance != null) StageAnchor.Instance.RecenterToHead();
                return;
            }
            // The title card: the select button starts the game; nothing else acts until then.
            var title = TitleScreen.Instance;
            if (title != null && title.Showing)
            {
                if (command == ControllerCommand.Place) title.Begin();
                return;
            }
            var conductor = EchoConductor.Instance;
            if (command == ControllerCommand.ToggleEchoEdit)
            {
                conductor?.ToggleEditMode(selector != null ? selector.FocusedTarget : null);
                return;
            }
            if (conductor != null && conductor.EditMode)
            {
                conductor.ExecuteEdit(command);
                return;
            }
            selector?.Execute(command);
        }
    }
}
