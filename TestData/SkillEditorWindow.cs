using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

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

[Serializable]
public class TreeNodeData
{
    public int Id;
    public string Name;
    public List<DynamicField> Fields = new List<DynamicField>();
}

public class TreeCategoryData
{
    public string CategoryName;
    public int NextId = 1;
    public readonly List<TreeNodeData> Nodes = new List<TreeNodeData>();

    public TreeCategoryData(string name)
    {
        CategoryName = name;
    }

    public TreeNodeData CreateNode(TypeSchema schema)
    {
        var node = new TreeNodeData
        {
            Id = NextId++,
            Name = $"{CategoryName}_{NextId - 1}",
            Fields = schema.DefaultPreset.Select(p => new DynamicField { Key = p.Key, Kind = p.Kind, Value = p.Value }).ToList()
        };
        Nodes.Add(node);
        return node;
    }

    public bool RemoveNode(int id)
    {
        var index = Nodes.FindIndex(n => n.Id == id);
        if (index < 0) return false;
        Nodes.RemoveAt(index);
        return true;
    }

    public TreeNodeData Find(int id) => Nodes.FirstOrDefault(n => n.Id == id);
}

public static class SkillTreeStore
{
    public static readonly TreeCategoryData Skill = new TreeCategoryData("스킬");
    public static readonly TreeCategoryData Sequence = new TreeCategoryData("시퀀스");
    public static readonly TreeCategoryData Effect = new TreeCategoryData("에펙");
    public static readonly TreeCategoryData Condition = new TreeCategoryData("컨디션");

    public static TreeCategoryData GetCategory(string tabName)
    {
        switch (tabName)
        {
            case "스킬": return Skill;
            case "시퀀스": return Sequence;
            case "에펙": return Effect;
            case "컨디션": return Condition;
            default: return null;
        }
    }
}

public static class SkillJsonIo
{
    private static string BasePath
    {
        get
        {
            var fixedPath = "/mnt/c/project/lsm/TestData";
            if (Directory.Exists(fixedPath)) return fixedPath;
            return Application.dataPath;
        }
    }

    public static string FilePathForTab(string tabName)
    {
        switch (tabName)
        {
            case "스킬": return Path.Combine(BasePath, "skill.json");
            case "시퀀스": return Path.Combine(BasePath, "sequence.json");
            case "에펙": return Path.Combine(BasePath, "affect.json");
            case "컨디션": return Path.Combine(BasePath, "condition.json");
            default: return null;
        }
    }

    public static bool Import(string tabName, TreeCategoryData category, out string message)
    {
        var filePath = FilePathForTab(tabName);
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            message = $"파일이 없습니다: {filePath}";
            return false;
        }

        try
        {
            var raw = File.ReadAllText(filePath, Encoding.UTF8);
            var root = RelaxedJsonParser.ParseObject(raw);
            category.Nodes.Clear();

            foreach (var pair in root)
            {
                if (!int.TryParse(pair.Key, out var id)) continue;
                if (!(pair.Value is Dictionary<string, object> dataObj)) continue;

                var node = new TreeNodeData { Id = id, Name = $"{category.CategoryName}_{id}" };
                foreach (var kv in dataObj)
                {
                    var field = new DynamicField { Key = kv.Key, Value = ToFieldString(kv.Value), Kind = GuessKind(kv.Value) };
                    node.Fields.Add(field);
                }

                var desc = node.Fields.FirstOrDefault(f => f.Key == "DESC")?.Value;
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    node.Name = desc;
                }
                category.Nodes.Add(node);
            }

