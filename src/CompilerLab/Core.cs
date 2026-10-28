using System.Text;

namespace CompilerLab.Core;

/// <summary>语言中用到的特殊符号常量。</summary>
public static class Symbols
{
    public const string Epsilon = "ε";   // 空串
    public const string End = "#";       // 输入串结束符
    public const string Arrow = "→";
}

// =====================================================================
//                              一、词法分析
// =====================================================================

/// <summary>单词种类。</summary>
public enum TokenKind
{
    Keyword,        // 关键字
    Identifier,     // 标识符
    IntConst,       // 整型常量
    RealConst,      // 实型常量
    Operator,       // 运算符
    Delimiter       // 界符
}

/// <summary>一个单词符号。</summary>
public sealed class Token
{
    public int Index { get; set; }          // 序号
    public int Syn { get; set; }            // 种别码
    public string Lexeme { get; set; } = "";  // 单词符号本身
    public TokenKind Kind { get; set; }
    public int Line { get; set; }
    public int Column { get; set; }

    /// <summary>该单词对应的文法终结符。</summary>
    public string Terminal { get; set; } = "";

    public string KindText => Kind switch
    {
        TokenKind.Keyword => "关键字",
        TokenKind.Identifier => "标识符",
        TokenKind.IntConst => "整型常量",
        TokenKind.RealConst => "实型常量",
        TokenKind.Operator => "运算符",
        _ => "界符"
    };
}

/// <summary>词法分析结果。</summary>
public sealed class LexResult
{
    public List<Token> Tokens { get; } = new();
    public List<CompileError> Errors { get; } = new();

    /// <summary>输出指导书要求的二元组序列 (syn, token)。</summary>
    public string ToSequenceString()
        => Tokens.Count == 0 ? "" : string.Join(" ", Tokens.Select(t => $"({t.Syn},{t.Lexeme})"));
}

/// <summary>编译过程中产生的一条错误（词法/语法/语义）。</summary>
public sealed class CompileError
{
    public string Stage { get; set; } = "";      // 词法分析 / 语法分析 / 语义分析
    public int Line { get; set; }
    public int Column { get; set; }
    public string Message { get; set; } = "";
    public string Text => Line > 0 ? $"[{Stage}] 第 {Line} 行：" + Message : $"[{Stage}] " + Message;
}

/// <summary>
/// 词法分析器。扫描小语言源程序，输出 (种别码, 单词符号) 序列，
/// 并把每个单词映射到文法终结符（关系运算符统一映射为 relop）。
/// </summary>
public sealed class Lexer
{
    /// <summary>关键字 → 种别码。</summary>
    public static readonly Dictionary<string, int> Keywords = new()
    {
        ["int"] = 1, ["real"] = 2, ["if"] = 3, ["else"] = 4,
        ["while"] = 5, ["read"] = 6, ["write"] = 7
    };

    /// <summary>运算符与界符 → 种别码（按长度降序做最长匹配）。</summary>
    public static readonly Dictionary<string, int> Operators = new()
    {
        ["<="] = 42, [">="] = 44, ["=="] = 45, ["!="] = 46,
        ["="] = 20, ["+"] = 21, ["-"] = 22, ["*"] = 23, ["/"] = 24,
        ["("] = 25, [")"] = 26, ["{"] = 27, ["}"] = 28, [";"] = 29,
        ["<"] = 41, [">"] = 43
    };

    public const int SynId = 10;      // 标识符
    public const int SynNum = 11;     // 整型常量
    public const int SynRnum = 12;    // 实型常量

    /// <summary>关系运算符统一映射到文法终结符 relop。</summary>
    private static readonly HashSet<string> Relations = new() { "<", "<=", ">", ">=", "==", "!=" };

    private static readonly string[] OperatorByLength =
        Operators.Keys.OrderByDescending(k => k.Length).ToArray();

    private readonly string _src;
    private int _pos, _line = 1, _col = 1;

    public Lexer(string source) => _src = (source ?? "").Replace("\r\n", "\n").Replace('\r', '\n');

    public LexResult Analyze()
    {
        var result = new LexResult();
        int index = 1;

        while (true)
        {
            SkipBlankAndComment(result);
            if (_pos >= _src.Length) break;

            char c = _src[_pos];
            int line = _line, col = _col;

            if (char.IsLetter(c) || c == '_') ReadWord(result, ref index);
            else if (char.IsDigit(c)) ReadNumber(result, ref index);
            else if (!ReadOperator(result, ref index))
            {
                result.Errors.Add(new CompileError
                {
                    Stage = "词法分析",
                    Line = line,
                    Column = col,
                    Message = $"非法字符 '{c}'：它不是本语言中的合法符号。"
                });
                Advance();
            }
        }

        return result;
    }

    // ------------------------------------------------------------------

    private void Advance()
    {
        if (_pos < _src.Length)
        {
            if (_src[_pos] == '\n') { _line++; _col = 1; }
            else _col++;
            _pos++;
        }
    }

    /// <summary>跳过空白与两类注释（// 与 /* */）。</summary>
    private void SkipBlankAndComment(LexResult result)
    {
        while (_pos < _src.Length)
        {
            char c = _src[_pos];
            if (c == ' ' || c == '\t' || c == '\n') { Advance(); continue; }

            if (c == '/' && _pos + 1 < _src.Length && _src[_pos + 1] == '/')
            {
                while (_pos < _src.Length && _src[_pos] != '\n') Advance();
                continue;
            }
            if (c == '/' && _pos + 1 < _src.Length && _src[_pos + 1] == '*')
            {
                int line = _line, col = _col;
                Advance(); Advance();
                bool closed = false;
                while (_pos < _src.Length)
                {
                    if (_src[_pos] == '*' && _pos + 1 < _src.Length && _src[_pos + 1] == '/')
                    { Advance(); Advance(); closed = true; break; }
                    Advance();
                }
                if (!closed)
                    result.Errors.Add(new CompileError
                    {
                        Stage = "词法分析",
                        Line = line,
                        Column = col,
                        Message = "块注释 /* 一直未遇到结束标记 */。"
                    });
                continue;
            }
            break;
        }
    }

