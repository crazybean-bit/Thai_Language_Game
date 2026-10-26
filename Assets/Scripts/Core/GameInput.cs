using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ThaiMystery
{
    // Keyboard input that works with either the new Input System or the old Input Manager,
    // whichever the project has enabled (Project Settings > Player > Active Input Handling).
    public static class GameInput
    {
        public static Vector2Int MoveDirection()
        {
            if (Held(Btn.Up)) return Vector2Int.up;
            if (Held(Btn.Down)) return Vector2Int.down;
            if (Held(Btn.Left)) return Vector2Int.left;
            if (Held(Btn.Right)) return Vector2Int.right;
            return Vector2Int.zero;
        }

        public static bool Confirm() => Pressed(Btn.Confirm);
        public static bool Cancel() => Pressed(Btn.Cancel);
        public static bool Escape() => Pressed(Btn.Escape);
        public static bool Journal() => Pressed(Btn.Journal);
        public static bool UpPressed() => Pressed(Btn.Up);
        public static bool DownPressed() => Pressed(Btn.Down);

        private enum Btn { Up, Down, Left, Right, Confirm, Cancel, Escape, Journal }

#if ENABLE_INPUT_SYSTEM
        private static bool Held(Btn k)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (k)
            {
                case Btn.Up: return kb.upArrowKey.isPressed || kb.wKey.isPressed;
                case Btn.Down: return kb.downArrowKey.isPressed || kb.sKey.isPressed;
                case Btn.Left: return kb.leftArrowKey.isPressed || kb.aKey.isPressed;
                case Btn.Right: return kb.rightArrowKey.isPressed || kb.dKey.isPressed;
            }
            return false;
        }

        private static bool Pressed(Btn k)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (k)
            {
                case Btn.Up: return kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
                case Btn.Down: return kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
                case Btn.Confirm: return kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame;
                case Btn.Cancel: return kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame;
                case Btn.Escape: return kb.escapeKey.wasPressedThisFrame;
                case Btn.Journal: return kb.jKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame;
            }
            return false;
        }
#else
        private static bool Held(Btn k)
        {
            switch (k)
            {
                case Btn.Up: return Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
                case Btn.Down: return Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
                case Btn.Left: return Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
                case Btn.Right: return Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
            }
            return false;
        }

        private static bool Pressed(Btn k)
        {
            switch (k)
            {
                case Btn.Up: return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
                case Btn.Down: return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
                case Btn.Confirm: return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Z);
                case Btn.Cancel: return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.X);
                case Btn.Escape: return Input.GetKeyDown(KeyCode.Escape);
                case Btn.Journal: return Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Tab);
            }
            return false;
        }
#endif
    }
}
