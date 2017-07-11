using System;
using System.Linq;
using System.Collections.Generic;

namespace LispInterpreter
{
    class Tokenizer
    {
        private string _input;

        public Tokenizer(string input)
        {
            _input = input;
        }

        public IEnumerable<string> GetTokens()
        {
            // Points to the current location in the string
            int stringPtr = 0;

            char CurrChar() => _input[stringPtr];
            char LookaheadChar(int i) => _input[stringPtr + i];

            while (stringPtr < _input.Length)
            {
                if (CurrChar() == '#' && LookaheadChar(1) == '(')
                {
                    // If we have a lambda declaration, immediately consume it
                    yield return "#(";
                    stringPtr += 2;
                }
                else if (CurrChar() == '(' || CurrChar() == ')')
                {
                    // If we have a bracket, immediately consume it
                    yield return CurrChar().ToString();
                    stringPtr++;
                } else if (char.IsDigit(CurrChar())) {
                    // If we have a digit, begin parsing a number
                    string buffer = "";

                    while (char.IsDigit(CurrChar()))
                    {
                        buffer += CurrChar();
                        stringPtr++;
                    }

                    yield return buffer;
                } else if (char.IsLetter(CurrChar()) || CurrChar() == ':' || CurrChar() == '%')
                {
                    // If we have a letter, begin parsing an identifier
                    string buffer = "";

                    while (char.IsLetterOrDigit(CurrChar()) || CurrChar() == ':' || CurrChar() == '%')
                    {
                        buffer += CurrChar();
                        stringPtr++;
                    }

                    yield return buffer;
                } else if (char.IsWhiteSpace(CurrChar()))
                {
                    // If we have whitespace, ignore it
                    stringPtr++;
                } else
                {
                    throw new Exception($"Unrecognised token {CurrChar()}");
                }
            }
        }
    }

    abstract class Node { }
    class ListNode : Node { public List<Node> Children { get; set; } }
    class LambdaNode : Node { public List<Node> Children { get; set; } }
    class IdentifierNode : Node { public string Value { get; set; } }
    class IntegerNode : Node { public int Value { get; set; } }
    class SymbolNode : Node { public string Value { get; set; } }

    class Parser
    {
        public IEnumerable<Node> ParseTokens(string[] tokens)
        {
            int tokenPtr = 0;

            while (tokenPtr < tokens.Length - 1)
            {
                string CurrToken()
                {
                    try
                    {
                        return tokens[tokenPtr];
                    } catch (IndexOutOfRangeException e)
                    {
                        throw new IndexOutOfRangeException("Tokens exhausted during parse - do you have unclosed parentheses?");
                    }
                }

                if (CurrToken() == "(" || CurrToken() == "#(")
                {
                    bool lambdaMode = (CurrToken() == "#(");

                    tokenPtr++;

                    var newTokens = new List<string>();
                    int nestingDepth = 1;

                    // Parse brackets by using depth to determine when the current list ends
                    for (;;)
                    {
                        newTokens.Add(CurrToken());
                        if (CurrToken() == "(" || CurrToken() == "#(")
                        {
                            nestingDepth++;
                            tokenPtr++;
                        }
                        else if (CurrToken() == ")")
                        {
                            nestingDepth--;
                            tokenPtr++;
                            if (nestingDepth == 0)
                            {
                                break;
                            }
                        }
                        else
                        {
                            tokenPtr++;
                        }
                    }

                    if (lambdaMode)
                    {
                        yield return new LambdaNode
                        {
                            Children = ParseTokens(newTokens.ToArray()).ToList()
                        };
                    } else
                    {
                        yield return new ListNode
                        {
                            Children = ParseTokens(newTokens.ToArray()).ToList()
                        };
                    }
                }
                else if (int.TryParse(CurrToken(), out int intValue))
                {
                    tokenPtr++;
                    yield return new IntegerNode { Value = intValue };
                }
                else
                {
                    var idValue = CurrToken();
                    tokenPtr++;
                    if (idValue.StartsWith(":"))
                    {
                        yield return new SymbolNode { Value = string.Concat(idValue.Skip(1)) };
                    }
                    else
                    {
                        yield return new IdentifierNode { Value = idValue };
                    }
                }
            }
        }
    }

    abstract class LispObject<T> { public T Value { get; set; } }
    class IntegerObject : LispObject<int> { }
    class SymbolObject : LispObject<string> { }
    class FunctionObject : LispObject<Func<List<dynamic>, Scope, dynamic>> { }
    class ListObject : LispObject<List<dynamic>> { }

    class Scope
    {
        public Dictionary<string, dynamic> Names { get; set; } = new Dictionary<string, dynamic>();
        public Scope Parent { get; set; }

        public Scope(Scope parent = null)
        {
            Parent = parent;
        }