    private void ReadWord(LexResult result, ref int index)
    {
        int line = _line, col = _col;
        var sb = new StringBuilder();
        while (_pos < _src.Length && (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_'))
        {
            sb.Append(_src[_pos]);
            Advance();
        }
        string word = sb.ToString();

        var t = new Token { Index = index++, Lexeme = word, Line = line, Column = col };
        if (Keywords.TryGetValue(word, out int syn))
        {
            t.Syn = syn;
            t.Kind = TokenKind.Keyword;
            t.Terminal = word;
        }
        else
        {
            t.Syn = SynId;
            t.Kind = TokenKind.Identifier;
            t.Terminal = "id";
        }
        result.Tokens.Add(t);
    }

    /// <summary>数值常量：支持 12、12.5、12.、.5、1.2e+3/long 等形式。</summary>
    private void ReadNumber(LexResult result, ref int index)
    {
        int line = _line, col = _col;
        var sb = new StringBuilder();
        bool isReal = false;

        while (_pos < _src.Length && char.IsDigit(_src[_pos])) { sb.Append(_src[_pos]); Advance(); }

        if (_pos < _src.Length && _src[_pos] == '.')
        {
            isReal = true;
            sb.Append('.');
            Advance();
            while (_pos < _src.Length && char.IsDigit(_src[_pos])) { sb.Append(_src[_pos]); Advance(); }
        }

        if (_pos < _src.Length && (_src[_pos] == 'e' || _src[_pos] == 'E'))
        {
            int save = _pos, saveLine = _line, saveCol = _col;
            var exp = new StringBuilder("e");
            Advance();
            if (_pos < _src.Length && (_src[_pos] == '+' || _src[_pos] == '-')) { exp.Append(_src[_pos]); Advance(); }
            if (_pos < _src.Length && char.IsDigit(_src[_pos]))
            {
                while (_pos < _src.Length && char.IsDigit(_src[_pos])) { exp.Append(_src[_pos]); Advance(); }
                isReal = true;
                sb.Append(exp);
            }
            else { _pos = save; _line = saveLine; _col = saveCol; }
        }

        string text = sb.ToString();
        var t = new Token
        {
            Index = index++,
            Lexeme = text,
            Line = line,
            Column = col,
            Kind = isReal ? TokenKind.RealConst : TokenKind.IntConst,
            Syn = isReal ? SynRnum : SynNum,
            Terminal = isReal ? "rnum" : "num"
        };
        result.Tokens.Add(t);

        // 数字后面紧跟字母或下划线属于非法数值
        if (_pos < _src.Length && (char.IsLetter(_src[_pos]) || _src[_pos] == '_'))
        {
            var bad = new StringBuilder();
            while (_pos < _src.Length && (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_'))
            { bad.Append(_src[_pos]); Advance(); }
            result.Errors.Add(new CompileError
            {
                Stage = "词法分析",
                Line = line,
                Column = col,
                Message = $"无效的数值常量“{text}{bad}”：数字后面不能紧跟字母或下划线。"
            });
        }
    }

    private bool ReadOperator(LexResult result, ref int index)
    {
        int line = _line, col = _col;
        foreach (string op in OperatorByLength)
        {
            if (_pos + op.Length > _src.Length) continue;
            if (string.CompareOrdinal(_src, _pos, op, 0, op.Length) != 0) continue;

            for (int k = 0; k < op.Length; k++) Advance();
            result.Tokens.Add(new Token
            {
                Index = index++,
                Lexeme = op,
                Line = line,
                Column = col,
                Syn = Operators[op],
                Kind = "(){};".Contains(op) ? TokenKind.Delimiter : TokenKind.Operator,
                Terminal = Relations.Contains(op) ? "relop" : op
            });
            return true;
        }
        return false;
    }
}

// =====================================================================
//                       二、文法（四元组）与其解析
// =====================================================================

/// <summary>一条产生式 A → X1X2…Xn（右部为空表示 A → ε）。</summary>
public sealed class Production
{
    public int Index { get; set; }
    public string Left { get; set; } = "";
    public List<string> Right { get; set; } = new();

    /// <summary>语义动作的匹配键，形如 “expr->expr + term”。</summary>
    public string Key { get; set; } = "";

    public bool IsEpsilon => Right.Count == 0;
    public string RightText => IsEpsilon ? Symbols.Epsilon : string.Join(" ", Right);
    public string Text => $"{Left}{Symbols.Arrow}{RightText}";
}

/// <summary>文法 G = (Vn, Vt, P, S)，即“四元组”。</summary>
public sealed class Grammar
{
    public List<string> NonTerminals { get; set; } = new();
    public List<string> Terminals { get; set; } = new();
    public List<Production> Productions { get; set; } = new();
    public string Start { get; set; } = "";

    public string Quadruple()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Vn = {{ {string.Join(", ", NonTerminals)} }}");
        sb.AppendLine($"Vt = {{ {string.Join(", ", Terminals)} }}");
        sb.AppendLine($"S  = {Start}");
        sb.Append("P  = { ");
        sb.Append(string.Join(" ； ", Productions.Select(p => p.Text)));
        sb.Append(" }");
        return sb.ToString();
    }

    public IEnumerable<Production> ProductionsOf(string nt) => Productions.Where(p => p.Left == nt);
    public bool IsNonTerminal(string s) => NonTerminals.Contains(s);

    public void Renumber()
    {
        for (int i = 0; i < Productions.Count; i++) Productions[i].Index = i;
    }

    /// <summary>按非终结符顺序整理产生式，便于阅读（稳定排序）。</summary>
    public void SortProductions()
    {
        Productions = Productions
            .Select((p, i) => (p, i))
            .OrderBy(x => NonTerminals.IndexOf(x.p.Left) is var k && k >= 0 ? k : int.MaxValue)
            .ThenBy(x => x.i)
            .Select(x => x.p)
            .ToList();
        Renumber();
    }

    /// <summary>从文本解析文法：每行一条产生式，右部可用 | 分隔多个候选式。</summary>
    public static Grammar Parse(string text, out List<string> errors)
    {
        errors = new List<string>();
        var g = new Grammar();
        var raw = new List<(string Left, List<string> Alts, bool Spaced)>();

        foreach (string rawLine in (text ?? "").Replace("\r\n", "\n").Split('\n'))
        {
            string line = rawLine;
            int cm = line.IndexOf("//", StringComparison.Ordinal);
            if (cm >= 0) line = line[..cm];
            line = line.Trim();
            if (line.Length == 0) continue;

            int arrow = line.IndexOf("->", StringComparison.Ordinal);
            int arrowLen = 2;
            if (arrow < 0) { arrow = line.IndexOf(Symbols.Arrow, StringComparison.Ordinal); arrowLen = 1; }
            if (arrow <= 0)
            {
                errors.Add($"无法识别的产生式（缺少 → 或 ->）：{line}");
                continue;
            }

            string lhs = line[..arrow].Trim();
            string rhsAll = line[(arrow + arrowLen)..];
            if (lhs.Length == 0)
            {
                errors.Add($"产生式左部为空：{line}");
                continue;
            }
            // 只要该行右部出现过空白，就整行按“空白分隔”切分符号，
            // 这样 id、num、rnum 这类多字符终结符能够整体识别。
            bool spaced = rhsAll.Any(char.IsWhiteSpace);
            raw.Add((lhs, rhsAll.Split('|').Select(s => s.Trim()).ToList(), spaced));
        }

        foreach (var (lhs, _, _) in raw)
            if (!g.NonTerminals.Contains(lhs)) g.NonTerminals.Add(lhs);

        if (g.NonTerminals.Count == 0)
        {
            errors.Add("文法为空。");
            return g;
        }

        foreach (var (lhs, alts, spaced) in raw)
            foreach (string alt in alts)
                g.Productions.Add(new Production
                {
                    Left = lhs,
                    Right = Tokenize(alt, g.NonTerminals, spaced)
                });

        g.Start = g.NonTerminals[0];

        foreach (var p in g.Productions)
            foreach (string s in p.Right)
                if (!g.NonTerminals.Contains(s) && !g.Terminals.Contains(s)) g.Terminals.Add(s);

        foreach (var p in g.Productions) p.Key = BuildKey(p);
        g.Renumber();
        return g;
    }

    /// <summary>语义动作的匹配键：左部 + 右部符号序列。</summary>
    public static string BuildKey(Production p)
        => p.Left + "->" + (p.IsEpsilon ? Symbols.Epsilon : string.Join(" ", p.Right));

    /// <summary>
    /// 把一条候选式切成符号序列。
    /// forceSpaced 为 true（或该串自身含空白）时按空白切分，否则按
    /// “优先匹配已声明的非终结符、否则单字符”切分，并吸收紧跟的撇号。
    /// </summary>
    public static List<string> Tokenize(string rhs, List<string> nonTerminals, bool forceSpaced)
    {
        var tokens = new List<string>();
        string s = (rhs ?? "").Trim();
        if (s.Length == 0) return tokens;

        if (forceSpaced || s.Any(char.IsWhiteSpace))
        {
            foreach (string t in s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                if (!IsEpsilonToken(t)) tokens.Add(t);
            return tokens;
        }

        int i = 0;
        while (i < s.Length)
        {
            string? matched = null;
            foreach (string nt in nonTerminals)
                if (nt.Length > 0 && i + nt.Length <= s.Length &&
                    string.CompareOrdinal(s, i, nt, 0, nt.Length) == 0 &&
                    (matched is null || nt.Length > matched.Length))
                    matched = nt;

            string sym;
            if (matched is not null) { sym = matched; i += matched.Length; }
            else { sym = s[i].ToString(); i++; }

            while (i < s.Length && s[i] == '\'') { sym += "'"; i++; }

            if (!IsEpsilonToken(sym)) tokens.Add(sym);
        }
        return tokens;
    }

    private static bool IsEpsilonToken(string t)
        => t is "ε" or "epsilon" or "EPSILON" or "Epsilon" or "@" or "空" or "空串";
}

// =====================================================================
//                         三、FIRST / FOLLOW 集
// =====================================================================

/// <summary>文法的 FIRST、FOLLOW 集（SLR(1) 分析表需要 FOLLOW）。</summary>
public sealed class GrammarSets
{
    public Grammar Grammar { get; }
    public Dictionary<string, SortedSet<string>> First { get; } = new();
    public Dictionary<string, SortedSet<string>> Follow { get; } = new();

    public GrammarSets(Grammar g)
    {
        Grammar = g;
        ComputeFirst();
        ComputeFollow();
    }

    /// <summary>符号串的 FIRST 集（可空时结果中含 ε）。</summary>
    public SortedSet<string> FirstOf(IEnumerable<string> symbols)
    {
        var result = new SortedSet<string>();
        foreach (string s in symbols)
        {
            if (Grammar.Terminals.Contains(s)) { result.Add(s); return result; }
            if (!First.TryGetValue(s, out var f)) { result.Add(s); return result; }

            foreach (string x in f) if (x != Symbols.Epsilon) result.Add(x);
            if (!f.Contains(Symbols.Epsilon)) return result;
        }
        result.Add(Symbols.Epsilon);
        return result;
    }

    private void ComputeFirst()
    {
        foreach (string t in Grammar.Terminals) First[t] = new SortedSet<string> { t };
        foreach (string nt in Grammar.NonTerminals) First[nt] = new SortedSet<string>();

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var p in Grammar.Productions)
                foreach (string x in FirstOf(p.Right))
                    if (First[p.Left].Add(x)) changed = true;
        }
    }

    private void ComputeFollow()
    {
        foreach (string nt in Grammar.NonTerminals) Follow[nt] = new SortedSet<string>();
        if (Grammar.NonTerminals.Contains(Grammar.Start)) Follow[Grammar.Start].Add(Symbols.End);

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var p in Grammar.Productions)
                for (int i = 0; i < p.Right.Count; i++)
                {
                    string b = p.Right[i];
                    if (!Grammar.NonTerminals.Contains(b)) continue;

                    var beta = FirstOf(p.Right.Skip(i + 1));
                    foreach (string x in beta)
                        if (x != Symbols.Epsilon && Follow[b].Add(x)) changed = true;

                    if (beta.Contains(Symbols.Epsilon))
                        foreach (string x in Follow[p.Left].ToList())
                            if (Follow[b].Add(x)) changed = true;
                }
        }
    }
}