            category.NextId = category.Nodes.Count == 0 ? 1 : (category.Nodes.Max(n => n.Id) + 1);
            message = $"가져오기 완료: {filePath} ({category.Nodes.Count}건)";
            return true;
        }
        catch (Exception ex)
        {
            message = $"가져오기 실패: {ex.Message}";
            return false;
        }
    }

    public static bool Export(string tabName, TreeCategoryData category, out string message)
    {
        var filePath = FilePathForTab(tabName);
        if (string.IsNullOrEmpty(filePath))
        {
            message = "파일 경로를 찾을 수 없습니다.";
            return false;
        }

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            for (int i = 0; i < category.Nodes.Count; i++)
            {
                var node = category.Nodes[i];
                sb.Append($"  \"{node.Id}\": {{\n");
                for (int f = 0; f < node.Fields.Count; f++)
                {
                    var field = node.Fields[f];
                    var value = ToJsonValue(field);
                    var comma = f == node.Fields.Count - 1 ? "" : ",";
                    sb.Append($"    \"{Escape(field.Key)}\": {value}{comma}\n");
                }
                var nodeComma = i == category.Nodes.Count - 1 ? "" : ",";
                sb.Append($"  }}{nodeComma}\n");
            }
            sb.AppendLine("}");

            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(false));
            message = $"내보내기 완료: {filePath}";
            return true;
        }
        catch (Exception ex)
        {
            message = $"내보내기 실패: {ex.Message}";
            return false;
        }
    }

    private static ValueKind GuessKind(object value)
    {
        if (value is long || value is int) return ValueKind.Int;
        if (value is double || value is float || value is decimal) return ValueKind.Float;
        return ValueKind.String;
    }

    private static string ToFieldString(object value)
    {
        if (value == null) return string.Empty;
        if (value is string s) return s;
        if (value is List<object> list) return string.Join(",", list.Select(ToFieldString));
        if (value is Dictionary<string, object>) return "{...}";
        return Convert.ToString(value);
    }

    private static string ToJsonValue(DynamicField field)
    {
        if (field.Kind == ValueKind.Int && int.TryParse(field.Value, out var i)) return i.ToString();
        if (field.Kind == ValueKind.Float && double.TryParse(field.Value, out var d)) return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"\"{Escape(field.Value ?? string.Empty)}\"";
    }

    private static string Escape(string s) => (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
}

public static class RelaxedJsonParser
{
    public static Dictionary<string, object> ParseObject(string source)
    {
        var cleaned = Regex.Replace(source, @"//.*?$", string.Empty, RegexOptions.Multiline);
        cleaned = Regex.Replace(cleaned, @",\s*([}\]])", "$1");
        var parser = new MiniParser(cleaned);
        var obj = parser.ParseValue() as Dictionary<string, object>;
        return obj ?? new Dictionary<string, object>();
    }

    private class MiniParser
    {
        private readonly string _s;
        private int _i;

        public MiniParser(string s) { _s = s ?? ""; _i = 0; }

        public object ParseValue()
        {
            Skip();
            if (_i >= _s.Length) return null;
            var c = _s[_i];
            if (c == '{') return ParseObject();
            if (c == '[') return ParseArray();
            if (c == '"') return ParseString();
            if (char.IsDigit(c) || c == '-') return ParseNumber();
            if (Match("true")) return true;
            if (Match("false")) return false;
            if (Match("null")) return null;
            return ParseBare();
        }

        private Dictionary<string, object> ParseObject()
        {
            var dict = new Dictionary<string, object>();
            _i++;
            while (_i < _s.Length)
            {
                Skip();
                if (_i < _s.Length && _s[_i] == '}') { _i++; break; }
                var key = ParseString();
                Skip(); if (_i < _s.Length && _s[_i] == ':') _i++;
                var val = ParseValue();
                dict[key] = val;
                Skip();
                if (_i < _s.Length && _s[_i] == ',') _i++;
            }
            return dict;
        }

        private List<object> ParseArray()
        {
            var list = new List<object>();
            _i++;
            while (_i < _s.Length)
            {
                Skip();
                if (_i < _s.Length && _s[_i] == ']') { _i++; break; }
                list.Add(ParseValue());
                Skip();
                if (_i < _s.Length && _s[_i] == ',') _i++;
            }
            return list;
        }

