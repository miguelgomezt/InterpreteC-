// Nodos del AST que son EXPRESIONES (producen un valor). Son el contrato con el evaluador.
// ToString() devuelve el nodo como código con los operadores entre paréntesis,
// para ver la agrupación: "1 + 2 * 3" → "(1 + (2 * 3))".

using System.Collections.Generic;
using System.Linq;
using Interprete.Lexing;

namespace Interprete.Ast;

/// <summary>Raíz de la jerarquía: todo nodo del AST es un Node.</summary>
public abstract class Node
{
    public abstract string TokenLiteral { get; }
    public abstract override string ToString();
}

/// <summary>Nodo que produce un valor. Guarda su Token (con Line y Column) para reportar errores.</summary>
public abstract class Expr(Token token) : Node
{
    public Token Token { get; } = token;
    public override string TokenLiteral => Token.Literal;
}

// ----- Hojas: literales e identificadores -----

/// <summary>x   suma</summary>
public sealed class Identifier(Token token, string value) : Expr(token)
{
    public string Value { get; } = value;
    public override string ToString() => Value;
}

/// <summary>42</summary>
public sealed class IntegerLiteral(Token token, long value) : Expr(token)
{
    public long Value { get; } = value;
    public override string ToString() => Token.Literal;
}

/// <summary>true / false</summary>
public sealed class BooleanLiteral(Token token, bool value) : Expr(token)
{
    public bool Value { get; } = value;
    public override string ToString() => Token.Literal;
}

/// <summary>"hola" (Value viene sin comillas)</summary>
public sealed class StringLiteral(Token token, string value) : Expr(token)
{
    public string Value { get; } = value;
    public override string ToString() => "\"" + Value + "\"";
}

// ----- Operadores -----

/// <summary>-5   !true</summary>
public sealed class PrefixExpr(Token token, string op, Expr right) : Expr(token)
{
    public string Operator { get; } = op;   // "-" o "!"
    public Expr Right { get; } = right;
    public override string ToString() => $"({Operator}{Right})";
}

/// <summary>a + b   x == y   n ** 2  (Left y Right pueden ser otros InfixExpr)</summary>
public sealed class InfixExpr(Token token, Expr left, string op, Expr right) : Expr(token)
{
    public Expr Left { get; } = left;
    public string Operator { get; } = op;
    public Expr Right { get; } = right;
    public override string ToString() => $"({Left} {Operator} {Right})";
}

/// <summary>x = 5   arr[0] = 5  (Left siempre es Identifier o IndexExpr; es expresión para usarla en el for)</summary>
public sealed class AssignExpr(Token token, Expr left, Expr value) : Expr(token)
{
    public Expr Left { get; } = left;
    public Expr Value { get; } = value;
    public override string ToString() => $"{Left} = {Value}";
}

// ----- Estructuras de control (también son expresiones) -----

/// <summary>
/// if (c) {...} else {...}. No hay nodo Elif: un elif es un IfExpr dentro del Alternative.
/// Alternative es null si no hay else/elif.
/// </summary>
public sealed class IfExpr(Token token, Expr condition, BlockStmt consequence, BlockStmt? alternative) : Expr(token)
{
    public Expr Condition { get; } = condition;
    public BlockStmt Consequence { get; } = consequence;
    public BlockStmt? Alternative { get; } = alternative;

    public override string ToString()
    {
        var text = $"if {Condition} {Consequence}";
        return Alternative is null ? text : $"{text} else {Alternative}";
    }
}

/// <summary>while (c) {...}</summary>
public sealed class WhileExpr(Token token, Expr condition, BlockStmt body) : Expr(token)
{
    public Expr Condition { get; } = condition;
    public BlockStmt Body { get; } = body;
    public override string ToString() => $"while {Condition} {Body}";
}

/// <summary>for (Init; Condition; Update) {...}. Las tres partes son opcionales (null); Init es una sentencia.</summary>
public sealed class ForExpr(Token token, Stmt? init, Expr? condition, Expr? update, BlockStmt body) : Expr(token)
{
    public Stmt? Init { get; } = init;
    public Expr? Condition { get; } = condition;
    public Expr? Update { get; } = update;
    public BlockStmt Body { get; } = body;

    public override string ToString()
    {
        var init = Init?.ToString().TrimEnd(';') ?? "";   // LetStmt termina en ';'
        return $"for ({init}; {Condition}; {Update}) {Body}";
    }
}

// ----- Funciones -----

/// <summary>function(a, b) {...}  (sin nombre: se nombra con let)</summary>
public sealed class FunctionLiteral(Token token, IReadOnlyList<Identifier> parameters, BlockStmt body) : Expr(token)
{
    public IReadOnlyList<Identifier> Parameters { get; } = parameters;
    public BlockStmt Body { get; } = body;
    public override string ToString() => $"{TokenLiteral}({string.Join(", ", Parameters)}) {Body}";
}

/// <summary>suma(1, 2)  (Function es un Identifier o un FunctionLiteral)</summary>
public sealed class CallExpr(Token token, Expr function, IReadOnlyList<Expr> arguments) : Expr(token)
{
    public Expr Function { get; } = function;
    public IReadOnlyList<Expr> Arguments { get; } = arguments;
    public override string ToString() => $"{Function}({string.Join(", ", Arguments)})";
}

// ----- Estructuras de datos -----

/// <summary>[1, 2 + 3, "hola"]</summary>
public sealed class ArrayLiteral(Token token, IReadOnlyList<Expr> elements) : Expr(token)
{
    public IReadOnlyList<Expr> Elements { get; } = elements;
    public override string ToString() => $"[{string.Join(", ", Elements)}]";
}

/// <summary>arr[0]   h["nombre"]</summary>
public sealed class IndexExpr(Token token, Expr left, Expr index) : Expr(token)
{
    public Expr Left { get; } = left;
    public Expr Index { get; } = index;
    public override string ToString() => $"({Left}[{Index}])";
}

/// <summary>Pareja clave: valor de un HashLiteral.</summary>
public sealed record HashPair(Expr Key, Expr Value);

/// <summary>{"nombre": "Pola"}  (las parejas van en una lista para conservar el orden)</summary>
public sealed class HashLiteral(Token token, IReadOnlyList<HashPair> pairs) : Expr(token)
{
    public IReadOnlyList<HashPair> Pairs { get; } = pairs;
    public override string ToString() =>
        "{" + string.Join(", ", Pairs.Select(p => $"{p.Key}: {p.Value}")) + "}";
}