// =====================================================================
//                     四、LR(0) 项目集规范族与 SLR(1) 分析表
// =====================================================================

/// <summary>LR 项目 [A → α·β]，用“产生式编号 + 圆点位置”唯一确定。</summary>
public sealed class LrItem : IEquatable<LrItem>, IComparable<LrItem>
{
    public int Production { get; }
    public int Dot { get; }
    public LrItem(int production, int dot) { Production = production; Dot = dot; }

    public bool Equals(LrItem? other) => other is not null && Production == other.Production && Dot == other.Dot;
    public override bool Equals(object? obj) => Equals(obj as LrItem);
    public override int GetHashCode() => Production * 1000 + Dot;
    public int CompareTo(LrItem? other) => other is null ? 1 : (Production, Dot).CompareTo((other.Production, other.Dot));
}

/// <summary>一个项目集（状态）。</summary>
public sealed class LrState
{
    public int Id { get; set; }
    public List<LrItem> Items { get; set; } = new();
    public string Key { get; set; } = "";

    public static string RenderItem(Grammar g, LrItem it)
    {
        var p = g.Productions[it.Production];
        var rhs = new List<string>(p.Right);
        rhs.Insert(Math.Clamp(it.Dot, 0, rhs.Count), "·");
        return $"{p.Left}{Symbols.Arrow}{string.Join(" ", rhs)}";
    }

    public string Render(Grammar g) => string.Join("  ", Items.Select(it => RenderItem(g, it)));
}

/// <summary>通用二维分析表。</summary>
public sealed class ParseTable
{
    public string Title { get; set; } = "";
    public List<string> Columns { get; } = new();
    public List<string> Rows { get; } = new();
    public List<string[]> Cells { get; } = new();
}

/// <summary>SLR(1) 分析所需的全部表格数据。</summary>
public sealed class SlrResult
{
    public Grammar Augmented { get; set; } = new();
    public string AugmentedStart { get; set; } = "";
    public GrammarSets Sets { get; set; } = null!;
    public List<LrState> States { get; } = new();

    public Dictionary<(int, string), string> Action { get; } = new();
    public Dictionary<(int, string), int> Goto { get; } = new();
    /// <summary>项目集之间的全部转移（与冲突无关），用于画状态转换图。</summary>
    public Dictionary<(int, string), int> Transitions { get; } = new();

    public ParseTable Table { get; set; } = new();
    public List<string> Conflicts { get; } = new();
    public bool IsSlr => Conflicts.Count == 0;
}

/// <summary>
/// SLR(1) 语法分析表构造器：求 LR(0) 项目集规范族，再用 FOLLOW 集
/// 决定归约项填写范围，从而得到 SLR(1) 的 ACTION / GOTO 表。
/// </summary>
public static class SlrTableBuilder
{
    public static SlrResult Build(Grammar input)
    {
        var r = new SlrResult();

        // ① 拓广文法：加入 S′ → S
        var g = new Grammar
        {
            NonTerminals = new List<string>(input.NonTerminals),
            Terminals = new List<string>(input.Terminals),
            Productions = input.Productions.Select(p => new Production
            {
                Index = p.Index,
                Left = p.Left,
                Right = new List<string>(p.Right),
                Key = p.Key
            }).ToList(),
            Start = input.Start
        };

        string newStart = "S′";
        while (g.NonTerminals.Contains(newStart)) newStart += "′";
        g.NonTerminals.Insert(0, newStart);
        g.Productions.Insert(0, new Production
        {
            Left = newStart,
            Right = new List<string> { g.Start }
        });
        g.Start = newStart;
        foreach (var p in g.Productions) p.Key = Grammar.BuildKey(p);
        g.SortProductions();
        g.Renumber();

        r.Augmented = g;
        r.AugmentedStart = newStart;
        r.Sets = new GrammarSets(g);

        BuildCanonicalCollection(r);
        BuildTables(r, g.Terminals.Concat(new[] { Symbols.End }).Distinct().ToList());
        return r;
    }

    // ------------------------------------------------------------------

    private static string KeyOf(IEnumerable<LrItem> items)
        => string.Join(",", items.OrderBy(i => i.Production).ThenBy(i => i.Dot)
                                 .Select(i => $"{i.Production}.{i.Dot}"));

    /// <summary>CLOSURE(I)：对每个 A→α·Bβ，把 B 的所有产生式 B→·γ 加入。</summary>
    private static List<LrItem> Closure(Grammar g, IEnumerable<LrItem> seed)
    {
        var set = new SortedSet<LrItem>(seed);
        var queue = new Queue<LrItem>(set);
        while (queue.Count > 0)
        {
            var it = queue.Dequeue();
            var p = g.Productions[it.Production];
            if (it.Dot >= p.Right.Count) continue;
            string b = p.Right[it.Dot];
            if (!g.NonTerminals.Contains(b)) continue;
            foreach (var q in g.ProductionsOf(b))
            {
                var n = new LrItem(q.Index, 0);
                if (set.Add(n)) queue.Enqueue(n);
            }
        }
        return set.ToList();
    }

    /// <summary>GOTO(I, X)：把圆点越过 X 的项目求闭包。</summary>
    private static List<LrItem> Goto(Grammar g, List<LrItem> items, string x)
    {
        var moved = new List<LrItem>();
        foreach (var it in items)
        {
            var p = g.Productions[it.Production];
            if (it.Dot < p.Right.Count && p.Right[it.Dot] == x)
                moved.Add(new LrItem(it.Production, it.Dot + 1));
        }
        return moved.Count == 0 ? new List<LrItem>() : Closure(g, moved);
    }

