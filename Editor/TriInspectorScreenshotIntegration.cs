using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class TriSamplesScreenshotIntegration
{
    static TriSamplesScreenshotIntegration()
    {
        EditorApplication.update += DetectSamplesWindows;
    }

    private static readonly Dictionary<EditorWindow, (VisualElement host, Button button)> overlays = new();
    private static FieldInfo currentField;

    private const float ButtonWidth = 170f;
    private const float ButtonHeight = 24f;
    private const float MarginRight = 15f;
    private const float MarginTop = 6f;

    private static void DetectSamplesWindows()
    {
        foreach (var win in Resources.FindObjectsOfTypeAll<EditorWindow>())
        {
            if (win == null) continue;
            if (win.GetType().FullName != "TriInspector.Editor.Samples.TriSamplesWindow") continue;

            if (!overlays.ContainsKey(win))
            {
                InjectOverlay(win);
            }
        }

        var dead = new List<EditorWindow>();
        foreach (var kv in overlays)
        {
            if (kv.Key == null) dead.Add(kv.Key);
        }
        foreach (var w in dead) overlays.Remove(w);
    }

    private static void InjectOverlay(EditorWindow win)
    {
        if (win.rootVisualElement == null)
        {
            Debug.Log("⚠️ rootVisualElement unavailable — UI Toolkit might not be initialized yet.");
            return;
        }

        if (currentField == null)
            currentField = win.GetType().GetField("_current", BindingFlags.Instance | BindingFlags.NonPublic);

        var host = new VisualElement
        {
            name = "TriSamplesScreenshotOverlay",
            pickingMode = PickingMode.Position
        };
        host.style.position = Position.Absolute;
        host.style.width = ButtonWidth;
        host.style.height = ButtonHeight;

        var btn = new Button(() =>
        {
            var current = currentField?.GetValue(win) as ScriptableObject;
            if (current == null)
            {
                Debug.Log("ℹ️ No sample currently selected in TriSamplesWindow.");
                return;
            }

            Selection.activeObject = current;
            InspectorScreenshotUtility.OpenDebugWindow(autoCapture: true);

        })
        {
            text = "📸 Take Screenshot",
            visible = false
        };

        btn.style.width = ButtonWidth;
        btn.style.height = ButtonHeight;

        host.Add(btn);
        win.rootVisualElement.Add(host);

        void UpdateLayout()
        {
            var current = currentField?.GetValue(win) as ScriptableObject;
            bool hasSample = current != null;

            btn.visible = hasSample;
            host.style.display = hasSample ? DisplayStyle.Flex : DisplayStyle.None;

            if (!hasSample)
                return;

            var r = win.rootVisualElement.contentRect;
            if (r.width <= 0 || r.height <= 0) return;

            float x = Mathf.Max(0, r.width - ButtonWidth - MarginRight);
            float y = MarginTop;

            host.style.left = x;
            host.style.top = y;
        }

        win.rootVisualElement.RegisterCallback<GeometryChangedEvent>(_ => UpdateLayout());
        win.rootVisualElement.schedule.Execute(UpdateLayout).Every(300);

        overlays[win] = (host, btn);
    }
}
