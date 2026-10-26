using UnityEngine;

namespace ThaiMystery
{
    // Shared references to the running game's systems, filled in by GameBootstrap.
    public static class Game
    {
        public static WordBook Words;
        public static GameFlags Flags;
        public static GridMap Map;
        public static DialogueRunner Dialogue;
        public static GameUI UI;
        public static PlayerController Player;
        public static VoicePlayer Voice;

        private static int lockedFrame = -1;

        // Stops one key press from being handled twice, e.g. the Space that closes
        // a dialogue also re-triggering the object the player is facing.
        public static void ConsumeInputThisFrame() => lockedFrame = Time.frameCount;
        public static bool InputConsumed => Time.frameCount <= lockedFrame;

        public static bool PlayerCanAct =>
            !InputConsumed && (Dialogue == null || !Dialogue.IsOpen) && (UI == null || !UI.ModalOpen) &&
            (UI == null || !UI.IsFading);
    }
}
