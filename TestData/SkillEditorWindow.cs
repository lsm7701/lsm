using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public interface ITabContent
{
    string TabName { get; }
    void OnEnable();
    void OnGUI();
}

public class SkillEditorWindow : EditorWindow
{
    private List<ITabContent> _tabs;
    private string[] _tabNames;
    private int _selectedTabIndex;

    [MenuItem("Battle/전투 스킬 툴")]
    public static void ShowWindow()
    {
        var window = GetWindow<SkillEditorWindow>("전투 스킬 툴");
        window.minSize = new Vector2(500, 400);
    }

    private void OnEnable()
    {
        _tabs = new List<ITabContent>
        {
            new SkillTab(),
            new SequenceTab(),
            new EffectTab(),
            new Temp1Tab(),
            new Temp2Tab()
        };

        _tabNames = _tabs.Select(t => t.TabName).ToArray();
        foreach (var tab in _tabs) tab.OnEnable();

        if (_selectedTabIndex < 0 || _selectedTabIndex >= _tabs.Count)
        {
            _selectedTabIndex = 0;
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        var toolbarStyle = new GUIStyle(EditorStyles.toolbarButton)
        {
            fixedHeight = 25,
            fontSize = 12
        };

        _selectedTabIndex = GUILayout.Toolbar(_selectedTabIndex, _tabNames, toolbarStyle);

        EditorGUILayout.Space(15);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (_tabs != null && _tabs.Count > _selectedTabIndex)
        {
            _tabs[_selectedTabIndex].OnGUI();
        }

        EditorGUILayout.EndVertical();
    }
}

public class SkillTab : ITabContent
{
    public string TabName => "스킬";
    public void OnEnable() { }
    public void OnGUI() { }
}

public class SequenceTab : ITabContent
{
    public string TabName => "시퀀스";
    public void OnEnable() { }
    public void OnGUI() { }
}

public class EffectTab : ITabContent
{
    public string TabName => "에펙";
    public void OnEnable() { }
    public void OnGUI() { }
}

public class Temp1Tab : ITabContent
{
    public string TabName => "임시1";
    public void OnEnable() { }
    public void OnGUI() { }
}

public class Temp2Tab : ITabContent
{
    public string TabName => "임시2";
    public void OnEnable() { }
    public void OnGUI() { }
}