    private static void BuildCanonicalCollection(SlrResult r)
    {
        var g = r.Augmented;
        var index = new Dictionary<string, int>();
        var queue = new Queue<LrState>();

        LrState Add(IEnumerable<LrItem> items)
        {
            var list = items.ToList();
            string key = KeyOf(list);
            if (index.TryGetValue(key, out int exist)) return r.States[exist];

            var st = new LrState { Id = r.States.Count, Items = list, Key = key };
            r.States.Add(st);
            index[key] = st.Id;
            queue.Enqueue(st);
            return st;
        }

        Add(Closure(g, new[] { new LrItem(0, 0) }));      // 初态 CLOSURE({S′→·S})

        var symbols = g.Terminals.Concat(g.NonTerminals).Distinct().ToList();
        while (queue.Count > 0)
        {
            var st = queue.Dequeue();
            foreach (string x in symbols)
            {
                var next = Goto(g, st.Items, x);
                if (next.Count == 0) continue;
                r.Transitions[(st.Id, x)] = Add(next).Id;
            }
        }
    }

    private static void BuildTables(SlrResult r, List<string> allTerminals)
    {
        var g = r.Augmented;

        // GOTO：圆点后是非终结符的项目
        foreach (var st in r.States)
            foreach (var it in st.Items)
            {
                var p = g.Productions[it.Production];
                if (it.Dot >= p.Right.Count) continue;
                string x = p.Right[it.Dot];
                if (g.NonTerminals.Contains(x) && r.Transitions.TryGetValue((st.Id, x), out int to))
                    r.Goto[(st.Id, x)] = to;
            }

        void Put(int state, string symbol, string value)
        {
            var key = (state, symbol);
            if (r.Action.TryGetValue(key, out string? old))
            {
                if (old != value) r.Conflicts.Add(Describe(state, symbol, old, value));
                return;                                  // 冲突时保留先填入的动作（移进优先）
            }
            r.Action[key] = value;
        }

        foreach (var st in r.States)
            foreach (var it in st.Items)
            {
                var p = g.Productions[it.Production];

                // 圆点后面还有符号 → 移进
                if (it.Dot < p.Right.Count)
                {
                    string x = p.Right[it.Dot];
                    if (g.Terminals.Contains(x) && r.Transitions.TryGetValue((st.Id, x), out int to))
                        Put(st.Id, x, "s" + to);
                    continue;
                }

                // 完整项目 [A → α·] → 归约（或接受）
                if (p.Left == r.AugmentedStart) { Put(st.Id, Symbols.End, "acc"); continue; }
                foreach (string a in r.Sets.Follow[p.Left]) Put(st.Id, a, "r" + it.Production);
            }

        // 组织成可显示的二维表：列 = 终结符(含 #) + 非终结符（GOTO 部分）
        var nonTerminalCols = g.NonTerminals.Where(n => n != r.AugmentedStart).ToList();
        var table = new ParseTable { Title = "SLR(1) 语法分析表（ACTION + GOTO）" };
        table.Columns.AddRange(allTerminals);
        table.Columns.AddRange(nonTerminalCols);

        foreach (var st in r.States)
        {
            table.Rows.Add(st.Id.ToString());
            var row = new string[table.Columns.Count];
            for (int c = 0; c < table.Columns.Count; c++)
            {
                string sym = table.Columns[c];
                if (r.Action.TryGetValue((st.Id, sym), out string? v)) row[c] = v;
                else if (r.Goto.TryGetValue((st.Id, sym), out int gt)) row[c] = gt.ToString();
            }
            table.Cells.Add(row);
        }
        r.Table = table;
    }

    private static string Describe(int state, string symbol, string oldValue, string newValue)
    {
        bool IsR(string v) => v.StartsWith("r");
        bool IsS(string v) => v.StartsWith("s");

        string kind =
            (IsS(oldValue) && IsR(newValue)) || (IsR(oldValue) && IsS(newValue)) ? "移进-归约冲突"
            : IsR(oldValue) && IsR(newValue) ? "归约-归约冲突"
            : "冲突";

        return $"状态 {state}，输入 {symbol}：{kind}（{oldValue} 与 {newValue}）";
    }
}

// =====================================================================
//                     五、小语言的定义（文法四元组）
// =====================================================================

/// <summary>
/// 本实验所定义的小语言。语句体统一用 { } 括起来，因此 if/else 不存在
/// 二义性；条件中只使用关系运算符（&& 与 || 会使文法不再是 SLR(1)）。
/// 该文法经程序验证为无冲突的 SLR(1) 文法。
/// </summary>
public static class Language
{
    public const string GrammarText =
        "prog→blk\n" +
        "blk→{ decls stmts }\n" +
        "decls→decls decl|ε\n" +
        "decl→int id ;|real id ;\n" +
        "stmts→stmts stmt|ε\n" +
        "stmt→id = expr ;\n" +
        "stmt→if ( cond ) blk\n" +
        "stmt→if ( cond ) blk else blk\n" +
        "stmt→while ( cond ) blk\n" +
        "stmt→blk\n" +
        "stmt→read ( id ) ;\n" +
        "stmt→write ( expr ) ;\n" +
        "cond→expr relop expr|expr\n" +
        "expr→expr + term|expr - term|term\n" +
        "term→term * factor|term / factor|factor\n" +
        "factor→( expr )|id|num|rnum|- factor\n";

    public static Grammar Create() => Grammar.Parse(GrammarText, out _);
}

// =====================================================================
//                   六、语法制导翻译：属性、符号表、四元式
// =====================================================================

/// <summary>值的类型。</summary>
public enum VarType { Void, Int, Real, Bool, Error }

public static class VarTypes
{
    public static string Text(this VarType t) => t switch
    {
        VarType.Int => "int",
        VarType.Real => "real",
        VarType.Bool => "bool",
        VarType.Error => "错误",
        _ => "—"
    };

    /// <summary>分配一个存储单元所需的字节数（示意：整型 2 字节，实型 4 字节）。</summary>
    public static int Size(this VarType t) => t == VarType.Real ? 4 : 2;

    /// <summary>算术运算的类型提升：只要有一个是实型，结果就是实型。</summary>
    public static VarType Promote(VarType a, VarType b)
    {
        if (a == VarType.Error || b == VarType.Error) return VarType.Error;
        if (a == VarType.Real || b == VarType.Real) return VarType.Real;
        return VarType.Int;
    }

    public static bool IsNumeric(this VarType t) => t is VarType.Int or VarType.Real;
}

/// <summary>一条四元式 (op, arg1, arg2, result)。</summary>
public sealed class Quad
{
    public int Index { get; set; }
    public string Op { get; set; } = "";
    public string Arg1 { get; set; } = "";
    public string Arg2 { get; set; } = "";
    public string Result { get; set; } = "";

    public string Text => $"({Op}, {Arg1}, {Arg2}, {Result})";

    /// <summary>四元式的含义说明（便于阅读与写报告）。</summary>
    public string Meaning => Op switch
    {
        "=" => $"{Result} := {Arg1}",
        "neg" => $"{Result} := -{Arg1}",
        "jz" => $"若 {Arg1} 为假（0）则转到 {Result}",
        "j" => $"无条件转到 {Result}",
        "label" => $"标号 {Result}：",
        "read" => $"读入 {Result}",
        "write" => $"输出 {Arg1}",
        "+" or "-" or "*" or "/" => $"{Result} := {Arg1} {Op} {Arg2}",
        "<" or "<=" or ">" or ">=" or "==" or "!=" => $"{Result} := {Arg1} {Op} {Arg2}",
        _ => ""
    };
}

/// <summary>符号表中的一个名字。</summary>
public sealed class Symbol
{
    public string Name { get; set; } = "";

