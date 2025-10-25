using UnityEditor;
using UnityEngine;
using System.IO;

public static class InspectorScreenshotUtility
{
    private const int FixedWidth = 1000;
    private static readonly Color BackgroundColor = new Color(0.219f, 0.219f, 0.219f, 1f);

    [MenuItem("Tools/Inspector Screenshot/Open Auto-Fit Window")]
    public static void OpenDebugWindow() => OpenDebugWindow(false);
    public static void OpenDebugWindow(bool autoCapture = false)
    {
        var selected = Selection.activeObject;
        if (autoCapture)
            Selection.activeObject = null;
        if (selected == null)
        {
            Debug.LogWarning("⚠️ No object selected.");
            return;
        }

        ScriptableObject instance = GetScriptableInstance(selected);
        if (instance == null)
        {
            Debug.LogError("❌ Please select a ScriptableObject or a script inheriting from ScriptableObject.");
            return;
        }

        CreateWindow(instance, autoCapture);
    }
    private static void CreateWindow(Object target, bool autoCapture)
    {
        Object clone = Object.Instantiate(target);
        clone.name = target.name + "_Clone";
        clone.hideFlags = HideFlags.DontSave;

        var editor = Editor.CreateEditor(clone);
        if (editor == null)
        {
            Debug.LogError("❌ Failed to create Editor instance for the cloned object.");
            return;
        }

        var window = ScriptableObject.CreateInstance<TempInspectorWindow>();
        window.editor = editor;
        window.targetInstance = clone;
        window.autoCapture = autoCapture;
        window.titleContent = new GUIContent("Inspector Screenshot");
        window.minSize = new Vector2(FixedWidth / 2f, 100);
        window.maxSize = new Vector2(FixedWidth / 2f, 5000);
        window.position = new Rect(200, 200, FixedWidth / 2f, 400);

        window.Show();
        window.Focus();
    }
    private static ScriptableObject GetScriptableInstance(Object selected)
    {
        if (selected is ScriptableObject so)
            return so;

        if (selected is MonoScript script)
        {
            var type = script.GetClass();
            if (type == null || !typeof(ScriptableObject).IsAssignableFrom(type))
                return null;

            var temp = ScriptableObject.CreateInstance(type);
            temp.name = $"{type.Name}_TempInstance";
            return temp;
        }

        return null;
    }
    private class TempInspectorWindow : EditorWindow
    {
        public Editor editor;
        public Object targetInstance;

        private float measuredHeight;
        private bool requestCapture;
        private Texture2D lastScreenshot;

        private float toolbarHeight = 22f;

        private const float heightPaddingCorrection = (22f * 1.8f) + 1f;
        public bool autoCapture;

        private void OnGUI()
        {
            if (editor == null)
            {
                EditorGUILayout.HelpBox("No editor assigned.", MessageType.Info);
                return;
            }

            Rect toolbarRect = EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.FlexibleSpace();
            if (!autoCapture && GUILayout.Button("📸 Take Screenshot", EditorStyles.toolbarButton))
                RequestScreenshot();
            EditorGUILayout.EndHorizontal();
            toolbarHeight = toolbarRect.height > 0 ? toolbarRect.height : 22f;

            editor.OnInspectorGUI();

            if (Event.current.type == EventType.Repaint)
            {
                Rect lastRect = GUILayoutUtility.GetLastRect();
                measuredHeight = lastRect.yMax + 20f - heightPaddingCorrection;
                AdjustHeightToFit();
            }

            if (requestCapture && Event.current.type == EventType.Repaint)
            {
                requestCapture = false;
                CaptureInspectorOnly();
            }
            
            if (autoCapture && Event.current.type == EventType.Repaint)
            {
                autoCapture = false;

                int frameDelay = 2;
                EditorApplication.update += WaitAndCapture;

                void WaitAndCapture()
                {
                    frameDelay--;
                    if (frameDelay <= 0)
                    {
                        EditorApplication.update -= WaitAndCapture;
                        if (this != null)
                        {
                            RequestScreenshot();
                            EditorApplication.delayCall += () =>
                            {
                                if (this != null)
                                {
                                    Close();
                                }
                            };
                        }
                    }
                }
            }

        }

        private void AdjustHeightToFit()
        {
            float newHeight = Mathf.Clamp(measuredHeight + toolbarHeight, 150f, 3000f);
            var rect = position;
            if (Mathf.Abs(rect.height - newHeight) > 1f)
            {
                rect.height = newHeight;
                position = rect;
                Repaint();
            }
        }

