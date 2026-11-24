using System;
using System.IO;
using System.Linq;
using GravityRoguelike.Run;
using GravityRoguelike.UI;
using GravityRoguelike.World;
using GravityRoguelike.World.Visuals;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

/// <summary>Authors only the standalone selection assets/scene. No changes to gameplay prefabs or the source scene.</summary>
public static class CharacterSelectDemoBuilder
{
    private const string ScenePath = "Assets/Scenes/CharacterSelectDemo.unity";
    private const string UIPath = "Assets/UI/CharacterSelect";
    private const string DataPath = "Assets/Data/Classes";
    private const string PrefabPath = "Assets/Prefabs/UI/CharacterSelectUI.prefab";

    [MenuItem("Tools/Character Selection/Create or Open Demo")]
    public static void CreateDemo()
    {
        if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
        Directory.CreateDirectory(UIPath);
        Directory.CreateDirectory(DataPath);
        Directory.CreateDirectory("Assets/Prefabs/UI");
        AssetDatabase.Refresh();
        Sprite tablet = MakeTexture("StoneTablet", 256, (x, y) =>
        {
            float grain = Mathf.PerlinNoise(x * 0.28f, y * 0.28f) * 0.04f;
            float edge = Mathf.Min(Mathf.Min(x, 255 - x), Mathf.Min(y, 255 - y));
            float wear = Mathf.Clamp01(edge / 28f);
            float crack = Mathf.Abs(x - 24 - y * 0.23f - Mathf.Sin(y * 0.11f) * 4) < 0.7f ? 0.035f : 0;
            return new Color((0.13f + grain) * (0.6f + wear * 0.4f) - crack,
                (0.115f + grain) * (0.6f + wear * 0.4f) - crack, (0.14f + grain) * (0.6f + wear * 0.4f) - crack, 1);
        }, 32);
        Sprite frame = MakeTexture("BronzeFrame", 256, (x, y) =>
        {
            float edge = Mathf.Min(Mathf.Min(x, 255 - x), Mathf.Min(y, 255 - y));
            float alpha = Mathf.Exp(-Mathf.Abs(edge - 14) * 0.32f) * 0.45f;
            if (Mathf.Abs(edge - 14) < 1.5f || Mathf.Abs(edge - 24) < 0.8f) alpha = 0.92f;
            int u = Mathf.Min(x, 255 - x), v = Mathf.Min(y, 255 - y);
            if (u < 46 && v < 46 && (Mathf.Abs(u - v) < 1 || Mathf.Abs(u + v - 48) < 1)) alpha = 1;
            float metal = 0.82f + Mathf.PerlinNoise(x * 0.16f, y * 0.16f) * 0.18f;
            return new Color(metal, metal, metal, alpha);
        }, 48);
        Sprite rune = MakeTexture("RuneGem", 48, (x, y) =>
        {
            float u = Mathf.Abs(x - 23.5f) / 17f, v = Mathf.Abs(y - 23.5f) / 22f;
            bool outline = Mathf.Abs(u + v - 1) < 0.11f;
            bool glyph = (Mathf.Abs(x - 23.5f) < 1.1f && v < 0.6f) || (Mathf.Abs(Mathf.Abs(x - 23.5f) - Mathf.Abs(y - 24) * 0.65f) < 1 && v < 0.35f);
            return new Color(1, 1, 1, outline || glyph ? 1 : u + v < 0.9f ? 0.16f : 0);
        });
        Sprite vignette = MakeTexture("Vignette", 256, (x, y) =>
        {
            float r = new Vector2((x - 127.5f) / 128f, (y - 127.5f) / 128f).magnitude;
            return new Color(1, 1, 1, Mathf.SmoothStep(0, 1, Mathf.Clamp01((r - 0.4f) / 0.95f)));
        });
        Sprite ember = MakeTexture("Ember", 32, (x, y) =>
        {
            float r = new Vector2(x - 15.5f, y - 15.5f).magnitude / 16;
            return new Color(1, 1, 1, Mathf.Exp(-r * r * 10) * Mathf.Clamp01((1 - r) * 3));
        });
        string[] names = { "Warrior", "Mage", "Archer" };
        string[] descriptions = {
            "Close combat, precise timing.\nLMB: alternating slash.\nRMB: thrusting stab.",
            "LMB: hold to fire aimed orbs.\nRMB: channel a staff sweep.\nStaff faces forward, not the cursor.",
            "Aim the bow with your cursor.\nHold LMB to draw and fire.\nTime your shots around the cooldown." };
        Color[] colors = { new Color(1, 0.49f, 0.28f), new Color(0.35f, 0.91f, 1), new Color(0.48f, 0.86f, 0.52f) };
        string[] animationFolders = { "VoidKnight", "VoidMage", "VoidArcher" };
        var definitions = new ClassDefinition[3];
        for (int i = 0; i < names.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{names[i]}.prefab");
            if (prefab == null || prefab.GetComponent<PlayerController2D>() == null || !prefab.CompareTag("Player"))
                throw new InvalidOperationException("Missing compatible player prefab: " + names[i]);
            definitions[i] = ScriptableObject.CreateInstance<ClassDefinition>();
            var data = new SerializedObject(definitions[i]);
            data.FindProperty("displayName").stringValue = names[i];
            data.FindProperty("description").stringValue = descriptions[i];
            data.FindProperty("difficulty").intValue = 1 + i * 2;
            data.FindProperty("playerPrefab").objectReferenceValue = prefab;
            data.FindProperty("accentColor").colorValue = colors[i];
            data.FindProperty("portrait").objectReferenceValue = MakePortrait(names[i], animationFolders[i]);
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definitions[i], $"{DataPath}/{names[i]}.asset");
        }