    /// <summary>
    /// 存储名。没有重名时与源程序中的名字相同；当内层块声明了与外层同名的变量时，
    /// 用“名字$层次”加以区分，四元式里引用的是存储名。
    /// </summary>
    public string Store { get; set; } = "";

    public VarType Type { get; set; }
    public int Address { get; set; }
    public int ScopeLevel { get; set; }
    public int Line { get; set; }
    public bool IsTemp { get; set; }

    public string Display => Store == Name || IsTemp ? Name : $"{Name}（第 {ScopeLevel} 层）";
}

/// <summary>一个作用域（一个 { } 块）。</summary>
public sealed class ScopeInfo
{
    public int Level { get; set; }
    public string Kind { get; set; } = "";
    public int Line { get; set; }
    public List<Symbol> Symbols { get; } = new();
}

/// <summary>语法制导翻译过程中每个文法符号的属性。</summary>
internal sealed class Attr
{
    public string Place { get; set; } = "";     // 值所在处：常量、变量名或临时变量名
    public VarType Type { get; set; } = VarType.Void;
    public string Name { get; set; } = "";      // 标识符名
    public string Op { get; set; } = "";        // 实际的关系运算符
    public int Line { get; set; }
    public List<Quad> Code { get; set; } = new();
}

/// <summary>语法分析过程中的一行。</summary>
public sealed class AnalysisStep
{
    public int Index { get; set; }
    public string StateStack { get; set; } = "";
    public string SymbolStack { get; set; } = "";
    public string CurrentInput { get; set; } = "";
    public string Action { get; set; } = "";
}

/// <summary>一次编译的完整结果。</summary>
public sealed class CompileResult
{
    public string Source { get; set; } = "";
    public LexResult Lex { get; set; } = new();
    public Grammar Grammar { get; set; } = new();
    public SlrResult Slr { get; set; } = new();

    public List<AnalysisStep> Process { get; } = new();
    public bool SyntaxAccepted { get; set; }
    public string SyntaxResult { get; set; } = "";

    public List<ScopeInfo> Scopes { get; } = new();
    public List<Symbol> Symbols { get; } = new();
    public List<CompileError> Errors { get; } = new();
    public List<Quad> Quads { get; } = new();

    /// <summary>局部优化后的四元式（解释执行使用这一份）。</summary>
    public List<Quad> OptimizedQuads { get; } = new();

    /// <summary>优化过程说明，便于在界面与报告中展示。</summary>
    public List<string> OptimizeLog { get; } = new();

    public Dictionary<string, VarType> VarTypes { get; } = new();

    public RunResult? Run { get; set; }

    public bool Ok => Errors.Count == 0;
    public IEnumerable<CompileError> AllErrors => Lex.Errors.Concat(Errors);
}

/// <summary>
/// 编译器：词法分析 → 语法分析（SLR(1) + 语法制导翻译）→ 语义检查
/// → 四元式中间代码生成。四元式的生成在归约时完成，各子树的代码段
/// 在父结点归约时按语序拼接，因此标号与跳转天然有序，不需要回填。
/// </summary>
public static class Compiler
{
    public static CompileResult Compile(string source)
    {
        var r = new CompileResult { Source = source };

        // ------------------------- ① 词法分析 -------------------------
        r.Lex = new Lexer(source).Analyze();

        // ------------------------- ② 文法与 SLR(1) 分析表 -------------------------
        var g = Language.Create();
        r.Grammar = g;
        r.Slr = SlrTableBuilder.Build(g);

        // --------------------- ③ 语法分析 + 语法制导翻译 ---------------------
        var ctx = new Context(r, g);
        Parse(r, g, r.Slr, ctx);

        // ------------------------- ④ 局部优化 -------------------------
        r.OptimizedQuads.AddRange(Optimizer.Optimize(r.Quads, r.OptimizeLog));

        // ------------------------- ⑤ 收尾 -------------------------
        for (int i = 0; i < r.Quads.Count; i++) r.Quads[i].Index = i + 1;
        r.Symbols.AddRange(ctx.AllSymbols);
        foreach (var s in ctx.AllSymbols) r.VarTypes[s.Store] = s.Type;
        return r;
    }

    // ------------------------------------------------------------------

    /// <summary>翻译过程中用到的临时量计数器、标号计数器与符号表。</summary>
    private sealed class Context
    {
        public readonly CompileResult R;
        public readonly Grammar G;
        public readonly List<ScopeInfo> ScopeStack = new();
        public readonly List<Symbol> AllSymbols = new();
        public int TempCount, LabelCount, NextAddress = 1;

        public Context(CompileResult r, Grammar g) { R = r; G = g; }

        public int NewTemp(VarType t)
        {
            TempCount++;
            string name = "t" + TempCount;
            R.VarTypes[name] = t;
            AllSymbols.Add(new Symbol
            {
                Name = name, Store = name, Type = t, Address = NextAddress, ScopeLevel = -1,
                IsTemp = true
            });
            NextAddress += t.Size();
            return TempCount;
        }

        public string NewLabel() => "L" + (++LabelCount);

        public void EnterScope(string kind, int line)
        {
            var s = new ScopeInfo { Level = ScopeStack.Count, Kind = kind, Line = line };
            ScopeStack.Add(s);
            R.Scopes.Add(s);
        }

        public void ExitScope() { if (ScopeStack.Count > 0) ScopeStack.RemoveAt(ScopeStack.Count - 1); }

        public Symbol? Lookup(string name)
        {
            for (int i = ScopeStack.Count - 1; i >= 0; i--)
            {
                var found = ScopeStack[i].Symbols.FirstOrDefault(s => s.Name == name);
                if (found is not null) return found;
            }
            return null;
        }

        public void Declare(string name, VarType type, int line)
        {
            if (ScopeStack.Count == 0) EnterScope("程序", 1);
            var scope = ScopeStack[^1];

            if (scope.Symbols.Any(s => s.Name == name))
            {
                Error(line, $"标识符“{name}”在当前作用域中重复声明。");
                return;
            }
            var sym = new Symbol
            {
                Name = name,
                Store = StoreNameFor(name, scope.Level),
                Type = type,
                Address = NextAddress,
                ScopeLevel = scope.Level,
                Line = line
            };
            NextAddress += type.Size();
            scope.Symbols.Add(sym);
            AllSymbols.Add(sym);
        }

        /// <summary>生成存储名：与任何外层名字都不冲突时就用原名，否则加“$层次”后缀。</summary>
        private string StoreNameFor(string name, int level)
        {
            bool clash = AllSymbols.Any(s => s.Store == name);
            if (!clash) return name;
            string candidate = $"{name}${level}";
            while (AllSymbols.Any(s => s.Store == candidate)) candidate += "'";
            return candidate;
        }

        public void Error(int line, string message)
            => R.Errors.Add(new CompileError { Stage = "语义分析", Line = line, Message = message });

        public void SyntaxError(int line, int col, string message)
            => R.Errors.Add(new CompileError { Stage = "语法分析", Line = line, Column = col, Message = message });
    }

    // ------------------------------------------------------------------
    //                          分析主循环
    // ------------------------------------------------------------------