        private string ParseString()
        {
            if (_s[_i] != '"') return ParseBare();
            _i++;
            var sb = new StringBuilder();
            while (_i < _s.Length)
            {
                var c = _s[_i++];
                if (c == '"') break;
                if (c == '\\' && _i < _s.Length)
                {
                    var n = _s[_i++];
                    switch (n)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(n); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private object ParseNumber()
        {
            var start = _i;
            while (_i < _s.Length && "-+0123456789.eE".IndexOf(_s[_i]) >= 0) _i++;
            var token = _s.Substring(start, _i - start);
            if (token.IndexOf('.') >= 0 || token.IndexOf('e') >= 0 || token.IndexOf('E') >= 0)
            {
                if (double.TryParse(token, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
            }
            if (long.TryParse(token, out var l)) return l;
            return 0;
        }

        private string ParseBare()
        {
            var start = _i;
            while (_i < _s.Length && ",]}:\r\n\t ".IndexOf(_s[_i]) < 0) _i++;
            return _s.Substring(start, _i - start).Trim();
        }

        private bool Match(string token)
        {
            Skip();
            if (_i + token.Length > _s.Length) return false;
            if (string.Compare(_s, _i, token, 0, token.Length, StringComparison.Ordinal) == 0)
            {
                _i += token.Length;
                return true;
            }
            return false;
        }

        private void Skip()
        {
            while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
        }
    }
}

public static class EnumKeyCatalog
{
    private static readonly Dictionary<string, Dictionary<ValueKind, HashSet<string>>> _cache = new Dictionary<string, Dictionary<ValueKind, HashSet<string>>>();

    public static IEnumerable<string> GetKeys(string tabName, ValueKind kind)
    {
        EnsureLoaded(tabName);
        if (_cache.TryGetValue(tabName, out var byKind) && byKind.TryGetValue(kind, out var set))
        {
            return set;
        }
        return Array.Empty<string>();
    }

    private static void EnsureLoaded(string tabName)
    {
        if (_cache.ContainsKey(tabName)) return;

        var file = tabName == "스킬" ? "SimulatorTableItemSkill.txt"
            : tabName == "시퀀스" ? "SimulatorTableItemSkillSequence.txt"
            : tabName == "에펙" ? "SimulatorTableItemSkillAffect.txt"
            : tabName == "컨디션" ? "SimulatorTableItemSkillCondition.txt"
            : string.Empty;

        var result = new Dictionary<ValueKind, HashSet<string>>
        {
            [ValueKind.Int] = new HashSet<string>(),
            [ValueKind.Float] = new HashSet<string>(),
            [ValueKind.String] = new HashSet<string>()
        };

        var baseDir = "/mnt/c/project/lsm/TestData";
        var path = Path.Combine(baseDir, file);
        if (!File.Exists(path))
        {
            _cache[tabName] = result;
            return;
        }

        var lines = File.ReadAllLines(path);
        ValueKind? current = null;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("public enum eDataInt")) { current = ValueKind.Int; continue; }
            if (line.StartsWith("public enum eDataFloat")) { current = ValueKind.Float; continue; }
            if (line.StartsWith("public enum eDataString")) { current = ValueKind.String; continue; }
            if (!current.HasValue) continue;
            if (line.StartsWith("}")) { current = null; continue; }
            if (line.StartsWith("//") || line.Length == 0) continue;

            var token = line.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(token)) continue;
            if (!char.IsLetter(token[0]) && token[0] != '_') continue;
            result[current.Value].Add(token);
        }

        _cache[tabName] = result;
    }
}

public class NodeTreeView : TreeView
{
    private readonly Func<List<TreeNodeData>> _source;
    private readonly Action<int> _onSelect;

    public NodeTreeView(TreeViewState state, Func<List<TreeNodeData>> source, Action<int> onSelect) : base(state)
    {
        _source = source;
        _onSelect = onSelect;
        Reload();
    }

    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
        var rows = new List<TreeViewItem>();

        foreach (var node in _source())
        {
            rows.Add(new TreeViewItem(node.Id, 0, $"[{node.Id}] {node.Name}"));
        }

        SetupParentsAndChildrenFromDepths(root, rows);
        return root;
    }

    protected override void SingleClickedItem(int id)
    {
        _onSelect?.Invoke(id);
    }
}

public abstract class DynamicTypeTabBase : ITabContent
{
    private int _selectedTypeIndex;
    private Vector2 _scroll;
    private TreeViewState _treeState;
    private NodeTreeView _treeView;
    private int _selectedNodeId;
    private TreeCategoryData _category;

    protected abstract string HeaderLabel { get; }
    protected abstract List<TypeSchema> Schemas { get; }
    public abstract string TabName { get; }

