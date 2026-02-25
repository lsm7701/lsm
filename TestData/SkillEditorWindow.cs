using UnityEditor;
using UnityEngine;
using System;
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
        window.minSize = new Vector2(900, 600);
    }

    private void OnEnable()
    {
        _tabs = new List<ITabContent>
        {
            new SkillTab(),
            new SequenceTab(),
            new EffectTab(),
            new ConditionTab(),
            new SummaryTab()
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

        EditorGUILayout.Space(12);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (_tabs != null && _tabs.Count > _selectedTabIndex)
        {
            _tabs[_selectedTabIndex].OnGUI();
        }

        EditorGUILayout.EndVertical();
    }
}

public enum ValueKind
{
    Int,
    Float,
    String
}

[Serializable]
public class DynamicField
{
    public string Key;
    public ValueKind Kind;
    public string Value;
}

[Serializable]
public class TypeSchema
{
    public string TypeName;
    public List<string> AllowedIntKeys = new List<string>();
    public List<string> AllowedFloatKeys = new List<string>();
    public List<string> AllowedStringKeys = new List<string>();
    public List<DynamicField> DefaultPreset = new List<DynamicField>();
}

public abstract class DynamicTypeTabBase : ITabContent
{
    private int _selectedTypeIndex;
    private Vector2 _scroll;
    private readonly List<DynamicField> _fields = new List<DynamicField>();

    protected abstract string HeaderLabel { get; }
    protected abstract List<TypeSchema> Schemas { get; }
    public abstract string TabName { get; }

    public void OnEnable()
    {
        _selectedTypeIndex = 0;
        _fields.Clear();
    }