    private static void Parse(CompileResult r, Grammar g, SlrResult slr, Context ctx)
    {
        var input = r.Lex.Tokens.ToList();
        int lastLine = input.Count > 0 ? input[^1].Line : 1;
        input.Add(new Token { Lexeme = "#", Terminal = Symbols.End, Syn = 0, Line = lastLine, Column = 0 });

        var states = new List<int> { 0 };
        var symbols = new List<string> { Symbols.End };
        var attrs = new List<Attr> { new() { Type = VarType.Void } };
        int pos = 0, step = 0;

        while (true)
        {
            if (++step > 200000)
            {
                r.SyntaxResult = "分析步数过多，已中止。";
                return;
            }

            int s = states[^1];
            Token cur = input[pos];

            var st = new AnalysisStep
            {
                Index = step,
                StateStack = string.Join(" ", states),
                SymbolStack = string.Join(" ", symbols),
                CurrentInput = cur.Lexeme
            };

            if (!slr.Action.TryGetValue((s, cur.Terminal), out string? act))
            {
                st.Action = "出错";
                r.Process.Add(st);
                r.SyntaxAccepted = false;
                ctx.SyntaxError(cur.Line, cur.Column,
                    $"遇到 “{cur.Lexeme}” 时无法继续分析（状态 {s} 的分析表项为空）。");
                r.SyntaxResult = $"分析失败：第 {cur.Line} 行第 {cur.Column} 列附近的 “{cur.Lexeme}” 不符合语法。";
                return;
            }

            if (act == "acc")
            {
                st.Action = "接受";
                r.Process.Add(st);
                r.SyntaxAccepted = true;
                r.SyntaxResult = "分析成功：源程序符合文法。";
                return;
            }

            if (act[0] == 's')
            {
                int to = int.Parse(act[1..]);
                st.Action = $"移进 “{cur.Lexeme}”，转到状态 {to}";
                r.Process.Add(st);
                states.Add(to);
                symbols.Add(cur.Terminal);
                attrs.Add(AttrOfToken(cur, ctx));
                if (cur.Lexeme == "{") ctx.EnterScope("复合语句", cur.Line);
                pos++;
                continue;
            }

            // 注意：分析表中的归约编号对应“拓广文法”的产生式编号（前面多了一条 S′→S），
            // 因此必须用 slr.Augmented 而不是原文法来取产生式。
            var p = slr.Augmented.Productions[int.Parse(act[1..])];
            st.Action = "归约 " + p.Text;
            r.Process.Add(st);

            int n = p.Right.Count;
            var rhs = new List<Attr>();
            for (int k = 0; k < n; k++)
            {
                rhs.Insert(0, attrs[^1]);
                attrs.RemoveAt(attrs.Count - 1);
                symbols.RemoveAt(symbols.Count - 1);
                states.RemoveAt(states.Count - 1);
            }

            Attr a = Reduce(ctx, p, rhs);

            if (p.Key == "blk->{ decls stmts }") ctx.ExitScope();

            if (p.Key == "prog->blk")        // 整个程序归约完，取出四元式序列
            {
                r.Quads.Clear();
                r.Quads.AddRange(a.Code);
            }

            if (!slr.Goto.TryGetValue((states[^1], p.Left), out int gt))
            {
                ctx.SyntaxError(cur.Line, cur.Column,
                    $"归约 {p.Text} 后 GOTO[{states[^1]}, {p.Left}] 为空。");
                r.SyntaxResult = "分析失败：状态转移表不完整。";
                return;
            }

            states.Add(gt);
            symbols.Add(p.Left);
            attrs.Add(a);
        }
    }

    /// <summary>终结符的属性：常量带值，标识符带名字，关系运算符带符号本身。</summary>
    private static Attr AttrOfToken(Token t, Context ctx)
    {
        var a = new Attr { Line = t.Line };
        switch (t.Terminal)
        {
            case "id":
                a.Name = t.Lexeme;
                a.Place = t.Lexeme;
                break;
            case "num":
                a.Place = t.Lexeme;
                a.Type = VarType.Int;
                break;
            case "rnum":
                a.Place = t.Lexeme;
                a.Type = VarType.Real;
                break;
            case "relop":
                a.Op = t.Lexeme;
                break;
        }
        return a;
    }

    // ------------------------------------------------------------------
    //                        语义动作（按产生式分派）
    // ------------------------------------------------------------------

    private static List<Quad> Cat(params List<Quad>[] parts)
    {
        var list = new List<Quad>();
        foreach (var p in parts) if (p is not null) list.AddRange(p);
        return list;
    }

    private static List<Quad> Emit(string op, string a1, string a2, string res)
        => new() { new Quad { Op = op, Arg1 = a1, Arg2 = a2, Result = res } };

    private static Attr Reduce(Context ctx, Production p, List<Attr> rhs)
    {
        var a = new Attr { Line = rhs.Count > 0 ? rhs[0].Line : 0 };

        switch (p.Key)
        {
            // ---------------- 程序与块 ----------------
            case "prog->blk":
                a.Code = rhs[0].Code;
                break;

            case "blk->{ decls stmts }":
                a.Code = Cat(rhs[1].Code, rhs[2].Code);
                break;

            // ---------------- 声明 ----------------
            case "decls->decls decl":
            case "decls->ε":
            case "stmts->stmts stmt":
            case "stmts->ε":
                a.Code = Cat(rhs.Count > 0 ? rhs[0].Code : new List<Quad>(),
                             rhs.Count > 1 ? rhs[1].Code : new List<Quad>());
                break;

            case "decl->int id ;":
                ctx.Declare(rhs[1].Name, VarType.Int, rhs[1].Line);
                break;

            case "decl->real id ;":
                ctx.Declare(rhs[1].Name, VarType.Real, rhs[1].Line);
                break;

            // ---------------- 赋值 ----------------
            case "stmt->id = expr ;":
            {
                a.Line = rhs[0].Line;
                var sym = ctx.Lookup(rhs[0].Name);
                if (sym is null)
                {
                    ctx.Error(rhs[0].Line, $"变量“{rhs[0].Name}”未声明就使用。");
                }
                else if (rhs[2].Type == VarType.Error) { /* 前面已报错，不再级联 */ }
                else if (!rhs[2].Type.IsNumeric())
                {
                    ctx.Error(rhs[0].Line, $"赋值号右边不是数值表达式，无法赋给变量“{rhs[0].Name}”。");
                    rhs[2].Type = VarType.Error;
                }
                else if (sym.Type == VarType.Int && rhs[2].Type == VarType.Real)
                {
                    ctx.Error(rhs[0].Line,
                        $"把实型表达式赋给整型变量“{rhs[0].Name}”，会丢失小数部分。");
                }

                a.Code = Cat(rhs[2].Code, Emit("=", rhs[2].Place, "", sym?.Store ?? rhs[0].Name));
                break;
            }

            // ---------------- 分支与循环 ----------------
            case "stmt->if ( cond ) blk":
            {
                string l1 = ctx.NewLabel();
                a.Code = Cat(rhs[2].Code,
                             Emit("jz", rhs[2].Place, "", l1),
                             rhs[4].Code,
                             Emit("label", "", "", l1));
                break;
            }

            case "stmt->if ( cond ) blk else blk":
            {
                string l1 = ctx.NewLabel(), l2 = ctx.NewLabel();
                a.Code = Cat(rhs[2].Code,
                             Emit("jz", rhs[2].Place, "", l1),
                             rhs[4].Code,
                             Emit("j", "", "", l2),
                             Emit("label", "", "", l1),
                             rhs[6].Code,
                             Emit("label", "", "", l2));
                break;
            }

            case "stmt->while ( cond ) blk":
            {
                string l1 = ctx.NewLabel(), l2 = ctx.NewLabel();
                a.Code = Cat(Emit("label", "", "", l1),
                             rhs[2].Code,
                             Emit("jz", rhs[2].Place, "", l2),
                             rhs[4].Code,
                             Emit("j", "", "", l1),
                             Emit("label", "", "", l2));
                break;
            }

            case "stmt->blk":
                a.Code = rhs[0].Code;
                break;

            // ---------------- 输入输出 ----------------
            case "stmt->read ( id ) ;":
            {
                var sym = ctx.Lookup(rhs[2].Name);
                if (sym is null)
                    ctx.Error(rhs[2].Line, $"变量“{rhs[2].Name}”未声明就用于输入语句。");
                a.Code = Emit("read", "", "", sym?.Store ?? rhs[2].Name);
                break;
            }

            case "stmt->write ( expr ) ;":
                a.Code = Cat(rhs[2].Code, Emit("write", rhs[2].Place, "", ""));
                break;

            // ---------------- 条件 ----------------
            case "cond->expr relop expr":
            {
                if (!rhs[0].Type.IsNumeric() || !rhs[2].Type.IsNumeric())
                {
                    ctx.Error(rhs[0].Line, "关系运算符两侧都必须是数值表达式。");
                    a.Type = VarType.Error;
                    a.Code = Cat(rhs[0].Code, rhs[2].Code);
                    break;
                }
                int t = ctx.NewTemp(VarType.Bool);
                a.Place = "t" + t;
                a.Type = VarType.Bool;
                a.Code = Cat(rhs[0].Code, rhs[2].Code,
                             Emit(rhs[1].Op, rhs[0].Place, rhs[2].Place, a.Place));
                break;
            }

            case "cond->expr":
                a.Place = rhs[0].Place;
                a.Type = rhs[0].Type == VarType.Error ? VarType.Error : VarType.Bool;
                a.Code = rhs[0].Code;
                break;

            // ---------------- 算术表达式 ----------------
            case "expr->expr + term":
            case "expr->expr - term":
            case "term->term * factor":
            case "term->term / factor":
            {
                var left = rhs[0];
                var right = rhs[2];
                string op = p.Right[1];
                a.Type = VarTypes.Promote(left.Type, right.Type);
                if (a.Type != VarType.Error)
                {
                    int t = ctx.NewTemp(a.Type);
                    a.Place = "t" + t;
                    a.Code = Cat(left.Code, right.Code, Emit(op, left.Place, right.Place, a.Place));
                }
                else a.Code = Cat(left.Code, right.Code);
                break;
            }

            case "expr->term":
            case "term->factor":
                a.Place = rhs[0].Place;
                a.Type = rhs[0].Type;
                a.Code = rhs[0].Code;
                break;

            // ---------------- 因子 ----------------
            case "factor->( expr )":
                a.Place = rhs[1].Place;
                a.Type = rhs[1].Type;
                a.Code = rhs[1].Code;
                break;

            case "factor->id":
            {
                a.Name = rhs[0].Name;
                var sym = ctx.Lookup(rhs[0].Name);
                if (sym is null)
                {
                    ctx.Error(rhs[0].Line, $"变量“{rhs[0].Name}”未声明就使用。");
                    a.Type = VarType.Error;
                    a.Place = rhs[0].Name;
                }
                else { a.Type = sym.Type; a.Place = sym.Store; }
                break;
            }

            case "factor->num":
                a.Place = rhs[0].Place;
                a.Type = VarType.Int;
                break;

            case "factor->rnum":
                a.Place = rhs[0].Place;
                a.Type = VarType.Real;
                break;

            case "factor->- factor":
            {
                a.Type = rhs[1].Type;
                if (a.Type != VarType.Error)
                {
                    int t = ctx.NewTemp(a.Type);
                    a.Place = "t" + t;
                    a.Code = Cat(rhs[1].Code, Emit("neg", rhs[1].Place, "", a.Place));
                }
                else a.Code = rhs[1].Code;
                break;
            }

            default:
                a.Code = new List<Quad>();
                break;
        }
        return a;
    }
}

