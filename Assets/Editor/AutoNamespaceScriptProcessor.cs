using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Preferences > Auto Namespace に設定画面を表示する。
/// </summary>
static class AutoNamespaceSettingsProvider
{
    [SettingsProvider]
    static SettingsProvider Create()
    {
        return new SettingsProvider("Preferences/Auto Namespace", SettingsScope.User)
        {
            guiHandler = _ =>
            {
                var settings = AutoNamespaceSettings.instance;
                EditorGUI.BeginChangeCheck();

                settings.enabled = EditorGUILayout.Toggle("有効", settings.enabled);
                using (new EditorGUI.DisabledScope(!settings.enabled))
                {
                    settings.rootNamespace = EditorGUILayout.TextField("Namespace", settings.rootNamespace).Trim();
                    settings.appendFolders = EditorGUILayout.Toggle("フォルダ名を付け足す", settings.appendFolders);

                    string example = AutoNamespaceScriptProcessor.GetNamespace("Assets/Chambara/Scripts/Player.cs");
                    EditorGUILayout.HelpBox(
                        "例: Assets/Chambara/Scripts/Player.cs → " +
                        (string.IsNullOrEmpty(example) ? "(namespaceを追加しない)" : "namespace " + example) +
                        "\nこの設定は UserSettings に保存され、git では共有されません。",
                        MessageType.Info);
                }

                if (EditorGUI.EndChangeCheck()) settings.Save();
            },
            keywords = new[] { "namespace", "script" },
        };
    }
}

/// <summary>
/// Unityエディタ上で新しくC#スクリプトを作成したときに、namespaceを自動で追加する。
/// namespaceは Preferences > Auto Namespace で設定する。
/// </summary>
public class AutoNamespaceScriptProcessor : AssetModificationProcessor
{
    // フォルダ名を付け足すときに含めないフォルダ名
    static readonly HashSet<string> IgnoreFolders = new HashSet<string>
    {
        "Scripts",
        "Script",
        "Editor", // namespace名がEditorだと「: Editor」の継承がエラーになるため除外
    };

    static readonly Regex UsingRegex = new Regex(@"^\s*using\s+[\w\.\s=]+;\s*$");
    static readonly Regex NamespaceRegex = new Regex(@"^\s*namespace\s", RegexOptions.Multiline);

    // Unityエディタ上でアセットが作成される直前に呼ばれる(引数は .meta のパス)
    static void OnWillCreateAsset(string metaPath)
    {
        if (!AutoNamespaceSettings.instance.enabled) return;
        if (!metaPath.EndsWith(".cs.meta", StringComparison.OrdinalIgnoreCase)) return;

        string assetPath = metaPath.Substring(0, metaPath.Length - ".meta".Length);
        if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal)) return;

        // ファイルの書き込み完了を待ってから処理する
        EditorApplication.delayCall += () => AddNamespace(assetPath);
    }

    static void AddNamespace(string assetPath)
    {
        if (!File.Exists(assetPath)) return;

        string ns = GetNamespace(assetPath);
        if (string.IsNullOrEmpty(ns)) return;

        byte[] bytes = File.ReadAllBytes(assetPath);
        bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        string text = new UTF8Encoding(false).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));

        // すでにnamespaceがあれば何もしない
        if (NamespaceRegex.IsMatch(text)) return;

        string newLine = text.Contains("\r\n") ? "\r\n" : "\n";
        List<string> lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();

        // 先頭のusing・空行・コメントはnamespaceの外に残す
        int bodyStart = 0;
        while (bodyStart < lines.Count)
        {
            string trimmed = lines[bodyStart].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//") || UsingRegex.IsMatch(lines[bodyStart]))
            {
                bodyStart++;
                continue;
            }
            break;
        }
        if (bodyStart >= lines.Count) return;

        // コメント行がクラスの直前にある場合(/// summary など)は本体側に含める
        while (bodyStart > 0 && lines[bodyStart - 1].Trim().StartsWith("//")) bodyStart--;

        List<string> header = lines.Take(bodyStart).ToList();
        List<string> body = lines.Skip(bodyStart).ToList();
        while (body.Count > 0 && body[body.Count - 1].Trim().Length == 0) body.RemoveAt(body.Count - 1);

        string indent = lines.Any(l => l.StartsWith("\t")) ? "\t" : "    ";

        var result = new List<string>(header);
        result.Add($"namespace {ns}");
        result.Add("{");
        result.AddRange(body.Select(l => l.Trim().Length == 0 ? string.Empty : indent + l));
        result.Add("}");
        result.Add(string.Empty);

        File.WriteAllText(assetPath, string.Join(newLine, result), new UTF8Encoding(hasBom));
        AssetDatabase.ImportAsset(assetPath);
    }

    // 設定に応じてnamespaceを作る(空文字ならnamespaceを追加しない)
    public static string GetNamespace(string assetPath)
    {
        var settings = AutoNamespaceSettings.instance;
        var parts = new List<string>();

        string root = settings.rootNamespace?.Trim();
        if (!string.IsNullOrEmpty(root)) parts.Add(root);

        // namespace未指定のときはフォルダ名だけで作る
        if (settings.appendFolders || parts.Count == 0)
        {
            string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? string.Empty;
            parts.AddRange(dir.Split('/')
                .Skip(1) // "Assets"
                .Where(f => !IgnoreFolders.Contains(f))
                .Select(ToIdentifier)
                .Where(f => f.Length > 0));
        }

        return string.Join(".", parts);
    }

    // フォルダ名をC#の識別子として使える形に変換する
    static string ToIdentifier(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
        }
        if (sb.Length > 0 && char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }
}