    public void OnEnable()
    {
        _selectedTypeIndex = 0;
        _category = SkillTreeStore.GetCategory(TabName);
        _treeState = _treeState ?? new TreeViewState();
        _treeView = new NodeTreeView(_treeState, () => _category.Nodes, id => _selectedNodeId = id);

        if (_category.Nodes.Count == 0 && Schemas.Count > 0)
        {
            _selectedNodeId = _category.CreateNode(Schemas[0]).Id;
            _treeView.Reload();
        }
        else if (_category.Nodes.Count > 0)
        {
            _selectedNodeId = _category.Nodes[0].Id;
        }
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
        if (GUILayout.Button("트리 노드 추가", GUILayout.Width(110)))
        {
            _selectedNodeId = _category.CreateNode(schema).Id;
            _treeView.Reload();
        }

        if (GUILayout.Button("트리 노드 삭제", GUILayout.Width(110)))
        {
            if (_category.RemoveNode(_selectedNodeId))
            {
                _selectedNodeId = _category.Nodes.Count > 0 ? _category.Nodes[0].Id : 0;
                _treeView.Reload();
            }
        }

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
            CurrentFields().Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
        }

        if (GUILayout.Button("JSON 가져오기", GUILayout.Width(100)))
        {
            if (SkillJsonIo.Import(TabName, _category, out var msg))
            {
                _selectedNodeId = _category.Nodes.Count > 0 ? _category.Nodes[0].Id : 0;
                _treeView.Reload();
                Debug.Log(msg);
            }
            else
            {
                Debug.LogError(msg);
            }
        }