// =====================================================================
//                        六点五、四元式局部优化
// =====================================================================

/// <summary>
/// 四元式的局部优化：常量折叠、公共子表达式消除、删除无用赋值。
/// 全部在基本块内进行，且只处理临时变量，不改变源程序的可观察行为。
/// </summary>
public static class Optimizer
{
    public static List<Quad> Optimize(IReadOnlyList<Quad> src, List<string> log)
    {
        var code = src.Select(q => new Quad
        {
            Index = q.Index,
            Op = q.Op,
            Arg1 = q.Arg1,
            Arg2 = q.Arg2,
            Result = q.Result
        }).ToList();

        foreach (var (start, end) in SplitBlocks(code))
        {
            FoldConstants(code, start, end, log);
            EliminateCommonSubexpressions(code, start, end, log);
            RemoveDeadAssignments(code, start, end, log);
        }

        var result = code.Where(q => q.Op != "nop").ToList();
        for (int i = 0; i < result.Count; i++) result[i].Index = i + 1;
        return result;
    }

    /// <summary>按标号与跳转切分基本块，返回 [起, 止) 区间。</summary>
    private static List<(int Start, int End)> SplitBlocks(List<Quad> code)
    {
        var blocks = new List<(int, int)>();
        int start = 0;
        for (int i = 0; i < code.Count; i++)
        {
            if (code[i].Op is "j" or "jz")
            {
                blocks.Add((start, i + 1));
                start = i + 1;
            }
            else if (code[i].Op == "label" && i > start)
            {
                blocks.Add((start, i));
                start = i;
            }
        }
        if (start < code.Count) blocks.Add((start, code.Count));
        return blocks;
    }

    private static bool IsBinary(string op)
        => op is "+" or "-" or "*" or "/" or "<" or "<=" or ">" or ">=" or "==" or "!=";

    private static bool IsTemp(string s)
        => s.Length > 1 && s[0] == 't' && s.Skip(1).All(char.IsDigit);

    private static bool IsIntConst(string s)
        => s.Length > 0 && s.All(c => char.IsDigit(c) || (c == '-' && s.Length > 1));

    private static string Subst(string s, Dictionary<string, string> map)
        => map.TryGetValue(s, out var v) ? v : s;

    /// <summary>常量折叠：两侧都是整型常量的运算直接算出结果，并把临时量的常量值向下传播。</summary>
    private static void FoldConstants(List<Quad> code, int start, int end, List<string> log)
    {
        var consts = new Dictionary<string, string>();

        for (int i = start; i < end; i++)
        {
            var q = code[i];

            if (q.Op == "=" && IsIntConst(q.Arg1) && IsTemp(q.Result))
            {
                consts[q.Result] = q.Arg1;
                continue;
            }

            if (IsBinary(q.Op))
            {
                string a1 = Subst(q.Arg1, consts), a2 = Subst(q.Arg2, consts);
                if (IsIntConst(a1) && IsIntConst(a2))
                {
                    long x = long.Parse(a1), y = long.Parse(a2);
                    if (!(q.Op == "/" && y == 0))
                    {
                        long v = q.Op switch
                        {
                            "+" => x + y,
                            "-" => x - y,
                            "*" => x * y,
                            _ => (long)Math.Truncate((double)x / y)
                        };
                        string before = q.Text;
                        q.Op = "=";
                        q.Arg1 = v.ToString();
                        q.Arg2 = "";
                        if (IsTemp(q.Result)) consts[q.Result] = q.Arg1;
                        log.Add($"常量折叠：{before} → {q.Text}");
                        continue;
                    }
                }
                q.Arg1 = a1;
                q.Arg2 = a2;
                continue;
            }

            if (q.Op == "jz") q.Arg1 = Subst(q.Arg1, consts);
        }
    }

    /// <summary>公共子表达式消除：同一基本块内重复出现的表达式直接复用前面算出的结果。</summary>
    private static void EliminateCommonSubexpressions(List<Quad> code, int start, int end, List<string> log)
    {
        var exprs = new Dictionary<string, string>();   // 表达式 → 已算出的结果名
        var alias = new Dictionary<string, string>();   // 临时量 → 与它等价的临时量

        string Resolve(string s)
        {
            while (alias.TryGetValue(s, out var v)) s = v;
            return s;
        }

        for (int i = start; i < end; i++)
        {
            var q = code[i];
            if (q.Op == "nop") continue;

            if (q.Op is "label" or "j" or "jz")
            {
                if (q.Op == "jz") q.Arg1 = Resolve(q.Arg1);
                continue;
            }

            q.Arg1 = Resolve(q.Arg1);
            q.Arg2 = Resolve(q.Arg2);

            if (IsBinary(q.Op))
            {
                string key = $"{q.Op} {q.Arg1} {q.Arg2}";
                if (exprs.TryGetValue(key, out var prev) && prev != q.Result)
                {
                    string before = q.Text;
                    if (IsTemp(q.Result))
                    {
                        // 结果也是临时量：直接让它等价于前面的结果，后续引用一并替换
                        alias[q.Result] = prev;
                        q.Op = "nop";
                        log.Add($"公共子表达式消除：{before} 复用 {prev}");
                    }
                    else
                    {
                        q.Op = "=";
                        q.Arg1 = prev;
                        q.Arg2 = "";
                        log.Add($"公共子表达式消除：{before} → {q.Text}");
                    }
                }
                else
                {
                    exprs[key] = q.Result;
                }
                continue;
            }

            // 名字被重新赋值后，引用它的表达式不再等价
            if ((q.Op == "=" || q.Op == "read") && q.Result.Length > 0)
            {
                foreach (string k in exprs.Where(kv => kv.Value == q.Result).Select(kv => kv.Key).ToList())
                    exprs.Remove(k);
            }
        }
    }