        var root = new GameObject("CharacterSelectUI", typeof(RectTransform));
        CharacterSelectUI ui = root.AddComponent<CharacterSelectUI>();
        var settings = new SerializedObject(ui);
        var list = settings.FindProperty("classes"); list.arraySize = definitions.Length;
        for (int i = 0; i < definitions.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
        settings.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        settings.FindProperty("tablet").objectReferenceValue = tablet;
        settings.FindProperty("frame").objectReferenceValue = frame;
        settings.FindProperty("rune").objectReferenceValue = rune;
        settings.FindProperty("vignette").objectReferenceValue = vignette;
        settings.FindProperty("ember").objectReferenceValue = ember;
        settings.ApplyModifiedPropertiesWithoutUndo();
        GameObject uiPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        if (!AssetDatabase.CopyAsset("Assets/Scenes/ProceduralGameplayDemo.unity", ScenePath))
            throw new IOException("Could not copy the development scene.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (var oldPlayer in Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(oldPlayer.gameObject);
        ProceduralGameplayDemo generator = Object.FindAnyObjectByType<ProceduralGameplayDemo>();
        var gen = new SerializedObject(generator);
        gen.FindProperty("player").objectReferenceValue = null;
        gen.ApplyModifiedPropertiesWithoutUndo();
        CameraFollow2D follow = Object.FindAnyObjectByType<CameraFollow2D>();
        follow.SetTarget(null);
        BlackHoleBackdrop backdrop = Object.FindAnyObjectByType<BlackHoleBackdrop>();
        CharacterSelectUI instance = ((GameObject)PrefabUtility.InstantiatePrefab(uiPrefab)).GetComponent<CharacterSelectUI>();
        RunFlowController flow = new GameObject("Run Flow").AddComponent<RunFlowController>();
        var refs = new SerializedObject(flow);
        refs.FindProperty("generator").objectReferenceValue = generator;
        refs.FindProperty("clearancePrefab").objectReferenceValue = definitions[0].PlayerPrefab;
        refs.FindProperty("selectionUI").objectReferenceValue = instance;
        refs.FindProperty("cameraFollow").objectReferenceValue = follow;
        refs.FindProperty("backdrop").objectReferenceValue = backdrop;
        refs.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("CharacterSelectDemo created with the existing generator/settings and no scene player.");
    }

    private static Sprite MakeTexture(string name, int size, Func<int, int, Color> pixel, int border = 0)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) pixels[y * size + x] = pixel(x, y);
        texture.SetPixels(pixels); texture.Apply();
        string path = $"{UIPath}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        return ImportSprite(path, border, FilterMode.Bilinear);
    }

    private static Sprite MakePortrait(string name, string animationFolder)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/{animationFolder}/Idle.anim");
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).First(b => b.propertyName == "m_Sprite");
        Sprite sprite = (Sprite)AnimationUtility.GetObjectReferenceCurve(clip, binding)[0].value;
        // Read a copy of the source PNG: original sprite import settings stay untouched.
        var source = new Texture2D(2, 2);
        source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));
        Rect r = sprite.rect;
        Color[] pixels = source.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
        int w = (int)r.width, h = (int)r.height, x0 = w, y0 = h, x1 = 0, y1 = 0;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (pixels[y * w + x].a > 0.05f)
        { x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y); }
        var crop = new Texture2D(x1 - x0 + 3, y1 - y0 + 3, TextureFormat.RGBA32, false);
        var cropped = new Color[crop.width * crop.height];
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            cropped[(y - y0 + 1) * crop.width + x - x0 + 1] = pixels[y * w + x];
        crop.SetPixels(cropped); crop.Apply();
        string path = $"{UIPath}/{name}Portrait.png";
        File.WriteAllBytes(path, crop.EncodeToPNG());
        Object.DestroyImmediate(crop); Object.DestroyImmediate(source);
        return ImportSprite(path, 0, FilterMode.Point);
    }

    private static Sprite ImportSprite(string path, int border, FilterMode filter)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        importer.spriteBorder = Vector4.one * border;
        importer.filterMode = filter;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // One optional batch-mode integration pass, also used to author the assets when MCP is unavailable.
    public static void BuildAndVerify()
    {
        try
        {
            CreateDemo();
            SessionState.SetBool("CharacterSelect.Verify", true);
            SessionState.SetFloat("CharacterSelect.Timeout", (float)EditorApplication.timeSinceStartup + 300);
            SessionState.SetInt("CharacterSelect.Stage", 0);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    [InitializeOnLoadMethod]
    private static void ResumeVerification()
    {
        if (SessionState.GetBool("CharacterSelect.Verify", false)) EditorApplication.update += VerifyUpdate;
    }

    private static int mapHash;
    private static Vector3 previewCamera;
    private static CharacterSelectCard[] testCards;

    private static void VerifyUpdate()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat("CharacterSelect.Timeout", 0))
                throw new TimeoutException("Character selection flow did not complete within the verification budget.");
            var flow = Object.FindAnyObjectByType<RunFlowController>();
            int stage = SessionState.GetInt("CharacterSelect.Stage", 0);
            if (flow == null) return;
            if (stage == 0)
            {
                if (flow.CurrentState != RunFlowController.RunState.SelectingClass) return;
                Require(Object.FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include) == null, "No player before selection");
                Require(Object.FindAnyObjectByType<Enemy>() == null, "No enemies before selection");
                mapHash = MapHash();
                previewCamera = Camera.main.transform.position;
                testCards = Object.FindObjectsByType<CharacterSelectCard>(FindObjectsSortMode.InstanceID);
                Require(testCards.Length == 3, "Three cards exist");
            }
            else if (stage <= 3)
            {
                var pointer = new PointerEventData(EventSystem.current);
                if (stage > 1) testCards[stage - 2].OnPointerExit(pointer);
                testCards[stage - 1].OnPointerEnter(pointer);
            }
            else if (stage == 4)
                testCards[0].OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            else if (stage == 5)
            {
                if (flow.CurrentState != RunFlowController.RunState.Playing) return;
                Require(Object.FindObjectsByType<PlayerController2D>(FindObjectsSortMode.None).Length == 1, "Exactly one selected player");
                Require(SelectedClass.Current != null && flow.Player != null, "Selection holder and player populated");
                Require(MapHash() == mapHash, "Selection does not regenerate the map");
                Require(Vector3.Distance(Camera.main.transform.position, previewCamera) < 0.25f, "No camera jump on spawn");
                Require(Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None).Length > 0, "Enemies spawn after selection");
                Keyboard keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.Backspace));
            }
            else if (stage == 6)
            {
                InputSystem.QueueStateEvent(Keyboard.current, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                if (flow.CurrentState != RunFlowController.RunState.SelectingClass) return;
                Require(MapHash() == mapHash, "Backspace preserves the same map");
                Require(Object.FindAnyObjectByType<PlayerController2D>(FindObjectsInactive.Include) == null, "Backspace removes the player");
                Require(Object.FindAnyObjectByType<Enemy>() == null, "Backspace removes enemies");
                SessionState.SetBool("CharacterSelect.Verify", false);
                File.WriteAllText("Temp/CharacterSelectVerification.txt", "PASS: real map generation, no initial player/enemies, three card hovers, confirmation, one player, camera continuity, enemy spawn, Backspace, unchanged map.\n");
                Debug.Log("CHARACTER SELECT VERIFICATION PASSED");
                EditorApplication.Exit(0);
                return;
            }
            SessionState.SetInt("CharacterSelect.Stage", stage + 1);
        }
        catch (Exception e)
        {
            SessionState.SetBool("CharacterSelect.Verify", false);
            File.WriteAllText("Temp/CharacterSelectVerification.txt", "FAIL: " + e);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static int MapHash()
    {
        var generator = new SerializedObject(Object.FindAnyObjectByType<ProceduralGameplayDemo>());
        var map = (Tilemap)generator.FindProperty("map").objectReferenceValue;
        int hash = 17, count = 0;
        foreach (Vector3Int cell in map.cellBounds.allPositionsWithin) if (map.HasTile(cell))
        { hash = unchecked(hash * 31 + cell.GetHashCode()); count++; }
        Require(count > 0, "Real generated tiles exist");
        return hash;
    }
}
