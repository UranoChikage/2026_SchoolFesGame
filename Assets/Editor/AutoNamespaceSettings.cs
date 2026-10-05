using UnityEditor;
using UnityEngine;

/// <summary>
/// Auto Namespace の個人設定。
/// UserSettings フォルダに保存されるため git では共有されない。
/// </summary>
[FilePath("UserSettings/AutoNamespaceSettings.asset", FilePathAttribute.Location.ProjectFolder)]
public class AutoNamespaceSettings : ScriptableSingleton<AutoNamespaceSettings>
{
    [Tooltip("新規スクリプトにnamespaceを自動で追加する")]
    public bool enabled = true;

    [Tooltip("追加するnamespace。空の場合はフォルダ名だけで作る")]
    public string rootNamespace = "";

    [Tooltip("フォルダ構成をnamespaceの後ろに付け足す (例: Root.Chambara)")]
    public bool appendFolders = false;

    public void Save() => Save(true);
}