        if (GUILayout.Button("JSON 내보내기", GUILayout.Width(100)))
        {
            if (SkillJsonIo.Export(TabName, _category, out var msg)) Debug.Log(msg);
            else Debug.LogError(msg);
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.BeginVertical(GUILayout.Width(280));
        var treeRect = GUILayoutUtility.GetRect(260, 420, GUILayout.ExpandHeight(true));
        _treeView.OnGUI(treeRect);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical();
        var node = _category.Find(_selectedNodeId);
        if (node == null)
        {
            EditorGUILayout.HelpBox("선택된 노드가 없습니다. 트리 노드를 추가하세요.", MessageType.Info);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            return;
        }

        node.Name = EditorGUILayout.TextField("노드명", node.Name ?? string.Empty);
        EditorGUILayout.LabelField($"노드 ID: {node.Id}", EditorStyles.miniLabel);

        DrawKeyGuide(schema);

        EditorGUILayout.Space(8);
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        var fields = CurrentFields();
        for (int i = 0; i < fields.Count; i++)
        {
            DrawFieldRow(schema, fields[i], i);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private List<DynamicField> CurrentFields()
    {
        var node = _category.Find(_selectedNodeId);
        if (node == null)
        {
            return new List<DynamicField>();
        }
        return node.Fields;
    }

    private void DrawKeyGuide(TypeSchema schema)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("사용 가능 키(타입별)", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField($"INT: {string.Join(", ", GetAllowedKeysByKind(schema, ValueKind.Int))}", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField($"FLOAT: {string.Join(", ", GetAllowedKeysByKind(schema, ValueKind.Float))}", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField($"STRING: {string.Join(", ", GetAllowedKeysByKind(schema, ValueKind.String))}", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
    }

    private void DrawFieldRow(TypeSchema schema, DynamicField field, int index)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        var fields = CurrentFields();
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

        var autoKind = ResolveKindByKey(schema, field.Key);
        if (autoKind.HasValue)
        {
            field.Kind = autoKind.Value;
        }

        field.Value = EditorGUILayout.TextField(field.Value ?? string.Empty);

        if (GUILayout.Button("삭제", GUILayout.Width(50)))
        {
            fields.RemoveAt(index);
            EditorGUILayout.EndHorizontal();
            return;
        }

        EditorGUILayout.EndHorizontal();

        var valueError = ValidateFieldValue(field);
        if (!string.IsNullOrEmpty(valueError))
        {
            EditorGUILayout.HelpBox(valueError, MessageType.Warning);
        }
    }

    private IEnumerable<string> GetAllowedKeysByKind(TypeSchema schema, ValueKind kind)
    {
        var schemaKeys = kind == ValueKind.Int ? schema.AllowedIntKeys
            : kind == ValueKind.Float ? schema.AllowedFloatKeys
            : schema.AllowedStringKeys;

        return schemaKeys
            .Concat(EnumKeyCatalog.GetKeys(TabName, kind))
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct()
            .OrderBy(k => k)
            .ToList();
    }

    private ValueKind? ResolveKindByKey(TypeSchema schema, string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (GetAllowedKeysByKind(schema, ValueKind.Int).Contains(key)) return ValueKind.Int;
        if (GetAllowedKeysByKind(schema, ValueKind.Float).Contains(key)) return ValueKind.Float;
        if (GetAllowedKeysByKind(schema, ValueKind.String).Contains(key)) return ValueKind.String;
        return null;
    }

    private string ValidateFieldValue(DynamicField field)
    {
        if (field.Kind == ValueKind.Int && !int.TryParse(field.Value, out _)) return $"{field.Key}: INT 값이 아닙니다.";
        if (field.Kind == ValueKind.Float && !double.TryParse(field.Value, out _)) return $"{field.Key}: FLOAT 값이 아닙니다.";
        return string.Empty;
    }

    private void ApplyPreset(TypeSchema schema)
    {
        var fields = CurrentFields();
        fields.Clear();
        foreach (var p in schema.DefaultPreset)
        {
            fields.Add(new DynamicField
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

        CurrentFields().Add(new DynamicField
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

public class SummaryTreeView : TreeView
{
    public SummaryTreeView(TreeViewState state) : base(state)
    {
        Reload();
    }

    protected override TreeViewItem BuildRoot()
    {
        var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
        var rows = new List<TreeViewItem>();
        var nextId = 1;

        var skillRoot = new TreeViewItem(nextId++, 0, "스킬");
        rows.Add(skillRoot);

        foreach (var skill in SkillTreeStore.Skill.Nodes)
        {
            var skillItem = new TreeViewItem(nextId++, 1, $"[{skill.Id}] {skill.Name}");
            rows.Add(skillItem);

            foreach (var seqId in ExtractIds(skill, "SEQUENCE_IDS", "SKILL_OPTIONS"))
            {
                var sequence = SkillTreeStore.Sequence.Find(seqId);
                if (sequence == null) continue;

                var sequenceItem = new TreeViewItem(nextId++, 2, $"시퀀스 [{sequence.Id}] {sequence.Name}");
                rows.Add(sequenceItem);

                foreach (var effId in ExtractIds(sequence, "CREATE_IDS"))
                {
                    var effect = SkillTreeStore.Effect.Find(effId);
                    if (effect == null) continue;

                    var effectItem = new TreeViewItem(nextId++, 3, $"에펙 [{effect.Id}] {effect.Name}");
                    rows.Add(effectItem);

                    foreach (var conId in ExtractIds(effect, "CONDITION_IDS"))
                    {
                        var condition = SkillTreeStore.Condition.Find(conId);
                        if (condition == null) continue;
                        rows.Add(new TreeViewItem(nextId++, 4, $"컨디션 [{condition.Id}] {condition.Name}"));
                    }
                }

                foreach (var conId in ExtractIds(sequence, "CONDITION_IDS"))
                {
                    var condition = SkillTreeStore.Condition.Find(conId);
                    if (condition == null) continue;
                    rows.Add(new TreeViewItem(nextId++, 3, $"컨디션 [{condition.Id}] {condition.Name}"));
                }
            }
        }

        SetupParentsAndChildrenFromDepths(root, rows);
        return root;
    }

    private static IEnumerable<int> ExtractIds(TreeNodeData node, params string[] keys)
    {
        foreach (var key in keys)
        {
            var field = node.Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
            if (field == null || string.IsNullOrWhiteSpace(field.Value))
            {
                continue;
            }

            var parts = field.Value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                if (int.TryParse(p.Trim(), out var id))
                {
                    yield return id;
                }
            }
        }
    }
}

public class SummaryTab : ITabContent
{
    public string TabName => "종합";
    private TreeViewState _treeState;
    private SummaryTreeView _treeView;

    public void OnEnable()
    {
        _treeState = _treeState ?? new TreeViewState();
        _treeView = new SummaryTreeView(_treeState);
    }

    public void OnGUI()
    {
        EditorGUILayout.LabelField("종합 트리(합본)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "스킬-시퀀스-에펙-컨디션 연결 구조를 TreeView로 표시합니다.\n" +
            "연결 키 예시: SKILL_OPTIONS/SEQUENCE_IDS, CREATE_IDS, CONDITION_IDS (ID는 쉼표 구분)",
            MessageType.Info);

        if (GUILayout.Button("트리 새로고침", GUILayout.Width(100)))
        {
            _treeView.Reload();
        }

        var treeRect = GUILayoutUtility.GetRect(200, 450, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        _treeView.OnGUI(treeRect);
    }
}