    public void OnGUI()
    {
        EditorGUILayout.LabelField(HeaderLabel, EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        if (Schemas == null || Schemas.Count == 0)
        {
            EditorGUILayout.HelpBox("타입 스키마가 없습니다.", MessageType.Warning);
            return;
        }

        var typeNames = Schemas.Select(s => s.TypeName).ToArray();
        _selectedTypeIndex = EditorGUILayout.Popup("타입", _selectedTypeIndex, typeNames);
        var schema = Schemas[Mathf.Clamp(_selectedTypeIndex, 0, Schemas.Count - 1)];

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("타입 기본값 채우기", GUILayout.Width(140)))
        {
            ApplyPreset(schema);
        }

        if (GUILayout.Button("항목 추가", GUILayout.Width(90)))
        {
            AddEmptyField(schema);
        }

        if (GUILayout.Button("항목 정렬(키)", GUILayout.Width(110)))
        {
            _fields.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        DrawKeyGuide(schema);

        EditorGUILayout.Space(8);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        for (int i = 0; i < _fields.Count; i++)
        {
            DrawFieldRow(schema, _fields[i], i);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawKeyGuide(TypeSchema schema)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("사용 가능 키(타입별)", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField($"INT: {string.Join(", ", schema.AllowedIntKeys)}", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField($"FLOAT: {string.Join(", ", schema.AllowedFloatKeys)}", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField($"STRING: {string.Join(", ", schema.AllowedStringKeys)}", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawFieldRow(TypeSchema schema, DynamicField field, int index)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        var allowedKeys = GetAllowedKeysByKind(schema, field.Kind).ToArray();
        var selectedKey = Mathf.Max(0, Array.IndexOf(allowedKeys, field.Key));

        field.Kind = (ValueKind)EditorGUILayout.EnumPopup(field.Kind, GUILayout.Width(70));
        allowedKeys = GetAllowedKeysByKind(schema, field.Kind).ToArray();

        if (allowedKeys.Length > 0)
        {
            selectedKey = Mathf.Clamp(selectedKey, 0, allowedKeys.Length - 1);
            selectedKey = EditorGUILayout.Popup(selectedKey, allowedKeys, GUILayout.Width(260));
            field.Key = allowedKeys[selectedKey];
        }
        else
        {
            field.Key = EditorGUILayout.TextField(field.Key ?? string.Empty, GUILayout.Width(260));
        }

        field.Value = EditorGUILayout.TextField(field.Value ?? string.Empty);

        if (GUILayout.Button("삭제", GUILayout.Width(50)))
        {
            _fields.RemoveAt(index);
            EditorGUILayout.EndHorizontal();
            return;
        }

        EditorGUILayout.EndHorizontal();
    }

    private IEnumerable<string> GetAllowedKeysByKind(TypeSchema schema, ValueKind kind)
    {
        switch (kind)
        {
            case ValueKind.Int: return schema.AllowedIntKeys;
            case ValueKind.Float: return schema.AllowedFloatKeys;
            case ValueKind.String: return schema.AllowedStringKeys;
            default: return Array.Empty<string>();
        }
    }

    private void ApplyPreset(TypeSchema schema)
    {
        _fields.Clear();
        foreach (var p in schema.DefaultPreset)
        {
            _fields.Add(new DynamicField
            {
                Key = p.Key,
                Kind = p.Kind,
                Value = p.Value
            });
        }
    }

    private void AddEmptyField(TypeSchema schema)
    {
        string defaultKey = schema.AllowedStringKeys.FirstOrDefault()
                            ?? schema.AllowedIntKeys.FirstOrDefault()
                            ?? schema.AllowedFloatKeys.FirstOrDefault()
                            ?? "NEW_KEY";

        _fields.Add(new DynamicField
        {
            Key = defaultKey,
            Kind = ValueKind.String,
            Value = string.Empty
        });
    }
}

public class SkillTab : DynamicTypeTabBase
{
    public override string TabName => "스킬";
    protected override string HeaderLabel => "스킬 항목 편집";

    private static readonly List<TypeSchema> _schemas = new List<TypeSchema>
    {
        new TypeSchema
        {
            TypeName = "액티브",
            AllowedIntKeys = new List<string> { "NEXT_SKILL_ID", "ENABLE_INDICATOR_SKILL" },
            AllowedFloatKeys = new List<string> { "COOL_TIME", "ATTACK_DISTANCE", "USE_EP", "GET_EP" },
            AllowedStringKeys = new List<string> { "SKILL_TYPE", "SKILL_OPTIONS", "ACTIVATE_TYPES", "RANGE_TYPE", "DESC" },
            DefaultPreset = new List<DynamicField>
            {
                new DynamicField { Key = "SKILL_TYPE", Kind = ValueKind.String, Value = "ACTIVE" },
                new DynamicField { Key = "COOL_TIME", Kind = ValueKind.Float, Value = "5" },
                new DynamicField { Key = "USE_EP", Kind = ValueKind.Float, Value = "0" },
                new DynamicField { Key = "DESC", Kind = ValueKind.String, Value = "" }
            }
        },
        new TypeSchema
        {
            TypeName = "패시브",
            AllowedIntKeys = new List<string> { "PLAYING_POSSIBLE_RESERVATION" },
            AllowedFloatKeys = new List<string> { "ENABLE", "ENABLE_NEXT" },
            AllowedStringKeys = new List<string> { "SKILL_TYPE", "SKILL_OPTIONS", "DESC" },
            DefaultPreset = new List<DynamicField>
            {
                new DynamicField { Key = "SKILL_TYPE", Kind = ValueKind.String, Value = "PASSIVE" },
                new DynamicField { Key = "ENABLE", Kind = ValueKind.Float, Value = "1" },
                new DynamicField { Key = "DESC", Kind = ValueKind.String, Value = "" }
            }
        }
    };

    protected override List<TypeSchema> Schemas => _schemas;
}

public class SequenceTab : DynamicTypeTabBase
{
    public override string TabName => "시퀀스";
    protected override string HeaderLabel => "시퀀스 항목 편집";

    private static readonly List<TypeSchema> _schemas = new List<TypeSchema>
    {
        new TypeSchema
        {
            TypeName = "데미지",
            AllowedIntKeys = new List<string> { "COUNT", "PLAY_INDICATOR" },
            AllowedFloatKeys = new List<string> { "START_DELAY_TIME", "DURATION_TIME", "FLOAT_VALUE" },
            AllowedStringKeys = new List<string> { "SEQUENCE_TYPE", "DAMAGE_TYPE", "CONDITION_IDS", "CREATE_IDS", "DESC" },
            DefaultPreset = new List<DynamicField>
            {
                new DynamicField { Key = "SEQUENCE_TYPE", Kind = ValueKind.String, Value = "DAMAGE" },
                new DynamicField { Key = "START_DELAY_TIME", Kind = ValueKind.Float, Value = "0" },
                new DynamicField { Key = "DESC", Kind = ValueKind.String, Value = "" }
            }
        }
    };

    protected override List<TypeSchema> Schemas => _schemas;
}

public class EffectTab : DynamicTypeTabBase
{
    public override string TabName => "에펙";
    protected override string HeaderLabel => "에펙 항목 편집";

    private static readonly List<TypeSchema> _schemas = new List<TypeSchema>
    {
        new TypeSchema
        {
            TypeName = "버프/디버프",
            AllowedIntKeys = new List<string> { "OVERLAP_COUNT", "PRIORITY", "INT_VALUE" },
            AllowedFloatKeys = new List<string> { "START_DELAY_TIME", "DURATION_TIME", "FLOAT_VALUE" },
            AllowedStringKeys = new List<string> { "AFFECT_TYPE", "BUFF_TYPE", "END_TYPE", "CONDITION_IDS", "DESC" },
            DefaultPreset = new List<DynamicField>
            {
                new DynamicField { Key = "AFFECT_TYPE", Kind = ValueKind.String, Value = "BUFF" },
                new DynamicField { Key = "DURATION_TIME", Kind = ValueKind.Float, Value = "3" },
                new DynamicField { Key = "DESC", Kind = ValueKind.String, Value = "" }
            }
        }
    };

    protected override List<TypeSchema> Schemas => _schemas;
}

public class ConditionTab : DynamicTypeTabBase
{
    public override string TabName => "컨디션";
    protected override string HeaderLabel => "컨디션 항목 편집";

    private static readonly List<TypeSchema> _schemas = new List<TypeSchema>
    {
        new TypeSchema
        {
            TypeName = "타겟팅",
            AllowedIntKeys = new List<string> { "ABSOLUTE_SELECT", "UNCHECK_ENABLE_TARGET", "CHECK_INPUTVALUE" },
            AllowedFloatKeys = new List<string> { "FLOAT_VALUE", "FLOAT_OPTION" },
            AllowedStringKeys = new List<string> { "CONDITION_TYPE", "TARGETING_TEAM", "TARGETING_SELECT", "OPERATOR", "DESC" },
            DefaultPreset = new List<DynamicField>
            {
                new DynamicField { Key = "CONDITION_TYPE", Kind = ValueKind.String, Value = "TARGETING" },
                new DynamicField { Key = "TARGETING_TEAM", Kind = ValueKind.String, Value = "ENEMY" },
                new DynamicField { Key = "DESC", Kind = ValueKind.String, Value = "" }
            }
        }
    };

    protected override List<TypeSchema> Schemas => _schemas;
}

public class SummaryTab : ITabContent
{
    public string TabName => "종합";

    public void OnEnable() { }

    public void OnGUI()
    {
        EditorGUILayout.LabelField("종합 트리(합본)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "여기는 스킬-시퀀스-에펙-컨디션 연결 트리 합본을 표시하는 자리입니다.\n" +
            "다음 단계에서 TreeView 기반 계층 편집/복구를 연결합니다.",
            MessageType.Info);
    }
}
