using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Editor-only walk animation generator: auto-creates 8 directional walk clips (walk-down.anim, etc.)
// from a Character creator Walk.png spritesheet (already sliced into Walk_<row>_<col> sub-sprites).
// Menu: Right-click Walk*.png in Assets → "Generate Walk Animations", or Tools/Animation/Generate Walk Clips.
//
// Layout: expects 8 rows × 15 columns of 64×64 sprites, already sliced (Sprite Mode: Multiple).
// Generates one looping 12-fps AnimationClip per row, bound to SpriteRenderer.m_Sprite.
// Outputs clips beside the source PNG (same folder as existing run-*.anim).
public static class WalkAnimGenerator
{
    // Row index → compass direction. One array, one place: if the order is wrong, a quick fix.
    // Row order derived from the direction-correct run-*.anim clips (same character creator).
    static readonly string[] DefaultRowToDir = { "up-right", "up", "up-left", "left", "down-left", "down", "down-right", "right" };

    // Assets/Generate Walk Animations — context menu for right-clicking a Walk*.png (or its folder).
    [MenuItem("Assets/Generate Walk Animations", false, 2000)]
    static void GenerateFromSelection()
    {
        UnityEngine.Object sel = Selection.activeObject;
        if (sel == null)
            return;

        string selPath = AssetDatabase.GetAssetPath(sel);

        // If it's a folder, look for a top-level Walk*.png inside it.
        if (AssetDatabase.IsValidFolder(selPath))
        {
            string[] guids = AssetDatabase.FindAssets("Walk*", new[] { selPath });
            string walkPng = null;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Only top-level files in the folder (no subdirs).
                if (Path.GetDirectoryName(path) == selPath && path.EndsWith(".png"))
                {
                    walkPng = path;
                    break;
                }
            }
            if (walkPng == null)
            {
                Debug.LogError($"[WalkAnimGenerator] No Walk*.png found at top level of folder: {selPath}");
                return;
            }
            selPath = walkPng;
        }

        // selPath is now the Walk*.png asset path.
        string outputDir = Path.GetDirectoryName(selPath);
        Generate(selPath, outputDir, DefaultRowToDir);
    }

    // Validator for "Assets/Generate Walk Animations" — only show if selection is Walk*.png or a folder with one.
    [MenuItem("Assets/Generate Walk Animations", true)]
    static bool ValidateGenerateFromSelection()
    {
        UnityEngine.Object sel = Selection.activeObject;
        if (sel == null)
            return false;

        string selPath = AssetDatabase.GetAssetPath(sel);

        // Direct file: must be Walk*.png.
        if (!AssetDatabase.IsValidFolder(selPath))
        {
            return selPath.EndsWith(".png") && Path.GetFileName(selPath).Contains("Walk");
        }

        // Folder: must contain a top-level Walk*.png.
        string[] guids = AssetDatabase.FindAssets("Walk*", new[] { selPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetDirectoryName(path) == selPath && path.EndsWith(".png"))
                return true;
        }
        return false;
    }

    // Alias menu entry for discoverability (Tools/Animation/Generate Walk Clips).
    [MenuItem("Tools/Animation/Generate Walk Clips")]
    static void GenerateWalkClipsAlias() => GenerateFromSelection();

    // Core generator: loads sprites from a Walk.png spritesheet, groups by row, generates 8 clips.
    public static void Generate(string pngPath, string outputDir, string[] rowToDir)
    {
        // Load all sub-sprites from the Walk*.png asset.
        UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(pngPath);
        var sprites = new Dictionary<int, Dictionary<int, Sprite>>();

        foreach (UnityEngine.Object asset in allAssets)
        {
            Sprite sprite = asset as Sprite;
            if (sprite == null)
                continue;

            // Parse name: Walk_<row>_<col>
            string name = sprite.name;
            if (!name.StartsWith("Walk_"))
                continue;

            string[] parts = name.Split('_');
            if (parts.Length != 3 || !int.TryParse(parts[1], out int row) || !int.TryParse(parts[2], out int col))
                continue;

            if (!sprites.ContainsKey(row))
                sprites[row] = new Dictionary<int, Sprite>();

            sprites[row][col] = sprite;
        }

        // Guard: exactly 8 rows (120 = 8 × 15 sprite grid).
        if (sprites.Count != 8)
        {
            Debug.LogError($"[WalkAnimGenerator] Expected 8 rows (one per direction), found {sprites.Count} in {pngPath}. Aborting.");
            return;
        }

        int clipsWritten = 0;
        int rowsSkipped = 0;

        // Generate one clip per row.
        for (int r = 0; r < 8; r++)
        {
            if (!sprites.ContainsKey(r))
            {
                Debug.LogError($"[WalkAnimGenerator] Row {r} missing entirely from {pngPath}. Skipping.");
                rowsSkipped++;
                continue;
            }

            var rowSprites = sprites[r];
            // Guard: each row must have exactly 15 frames (columns 0..14).
            if (rowSprites.Count != 15)
            {
                Debug.LogError($"[WalkAnimGenerator] Row {r} has {rowSprites.Count} frames, expected 15. Skipping this row (no 1-frame clips).");
                rowsSkipped++;
                continue;
            }

            // Sort by numeric column index to avoid "Walk_0_10" sorting before "Walk_0_2".
            var sortedCols = rowSprites.Keys.OrderBy(c => c).ToList();

            // Create the clip.
            var clip = new AnimationClip { frameRate = 12f };

            // Build sprite keyframe curve.
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = new ObjectReferenceKeyframe[15];
            for (int c = 0; c < 15; c++)
            {
                keys[c] = new ObjectReferenceKeyframe
                {
                    time = c / 12f,
                    value = rowSprites[sortedCols[c]]
                };
            }
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            // Set loop flag.
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // Write to asset database (idempotent overwrite).
            string dirName = rowToDir[r];
            string outputPath = $"{outputDir}/walk-{dirName}.anim";

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath) != null)
                AssetDatabase.DeleteAsset(outputPath);

            AssetDatabase.CreateAsset(clip, outputPath);
            clipsWritten++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[WalkAnimGenerator] Generated {clipsWritten} walk clips from {Path.GetFileName(pngPath)} to {outputDir}. {(rowsSkipped > 0 ? $"({rowsSkipped} rows skipped due to frame count mismatch.)" : "")}");
    }
}
