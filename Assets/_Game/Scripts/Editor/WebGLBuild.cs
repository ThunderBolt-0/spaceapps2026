using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MissionTrainer.EditorTools
{
    /// <summary>
    /// Menu: Mission Trainer > Build WebGL (itch.io).
    /// Builds the Mars scene to Builds/WebGL with settings that work on itch.io and on a plain local server:
    /// Gzip + decompression fallback (no special server headers needed), data caching, 1280x720.
    /// Writes a short report to Logs/WebGLBuildReport.txt.
    /// Command line: Unity -batchmode -projectPath . -executeMethod MissionTrainer.EditorTools.WebGLBuild.Build
    /// </summary>
    public static class WebGLBuild
    {
        const string OutDir = "Builds/WebGL";
        const string ReportPath = "Logs/WebGLBuildReport.txt";
        static readonly string[] Scenes = { "Assets/_Game/Scenes/Mars.unity" };

        [MenuItem("Mission Trainer/Build WebGL (itch.io)")]
        public static void Build()
        {
            if (!File.Exists(Scenes[0]))
            {
                Debug.LogError("Mars scene missing. Run Mission Trainer > Build Mars Scene (3D) first.");
                return;
            }

            PlayerSettings.productName = "The Red Frontier";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            if (Directory.Exists("Assets/WebGLTemplates/MarsFit")) PlayerSettings.WebGL.template = "PROJECT:MarsFit";   // fills the window
            // Brotli: about 15-20% smaller than Gzip (keeps every file under Cloudflare Pages' 25 MiB limit).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;   // works even if the server doesn't send compression headers
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            // Keep every file under Cloudflare Pages' 25 MiB limit: textures at most 1024 px in the web build
            // (2048 terrain/rock textures are 4x bigger and barely shrink with gzip). The editor keeps full size.
            CapWebTextures(1024);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = OutDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });

            var s = report.summary;
            var sb = new StringBuilder();
            sb.AppendLine($"Result: {s.result}");
            sb.AppendLine($"Time: {s.totalTime}");
            sb.AppendLine($"Size: {s.totalSize / (1024f * 1024f):0.0} MB");
            sb.AppendLine($"Errors: {s.totalErrors}  Warnings: {s.totalWarnings}");
            sb.AppendLine($"Output: {Path.GetFullPath(OutDir)}");
            foreach (var step in report.steps)
                foreach (var m in step.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Warning))
                    sb.AppendLine($"[{m.type}] {step.name}: {m.content}");
            if (Directory.Exists(OutDir))
            {
                sb.AppendLine("Files:");
                foreach (var f in Directory.GetFiles(OutDir, "*", SearchOption.AllDirectories).OrderByDescending(f => new FileInfo(f).Length).Take(12))
                    sb.AppendLine($"  {new FileInfo(f).Length / (1024f * 1024f),7:0.00} MB  {f}");
            }
            if (s.result == BuildResult.Succeeded) PublishToWeb(sb);
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log("WebGL build finished:\n" + sb);

            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>
        /// WebGL-only texture settings for every texture in the game: at most 'max' pixels and Crunch compression
        /// (much smaller downloads). Saved in the .meta files; the editor and other platforms keep full quality.
        /// </summary>
        static void CapWebTextures(int max)
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                var ps = ti.GetPlatformTextureSettings("WebGL");
                int size = Mathf.Min(max, ti.maxTextureSize);
                if (ps.overridden && ps.maxTextureSize == size && ps.crunchedCompression) continue;
                ps.overridden = true;
                ps.maxTextureSize = size;
                ps.format = TextureImporterFormat.Automatic;
                ps.textureCompression = TextureImporterCompression.Compressed;
                ps.crunchedCompression = true;
                ps.compressionQuality = 60;
                ti.SetPlatformTextureSettings(ps);
                ti.SaveAndReimport();
                changed++;
            }
            if (changed > 0) Debug.Log($"WebGL textures: capped {changed} textures at {max} px with Crunch compression.");
        }

        const string WebDir = "web";
        const long CloudflareMaxFile = 25L * 1024 * 1024;   // Cloudflare Pages: max 25 MiB per file

        /// <summary>
        /// Copies the finished build into web/ (tracked in git). Cloudflare Pages serves that folder:
        /// build command none, output directory "web". Warns if a file is too big for Cloudflare Pages.
        /// </summary>
        static void PublishToWeb(StringBuilder sb)
        {
            if (Directory.Exists(WebDir)) Directory.Delete(WebDir, true);
            foreach (var src in Directory.GetFiles(OutDir, "*", SearchOption.AllDirectories))
            {
                var dst = Path.Combine(WebDir, Path.GetRelativePath(OutDir, src));
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(src, dst, true);
            }
            // Cache the big game files for an hour; the page itself always fresh.
            File.WriteAllText(Path.Combine(WebDir, "_headers"),
                "/Build/*\n  Cache-Control: public, max-age=3600\n/index.html\n  Cache-Control: no-cache\n");
            sb.AppendLine($"Copied to {Path.GetFullPath(WebDir)} (for Cloudflare Pages).");
            foreach (var f in Directory.GetFiles(WebDir, "*", SearchOption.AllDirectories))
                if (new FileInfo(f).Length > CloudflareMaxFile)
                    sb.AppendLine($"[Warning] {f} is {new FileInfo(f).Length / (1024f * 1024f):0.0} MB: too big for Cloudflare Pages (25 MiB per file).");
        }
    }
}