        public dynamic Resolve(string name)
        {
            if (Names.ContainsKey(name))
            {
                return Names[name];
            } else if (Parent == null)
            {
                throw new KeyNotFoundException($"Could not resolve name {name}");
            } else
            {
                return Parent.Resolve(name);
            }
        }
    }

    class Interpreter
    {
        public static SymbolObject NilBlueprint => new SymbolObject { Value = "nil" };
        public static SymbolObject TrueBlueprint => new SymbolObject { Value = "true" };
        public static SymbolObject FalseBlueprint => new SymbolObject { Value = "false" };

        private static FunctionObject MakeFuncObj(Func<List<dynamic>, Scope, dynamic> func) =>
            new FunctionObject {
                Value = func
            };

        private static dynamic EvaluateIfRequired(dynamic o, Scope s)
            => o is FunctionObject
                ? o.Value.DynamicInvoke(new List<dynamic> { }, new Scope(s))
                : o;

        public static readonly Scope StandardLibrary = new Scope
        {
            Names = new Dictionary<string, dynamic>()
            {
                ["add"] = MakeFuncObj((x, s) => new IntegerObject { Value = x[0].Value + x[1].Value }),
                ["sub"] = MakeFuncObj((x, s) => new IntegerObject { Value = x[0].Value - x[1].Value }),

                ["def"] = MakeFuncObj((x, s) => {
                    s.Names[x[0].Value] = x[1];
                    return x[1];
                }),

                ["do"] = MakeFuncObj((x, s) => NilBlueprint),

                ["print"] = MakeFuncObj((x, s) => {
                    if (x[0] is ListObject)
                    {
                        Console.Write("(");
                        bool isFirst = true;
                        foreach (var item in ((ListObject)(x[0])).Value)
                        {
                            if (!isFirst)
                            {
                                Console.Write(" ");
                            }
                            Console.Write(item.Value);
                            isFirst = false;
                        }
                        Console.WriteLine(")");
                    }
                    else
                    {
                        Console.WriteLine(x[0].Value.ToString());
                    }
                    return NilBlueprint;
                }),

                ["list"] = MakeFuncObj((x, s) => new ListObject { Value = x }),

                ["car"] = MakeFuncObj((x, s) => x[0].Value[0]),

                ["cdr"] = MakeFuncObj((x, s) => new ListObject { Value = Enumerable.ToList(Enumerable.Skip(x[0].Value, 1)) }),

                ["when"] = MakeFuncObj((x, s) =>
                {
                    var condition = x[0];
                    if (condition is SymbolObject && condition.Value == "true")
                    {
                        var action = x[1];
                        return EvaluateIfRequired(action, new Scope(s));
                    } else
                    {
                        return FalseBlueprint;
                    }
                }),

                ["if"] = MakeFuncObj((x, s) =>
                {
                    var condition = x[0];
                    dynamic action;
                    if (condition is SymbolObject && condition.Value == "true")
                    {
                        action = x[1];
                    }
                    else
                    {
                        action = x[2];
                    }
                    return EvaluateIfRequired(action, new Scope(s));
                }),

                ["eq"] = MakeFuncObj((x, s) =>
                {
                    return x[0].Value == x[1].Value
                        ? TrueBlueprint
                        : FalseBlueprint;
                }),

                ["id"] = MakeFuncObj((x, s) => x[0])
            }
        };

        public List<Node> _root;

        public Interpreter(List<Node> root)
        {
            _root = root;
        }

        public List<dynamic> EvaluateRoot() =>
            _root.Select(x => Evaluate(x, new Scope(StandardLibrary))).ToList();

        public dynamic Evaluate(Node node, Scope enclosingScope)
        {
            switch (node)
            {
                case IntegerNode i:
                    return new IntegerObject { Value = i.Value };
                case IdentifierNode i:
                    return enclosingScope.Resolve(i.Value);
                case SymbolNode s:
                    return new SymbolObject { Value = s.Value };
                case ListNode l:
                    var func = Evaluate(l.Children[0], enclosingScope);
                    var args = l.Children.Skip(1).Select(x => Evaluate(x, enclosingScope)).ToList();

                    return func.Value.DynamicInvoke(args, enclosingScope);
                case LambdaNode l:
                    return new FunctionObject {
                        Value = ((x, s) => {
                            int counter = 0;
                            foreach (var argument in x)
                            {
                                s.Names[$"%{counter}"] = argument;
                                counter++;
                            }
                            return Evaluate(new ListNode { Children = l.Children }, new Scope(s));
                        })
                    };
                default:
                    throw new InvalidOperationException("Attempt to evaluate unrecognised node type");
            }
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var program = @"
(do
    (def :factorial
        #(if (eq %0 1)
            1
            #(add %0 (factorial (sub %0 1)))))
    (print (factorial 5))
)
";
            var tokens = new Tokenizer(program).GetTokens().ToList();
            var parsed = new Parser().ParseTokens(tokens.ToArray()).ToList();

            var result = new Interpreter(parsed).EvaluateRoot();
            }
    }
}