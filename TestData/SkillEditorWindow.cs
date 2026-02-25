using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

// 1. 탭의 공통 규칙을 정의하는 인터페이스
public interface ITabContent
{
    string TabName { get; }
    void OnEnable();
    void OnGUI();
}

// 2. 메인 에디터 윈도우 클래스
public class SkillEditorWindow : EditorWindow
{
    private List<ITabContent> _tabs;
    private string[] _tabNames;
    private int _selectedTabIndex = 0;

    // 요청하신 경로: Battle > 전투 스킬 툴
    [MenuItem("Battle/전투 스킬 툴")]
    public static void ShowWindow()
    {
        var window = GetWindow<SkillEditorWindow>("전투 스킬 툴");
        window.minSize = new Vector2(500, 400);
    }

    private void OnEnable()
    {
        // 새로운 탭 클래스를 만들면 여기에 추가만 하면 자동으로 메뉴에 등록됩니다.
        _tabs = new List<ITabContent>
        {
            new SkillListTab(),
            new SkillEffectTab(),
            new SkillDataTab()
        };

        _tabNames = _tabs.Select(t => t.TabName).ToArray();
        foreach (var tab in _tabs) tab.OnEnable();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        // 이미지와 유사한 상단 탭 버튼 스타일 (Toolbar)
        GUIStyle toolbarStyle = new GUIStyle(EditorStyles.toolbarButton) 
        { 
            fixedHeight = 25,
            fontSize = 12
        };
        
        _selectedTabIndex = GUILayout.Toolbar(_selectedTabIndex, _tabNames, toolbarStyle);

        EditorGUILayout.Space(15);

        // 가운데 에디터 영역을 그리는 부분
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        if (_tabs != null && _tabs.Count > _selectedTabIndex)
        {
            // 선택된 클래스의 OnGUI를 호출하여 화면을 새로 그립니다.
            _tabs[_selectedTabIndex].OnGUI();
        }

        EditorGUILayout.EndVertical();
    }
}

// --- 각 탭의 기능을 담당하는 클래스들 ---

public class SkillListTab : ITabContent
{
    public string TabName => "페이지 1"; // 이미지 상의 이름 반영
    public void OnEnable() { }
    public void OnGUI()
    {
        GUILayout.Label("⚔️ 전투 스킬 목록", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        if (GUILayout.Button("새 스킬 추가", GUILayout.Height(30)))
        {
            Debug.Log("새로운 전투 스킬 항목이 생성되었습니다.");
        }
    }
}

public class SkillEffectTab : ITabContent
{
    public string TabName => "[빈페이지 2]";
    public void OnEnable() { }
    public void OnGUI()
    {
        GUILayout.Label("✨ 이펙트 설정", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("스킬별 파티클 및 사운드를 할당하는 페이지입니다.", MessageType.Info);
    }
}

public class SkillDataTab : ITabContent
{
    public string TabName => "[빈 페이지 3]";
    public void OnEnable() { }
    public void OnGUI()
    {
        GUILayout.Label("📊 데이터 테이블 추출", EditorStyles.boldLabel);
        if (GUILayout.Button("JSON 데이터로 내보내기"))
        {
            Debug.Log("스킬 데이터가 성공적으로 추출되었습니다.");
        }
    }
}