        private void RequestScreenshot()
        {
            requestCapture = true;
            Repaint();
            EditorApplication.delayCall += () => { if (this != null) Repaint(); };
        }

        private void CaptureInspectorOnly()
        {
            int winW = Mathf.RoundToInt(position.width);
            int winH = Mathf.RoundToInt(position.height);
            int contentY = Mathf.RoundToInt(toolbarHeight);
            int scriptY = 22;

            int captureH = winH - contentY - scriptY;
            var contentTex = new Texture2D(winW, captureH, TextureFormat.RGBA32, false);
            contentTex.ReadPixels(new Rect(0, 0, winW, captureH), 0, 0);
            contentTex.Apply();

            int finalW = FixedWidth;
            int paddingTop = 40;
            int paddingBottom = 40;
            int finalH = captureH + paddingTop + paddingBottom;

            var finalTex = new Texture2D(finalW, finalH, TextureFormat.RGBA32, false);

            Color[] bg = new Color[finalW * finalH];
            for (int i = 0; i < bg.Length; i++)
                bg[i] = BackgroundColor;
            finalTex.SetPixels(bg);

            float scale = (float)captureH / captureH;
            int scaledW = winW;
            int offsetX = (finalW - scaledW) / 2;
            int offsetY = paddingBottom;

            for (int y = 0; y < captureH; y++)
            {
                for (int x = 0; x < winW; x++)
                {
                    Color c = contentTex.GetPixel(x, y);
                    int targetX = Mathf.Clamp(offsetX + x, 0, finalW - 1);
                    int targetY = Mathf.Clamp(offsetY + y, 0, finalH - 1);
                    finalTex.SetPixel(targetX, targetY, c);
                }
            }

            finalTex.Apply();
            ApplyRoundedCorners(finalTex, 6);

            string defaultName = $"{targetInstance.name}_Inspector.png";
            string path = EditorUtility.SaveFilePanel(
                "Salvar Screenshot do Inspector",
                Application.dataPath,
                defaultName,
                "png"
            );

            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, finalTex.EncodeToPNG());
                Debug.Log($"📸 Screenshot saved at: {path}");
                AssetDatabase.Refresh();
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                
            }


            if (lastScreenshot != null)
                DestroyImmediate(lastScreenshot);
            lastScreenshot = finalTex;

            DestroyImmediate(contentTex);
            Repaint();
        }

        private void ApplyRoundedCorners(Texture2D tex, int radius, float feather = 1f)
        {
            int w = tex.width;
            int h = tex.height;

            float cx = radius - 0.5f;
            float cy = radius - 0.5f;

            radius = Mathf.Max(1, Mathf.Min(radius, Mathf.Min(w, h) / 2));
            feather = Mathf.Max(0.001f, feather);

            void SetCornerAlphas(int x, int y, float a)
            {
                var c = tex.GetPixel(x, y);
                c.a = Mathf.Min(c.a, a);
                tex.SetPixel(x, y, c);

                c = tex.GetPixel(w - 1 - x, y);
                c.a = Mathf.Min(c.a, a);
                tex.SetPixel(w - 1 - x, y, c);

                c = tex.GetPixel(x, h - 1 - y);
                c.a = Mathf.Min(c.a, a);
                tex.SetPixel(x, h - 1 - y, c);

                c = tex.GetPixel(w - 1 - x, h - 1 - y);
                c.a = Mathf.Min(c.a, a);
                tex.SetPixel(w - 1 - x, h - 1 - y, c);
            }

            for (int y = 0; y < radius; y++)
            {
                for (int x = 0; x < radius; x++)
                {
                    float dx = cx - x;
                    float dy = cy - y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist >= radius)
                    {
                        SetCornerAlphas(x, y, 0f);
                    }
                    else if (dist >= radius - feather)
                    {
                        float t = (radius - dist) / feather;
                        float a = Mathf.Clamp01(t);
                        SetCornerAlphas(x, y, a);
                    }
                }
            }

            tex.Apply();
        }



        private void OnDestroy()
        {
            if (editor != null)
                DestroyImmediate(editor);

            if (targetInstance != null)
            {
                if (targetInstance.hideFlags.HasFlag(HideFlags.HideAndDontSave))
                    DestroyImmediate(targetInstance);
            }
        }
    }
}
