// Nodos del AST que son SENTENCIAS (no producen valor) y la raíz Program.

using System.Collections.Generic;
using System.Linq;
using Interprete.Lexing;

namespace Interprete.Ast;

public abstract class Stmt(Token token) : Node
{
    public Token Token { get; } = token;
    public override string TokenLiteral => Token.Literal;
}

/// <summary>Raíz del AST: lista de sentencias. El parser siempre devuelve un Program (aunque esté vacío).</summary>
public sealed class Program(IReadOnlyList<Stmt> statements) : Node
{
    public IReadOnlyList<Stmt> Statements { get; } = statements;
    public override string TokenLiteral => Statements.Count > 0 ? Statements[0].TokenLiteral : "";
    public override string ToString() => string.Concat(Statements);   // sin separador
}

/// <summary>let edad = 20;</summary>
public sealed class LetStmt(Token token, Identifier name, Expr value) : Stmt(token)
{
    public Identifier Name { get; } = name;
    public Expr Value { get; } = value;
    public override string ToString() => $"{TokenLiteral} {Name} = {Value};";
}

/// <summary>return a + b;</summary>
public sealed class ReturnStmt(Token token, Expr returnValue) : Stmt(token)
{
    public Expr ReturnValue { get; } = returnValue;
    public override string ToString() => $"{TokenLiteral} {ReturnValue};";
}

/// <summary>Expresión usada como sentencia:  x + 10;   suma(1, 2);   if (...) {...}</summary>
public sealed class ExpressionStmt(Token token, Expr expression) : Stmt(token)
{
    public Expr Expression { get; } = expression;
    public override string ToString() => Expression.ToString();
}

/// <summary>Sentencias entre llaves; cuerpo de if, while, for y funciones.</summary>
public sealed class BlockStmt(Token token, IReadOnlyList<Stmt> statements) : Stmt(token)
{
    public IReadOnlyList<Stmt> Statements { get; } = statements;
    public override string ToString() =>
        "{ " + string.Concat(Statements.Select(s => s + " ")) + "}";
}