    /// <summary>删除无用赋值：临时量算出后再没有被任何四元式引用，这条赋值就是死代码。</summary>
    private static void RemoveDeadAssignments(List<Quad> code, int start, int end, List<string> log)
    {
        var refs = new Dictionary<string, int>();
        foreach (var q in code)
        {
            if (q.Op == "nop") continue;
            if (IsTemp(q.Arg1)) refs[q.Arg1] = refs.GetValueOrDefault(q.Arg1) + 1;
            if (IsTemp(q.Arg2)) refs[q.Arg2] = refs.GetValueOrDefault(q.Arg2) + 1;
        }

        for (int i = start; i < end; i++)
        {
            var q = code[i];
            if (q.Op == "nop" || !IsTemp(q.Result)) continue;
            if (q.Op != "=" && q.Op != "neg" && !IsBinary(q.Op)) continue;
            if (refs.GetValueOrDefault(q.Result) > 0) continue;

            string before = q.Text;
            q.Op = "nop";
            log.Add($"删除无用赋值：{before}");
        }
    }
}

// =====================================================================
//                          七、解释执行四元式
// =====================================================================

/// <summary>一次运行的结果。</summary>
public sealed class RunResult
{
    public bool Ok { get; set; } = true;
    public string Message { get; set; } = "";
    public int Steps { get; set; }
    public List<string> Output { get; } = new();
    public List<(string Name, string Type, string Value)> Variables { get; } = new();
}

/// <summary>
/// 四元式解释器。按四元式序列逐条执行，标签用跳转表定位，
/// read 语句从调用方提供的输入序列中依次取值。
/// </summary>
public static class Interpreter
{
    public static RunResult Run(CompileResult r, IReadOnlyList<string> inputs)
    {
        var result = new RunResult();
        var env = new Dictionary<string, object>();
        var labels = new Dictionary<string, int>();

        var quads = r.OptimizedQuads.Count > 0 ? r.OptimizedQuads : r.Quads;

        for (int i = 0; i < quads.Count; i++)
            if (quads[i].Op == "label") labels[quads[i].Result] = i;

        // 已声明的变量先按类型置初值（用存储名，便于区分不同层次的同名变量）
        foreach (var s in r.Symbols)
        {
            if (s.IsTemp) continue;
            env[StoreOf(s)] = s.Type == VarType.Real ? 0.0 : 0L;
        }

        int pc = 0, step = 0, readIndex = 0;

        while (pc >= 0 && pc < quads.Count)
        {
            if (++step > 200000)
            {
                result.Ok = false;
                result.Message = "执行步数过多，可能是死循环，已中止。";
                break;
            }

            var q = quads[pc];
            switch (q.Op)
            {
                case "label":
                    pc++;
                    break;

                case "j":
                    if (!labels.TryGetValue(q.Result, out pc))
                        return Fail(result, $"找不到标号 {q.Result}。");
                    break;

                case "jz":
                    if (Num(ValueOf(env, q.Arg1)) == 0d)
                    {
                        if (!labels.TryGetValue(q.Result, out pc))
                            return Fail(result, $"找不到标号 {q.Result}。");
                    }
                    else pc++;
                    break;

                case "=":
                    env[q.Result] = Coerce(Num(ValueOf(env, q.Arg1)), TypeOf(r, q.Result));
                    pc++;
                    break;

                case "neg":
                    env[q.Result] = Coerce(0d - Num(ValueOf(env, q.Arg1)), TypeOf(r, q.Result));
                    pc++;
                    break;

                case "+" or "-" or "*" or "/":
                {
                    double x = Num(ValueOf(env, q.Arg1)), y = Num(ValueOf(env, q.Arg2));
                    if (q.Op == "/" && y == 0d)
                        return Fail(result, $"第 {q.Index} 条四元式发生除零错误。");
                    double v = q.Op switch
                    {
                        "+" => x + y,
                        "-" => x - y,
                        "*" => x * y,
                        _ => x / y
                    };
                    env[q.Result] = Coerce(v, TypeOf(r, q.Result));
                    pc++;
                    break;
                }

                case "<" or "<=" or ">" or ">=" or "==" or "!=":
                {
                    double x = Num(ValueOf(env, q.Arg1)), y = Num(ValueOf(env, q.Arg2));
                    bool v = q.Op switch
                    {
                        "<" => x < y,
                        "<=" => x <= y,
                        ">" => x > y,
                        ">=" => x >= y,
                        "==" => Math.Abs(x - y) < 1e-12,
                        _ => Math.Abs(x - y) >= 1e-12
                    };
                    env[q.Result] = v ? 1L : 0L;
                    pc++;
                    break;
                }

                case "read":
                {
                    string text = readIndex < inputs.Count ? inputs[readIndex] : "0";
                    readIndex++;
                    if (readIndex > inputs.Count)
                        result.Message = "输入数据不足，缺少的值按 0 处理。";
                    if (!double.TryParse(text, out double v)) v = 0d;
                    env[q.Result] = Coerce(v, TypeOf(r, q.Result));
                    pc++;
                    break;
                }

                case "write":
                    result.Output.Add(Format(ValueOf(env, q.Arg1)));
                    pc++;
                    break;

                default:
                    return Fail(result, $"无法识别的四元式操作：{q.Op}");
            }
        }

        result.Steps = step;

        // 输出最终变量取值（不含临时变量）
        foreach (var s in r.Symbols)
        {
            if (s.IsTemp) continue;
            if (env.TryGetValue(StoreOf(s), out object? v))
                result.Variables.Add((s.Display, s.Type.Text(), Format(v)));
        }
        if (result.Ok && result.Message.Length == 0)
            result.Message = "程序执行完毕。";
        return result;

        static RunResult Fail(RunResult result, string message)
        {
            result.Ok = false;
            result.Message = message;
            return result;
        }
    }

    private static string StoreOf(Symbol s) => string.IsNullOrEmpty(s.Store) ? s.Name : s.Store;

    private static VarType TypeOf(CompileResult r, string name)
        => r.VarTypes.TryGetValue(name, out var t) ? t : VarType.Real;

    /// <summary>取一个操作数的值：先查运行环境（变量与临时变量），否则按字面常量解析。</summary>
    private static object ValueOf(Dictionary<string, object> env, string s)
    {
        if (string.IsNullOrEmpty(s)) return 0L;
        if (env.TryGetValue(s, out object? v)) return v;
        if (s.IndexOf('.') >= 0 || s.IndexOf('e') >= 0 || s.IndexOf('E') >= 0)
            return double.TryParse(s, out double d) ? d : 0d;
        return long.TryParse(s, out long l) ? l : 0L;
    }

    private static double Num(object v) => v is double d ? d : Convert.ToDouble(v);

    /// <summary>把运算结果按目标类型存放：整型向零取整（整数除法、int 赋值都符合 C 的语义）。</summary>
    private static object Coerce(double v, VarType t)
        => t == VarType.Real ? v : (object)(long)Math.Truncate(v);

    private static string Format(object v)
    {
        if (v is double d)
            return Math.Abs(d - Math.Round(d)) < 1e-12 ? d.ToString("0.0") : d.ToString("0.#####");
        return Convert.ToInt64(v).ToString();
    }
}

