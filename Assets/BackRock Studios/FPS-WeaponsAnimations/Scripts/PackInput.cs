// FPS Weapons Pack: reads the demo's controls from whichever input system the
// project has enabled (Edit > Project Settings > Player > Active Input
// Handling). New Unity 6 projects enable only the Input System package, where
// UnityEngine.Input throws; projects that still use the Input Manager (or
// both) read it as before. The names match the Input Manager defaults the
// scripts were written against: Horizontal, Vertical, Mouse X, Mouse Y,
// Mouse ScrollWheel, Fire1, Fire2 and Jump.
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace BackRock
{
    public static class PackInput
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        // Input Manager defaults: mouse axes are pixels x 0.1, the scroll
        // wheel reads about 0.1 a notch, keyboard axes are -1..1.
        const float MouseSensitivity = 0.1f;

        public static float GetAxis(string axis)
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            switch (axis)
            {
                case "Horizontal":
                    if (keyboard == null) return 0f;
                    return (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                         - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
                case "Vertical":
                    if (keyboard == null) return 0f;
                    return (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                         - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
                case "Mouse X":
                    return mouse == null ? 0f : mouse.delta.ReadValue().x * MouseSensitivity;
                case "Mouse Y":
                    return mouse == null ? 0f : mouse.delta.ReadValue().y * MouseSensitivity;
                case "Mouse ScrollWheel":
                    if (mouse == null) return 0f;
                    float y = mouse.scroll.ReadValue().y;
                    return y > 0f ? 0.1f : y < 0f ? -0.1f : 0f;
            }
            return 0f;
        }

        static UnityEngine.InputSystem.Controls.ButtonControl Button(string button)
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;
            switch (button)
            {
                case "Fire1": return mouse != null ? mouse.leftButton : null;
                case "Fire2": return mouse != null ? mouse.rightButton : null;
                case "Fire3": return mouse != null ? mouse.middleButton : null;
                case "Jump": return keyboard != null ? keyboard.spaceKey : null;
            }
            return null;
        }

        public static bool GetButton(string button) { var b = Button(button); return b != null && b.isPressed; }
        public static bool GetButtonDown(string button) { var b = Button(button); return b != null && b.wasPressedThisFrame; }
        public static bool GetButtonUp(string button) { var b = Button(button); return b != null && b.wasReleasedThisFrame; }

        static UnityEngine.InputSystem.Controls.KeyControl KeyFor(KeyCode code)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return null;
            // Key orders the digits 1..9 then 0.
            if (code == KeyCode.Alpha0)
                return keyboard.digit0Key;
            if (code >= KeyCode.Alpha1 && code <= KeyCode.Alpha9)
                return keyboard[Key.Digit1 + (code - KeyCode.Alpha1)];
            if (code >= KeyCode.A && code <= KeyCode.Z)
                return keyboard[Key.A + (code - KeyCode.A)];
            switch (code)
            {
                case KeyCode.Tab: return keyboard.tabKey;
                case KeyCode.Space: return keyboard.spaceKey;
                case KeyCode.Escape: return keyboard.escapeKey;
                case KeyCode.Return: return keyboard.enterKey;
                case KeyCode.LeftShift: return keyboard.leftShiftKey;
                case KeyCode.LeftControl: return keyboard.leftCtrlKey;
            }
            return null;
        }

        public static bool GetKey(KeyCode code) { var k = KeyFor(code); return k != null && k.isPressed; }
        public static bool GetKeyDown(KeyCode code) { var k = KeyFor(code); return k != null && k.wasPressedThisFrame; }
        public static bool GetKeyUp(KeyCode code) { var k = KeyFor(code); return k != null && k.wasReleasedThisFrame; }

        // The demo scenes' EventSystem uses StandaloneInputModule, which also
        // reads UnityEngine.Input. In the pack's own scenes, swap it for the
        // Input System's UI module; other scenes are left alone.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void WatchPackScenes()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (!scene.path.Contains("/FPS-WeaponsAnimations/")) return;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var legacy in root.GetComponentsInChildren<UnityEngine.EventSystems.StandaloneInputModule>(true))
                    {
                        var go = legacy.gameObject;
                        Object.DestroyImmediate(legacy);
                        if (go.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
                            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                    }
                }
            };
        }
#else
        public static float GetAxis(string axis) { return Input.GetAxis(axis); }
        public static bool GetButton(string button) { return Input.GetButton(button); }
        public static bool GetButtonDown(string button) { return Input.GetButtonDown(button); }
        public static bool GetButtonUp(string button) { return Input.GetButtonUp(button); }
        public static bool GetKey(KeyCode code) { return Input.GetKey(code); }
        public static bool GetKeyDown(KeyCode code) { return Input.GetKeyDown(code); }
        public static bool GetKeyUp(KeyCode code) { return Input.GetKeyUp(code); }
#endif
    }
}
