// Analizador sintáctico (parser de Pratt): convierte los tokens del Lexer en el AST (Program).
//
// Idea de Pratt:
//   - Cada tipo de token tiene hasta dos funciones de parseo: PREFIJA (el token inicia una
//     expresión: 5, x, -x, (a+b), if...) e INFIJA (el token va entre dos expresiones: a + b, f(x), a[0]).
//   - Cada operador infijo tiene una PRECEDENCIA; ParseExpression las usa para agrupar.
//
// Convención: _curToken es el token actual y _peekToken el siguiente. Una función de parseo de
// expresiones entra con _curToken en el PRIMER token de la expresión y sale con _curToken en el ÚLTIMO.
//
// Diferencia con el repo base: si una parte falla (devuelve null) el nodo padre no se crea, así
// ningún nodo del AST tiene hijos null. Si Errors no está vacía, el AST no debe evaluarse.

// Los using van debajo de 'namespace' a propósito: así 'Program' siempre es Interprete.Ast.Program
// aunque exista otra clase Program (la del punto de entrada) en el espacio global.
namespace Interprete.Parsing;

using System;
using System.Collections.Generic;
using System.Globalization;
using Interprete.Ast;
using Interprete.Lexing;

/// <summary>Niveles de precedencia, de menor a mayor: mayor precedencia = se agrupa primero.</summary>
public enum Precedence
{
    Lowest = 1,
    Assign,       // =
    Equality,     // == !=
    Comparison,   // < > <= >=
    Sum,          // + -
    Product,      // * / %
    Prefix,       // -x !x
    Exponent,     // **  (más fuerte que el prefijo: -2 ** 2 = -(2 ** 2))
    Call,         // f(x)
    Index,        // a[i]
}

public sealed class Parser
{
    // Precedencia de cada token cuando actúa como operador infijo. Lo que no está aquí
    // (';', ')', '}', ',', Eof...) vale Lowest y hace que ParseExpression se detenga.
    private static readonly Dictionary<TokenType, Precedence> Precedences = new()
    {
        [TokenType.Assign] = Precedence.Assign,
        [TokenType.Eq] = Precedence.Equality,
        [TokenType.NotEq] = Precedence.Equality,
        [TokenType.Lt] = Precedence.Comparison,
        [TokenType.Gt] = Precedence.Comparison,
        [TokenType.Lte] = Precedence.Comparison,
        [TokenType.Gte] = Precedence.Comparison,
        [TokenType.Plus] = Precedence.Sum,
        [TokenType.Minus] = Precedence.Sum,
        [TokenType.Slash] = Precedence.Product,
        [TokenType.Asterisk] = Precedence.Product,
        [TokenType.Mod] = Precedence.Product,
        [TokenType.Pow] = Precedence.Exponent,
        [TokenType.LParen] = Precedence.Call,
        [TokenType.LBracket] = Precedence.Index,
    };

    private readonly Lexer _lexer;
    private readonly List<string> _errors = new();

    private Token _curToken;
    private Token _peekToken;

    private readonly Dictionary<TokenType, Func<Expr?>> _prefixParseFns = new();
    private readonly Dictionary<TokenType, Func<Expr, Expr?>> _infixParseFns = new();

    public Parser(Lexer lexer)
    {
        _lexer = lexer;

        // Prefijas: tokens que pueden iniciar una expresión.
        _prefixParseFns[TokenType.Ident] = ParseIdentifier;
        _prefixParseFns[TokenType.Int] = ParseIntegerLiteral;
        _prefixParseFns[TokenType.String] = ParseStringLiteral;
        _prefixParseFns[TokenType.True] = ParseBoolean;
        _prefixParseFns[TokenType.False] = ParseBoolean;
        _prefixParseFns[TokenType.Bang] = ParsePrefixExpression;
        _prefixParseFns[TokenType.Minus] = ParsePrefixExpression;
        _prefixParseFns[TokenType.LParen] = ParseGroupedExpression;
        _prefixParseFns[TokenType.If] = ParseIfExpression;
        _prefixParseFns[TokenType.While] = ParseWhileExpression;
        _prefixParseFns[TokenType.For] = ParseForExpression;
        _prefixParseFns[TokenType.Function] = ParseFunctionLiteral;
        _prefixParseFns[TokenType.LBracket] = ParseArrayLiteral;
        _prefixParseFns[TokenType.LBrace] = ParseHashLiteral;

        // Infijas: tokens que aparecen entre dos expresiones.
        _infixParseFns[TokenType.Plus] = ParseInfixExpression;
        _infixParseFns[TokenType.Minus] = ParseInfixExpression;
        _infixParseFns[TokenType.Asterisk] = ParseInfixExpression;
        _infixParseFns[TokenType.Slash] = ParseInfixExpression;
        _infixParseFns[TokenType.Mod] = ParseInfixExpression;
        _infixParseFns[TokenType.Pow] = ParseInfixExpression;
        _infixParseFns[TokenType.Eq] = ParseInfixExpression;
        _infixParseFns[TokenType.NotEq] = ParseInfixExpression;
        _infixParseFns[TokenType.Lt] = ParseInfixExpression;
        _infixParseFns[TokenType.Gt] = ParseInfixExpression;
        _infixParseFns[TokenType.Lte] = ParseInfixExpression;
        _infixParseFns[TokenType.Gte] = ParseInfixExpression;
        _infixParseFns[TokenType.Assign] = ParseAssignExpression;
        _infixParseFns[TokenType.LParen] = ParseCallExpression;
        _infixParseFns[TokenType.LBracket] = ParseIndexExpression;

        _curToken = _lexer.NextToken();
        _peekToken = _lexer.NextToken();
    }

    /// <summary>Errores de sintaxis con línea y columna. Revisarla después de ParseProgram.</summary>
    public IReadOnlyList<string> Errors => _errors;

    // ----- Utilidades -----

    private void NextToken()
    {
        _curToken = _peekToken;
        _peekToken = _lexer.NextToken();
    }

    private bool CurTokenIs(TokenType type) => _curToken.Type == type;
    private bool PeekTokenIs(TokenType type) => _peekToken.Type == type;

    // Exige que el siguiente token sea 'type': si lo es, avanza; si no, registra el error y devuelve false.
    private bool ExpectPeek(TokenType type)
    {
        if (PeekTokenIs(type))
        {
            NextToken();
            return true;
        }
        PeekError(type);
        return false;
    }

    private Precedence PeekPrecedence() =>
        Precedences.TryGetValue(_peekToken.Type, out var p) ? p : Precedence.Lowest;

    private Precedence CurPrecedence() =>
        Precedences.TryGetValue(_curToken.Type, out var p) ? p : Precedence.Lowest;

    private void AddError(Token at, string message) =>
        _errors.Add($"Error de sintaxis (línea {at.Line}, columna {at.Column}): {message}");

    private void PeekError(TokenType expected) =>
        AddError(_peekToken, $"se esperaba {Name(expected)}, pero se encontró {Describe(_peekToken)}");

    private void NoPrefixParseFnError() =>
        AddError(_curToken, $"no se puede iniciar una expresión con {Describe(_curToken)}");

    private static string Describe(Token t) =>
        t.Type == TokenType.Eof ? "el fin de la entrada" : $"'{t.Literal}'";

    private static string Name(TokenType t) => t switch
    {
        TokenType.Ident => "un identificador",
        TokenType.Assign => "'='",
        TokenType.Semicolon => "';'",
        TokenType.Colon => "':'",
        TokenType.Comma => "','",
        TokenType.LParen => "'('",
        TokenType.RParen => "')'",
        TokenType.LBrace => "'{'",
        TokenType.RBrace => "'}'",
        TokenType.LBracket => "'['",
        TokenType.RBracket => "']'",
        _ => t.ToString(),
    };

    // ----- Programa y sentencias -----

    /// <summary>
    /// Punto de entrada: parsea sentencias hasta Eof. Cada ParseStatement termina en el último
    /// token de la sentencia, por eso después se llama a NextToken.
    /// </summary>
    public Program ParseProgram()
    {
        var statements = new List<Stmt>();

        while (!CurTokenIs(TokenType.Eof))
        {
            var stmt = ParseStatement();
            if (stmt is not null) statements.Add(stmt);
            NextToken();
        }

        return new Program(statements);
    }

    // let → declaración · return → retorno · ';' → vacía (se ignora) · otro → expresión como sentencia.
    private Stmt? ParseStatement()
    {
        switch (_curToken.Type)
        {
            case TokenType.Let: return ParseLetStatement();
            case TokenType.Return: return ParseReturnStatement();
            case TokenType.Semicolon: return null;
            default: return ParseExpressionStatement();
        }
    }

    // let <ident> = <expresión> [;]
    private LetStmt? ParseLetStatement()
    {
        var letToken = _curToken;

        if (!ExpectPeek(TokenType.Ident)) return null;
        var name = new Identifier(_curToken, _curToken.Literal);

        if (!ExpectPeek(TokenType.Assign)) return null;
        NextToken();

        var value = ParseExpression(Precedence.Lowest);
        if (value is null) return null;

        if (PeekTokenIs(TokenType.Semicolon)) NextToken();

        return new LetStmt(letToken, name, value);
    }

    // return <expresión> [;]
    private ReturnStmt? ParseReturnStatement()
    {
        var returnToken = _curToken;
        NextToken();

        var value = ParseExpression(Precedence.Lowest);
        if (value is null) return null;

        if (PeekTokenIs(TokenType.Semicolon)) NextToken();

        return new ReturnStmt(returnToken, value);
    }

    // <expresión> [;]   (el ';' es opcional para poder escribir "5 + 5" en el REPL)
    private ExpressionStmt? ParseExpressionStatement()
    {
        var firstToken = _curToken;

        var expression = ParseExpression(Precedence.Lowest);
        if (expression is null) return null;

        if (PeekTokenIs(TokenType.Semicolon)) NextToken();

        return new ExpressionStmt(firstToken, expression);
    }

    // { sentencias }   Entra en '{', sale en '}'. Si llega a Eof sin '}', registra el error.
    private BlockStmt ParseBlockStatement()
    {
        var braceToken = _curToken;
        var statements = new List<Stmt>();

        NextToken();

        while (!CurTokenIs(TokenType.RBrace) && !CurTokenIs(TokenType.Eof))
        {
            var stmt = ParseStatement();
            if (stmt is not null) statements.Add(stmt);
            NextToken();
        }

        if (!CurTokenIs(TokenType.RBrace))
            AddError(_curToken, "bloque sin cerrar: falta '}'");

        return new BlockStmt(braceToken, statements);
    }

    // ----- Núcleo de Pratt -----

    // 'precedence' es la del operador a la IZQUIERDA (Lowest si no hay): "sigue absorbiendo
    // operadores a la derecha mientras aten con MÁS fuerza que el de la izquierda".
    //   1. Con la función prefija del token actual se parsea el lado izquierdo.
    //   2. Mientras el siguiente token tenga precedencia mayor, se avanza a él y se llama a su
    //      función infija pasándole lo parseado; el resultado es el nuevo lado izquierdo.
    //   3. Si no, se devuelve lo construido.
    //
    //   1 + 2 * 3:  izq = 1 → '+' (Sum > Lowest) → su derecha se parsea con Sum:
    //               izq = 2 → '*' (Product > Sum) → (2 * 3)   ⇒   (1 + (2 * 3))
    //   1 * 2 + 3:  izq = 1 → '*' → su derecha con Product: 2, y '+' (Sum) no es mayor → 2
    //               izq = (1 * 2) → '+' (Sum > Lowest)        ⇒   ((1 * 2) + 3)
    private Expr? ParseExpression(Precedence precedence)
    {
        if (!_prefixParseFns.TryGetValue(_curToken.Type, out var prefix))
        {
            NoPrefixParseFnError();
            return null;
        }
        var left = prefix();

        while (left is not null
               && !PeekTokenIs(TokenType.Semicolon)
               && precedence < PeekPrecedence())
        {
            if (!_infixParseFns.TryGetValue(_peekToken.Type, out var infix)) return left;

            NextToken();
            left = infix(left);
        }

        return left;
    }

    // ----- Funciones prefijas -----

    private Expr? ParseIdentifier() => new Identifier(_curToken, _curToken.Literal);

    // El lexer entrega el número como texto; aquí se convierte a long (error si no cabe en 64 bits).
    private Expr? ParseIntegerLiteral()
    {
        if (!long.TryParse(_curToken.Literal, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            AddError(_curToken, $"no se pudo convertir '{_curToken.Literal}' a entero");
            return null;
        }
        return new IntegerLiteral(_curToken, value);
    }

    private Expr? ParseStringLiteral() => new StringLiteral(_curToken, _curToken.Literal);

    private Expr? ParseBoolean() => new BooleanLiteral(_curToken, CurTokenIs(TokenType.True));

    // -x  !x   El operando se parsea con Prefix: en "-a * b" el '-' solo toma 'a' ((-a) * b),
    // pero como Exponent > Prefix, "-2 ** 2" es (-(2 ** 2)).
    private Expr? ParsePrefixExpression()
    {
        var opToken = _curToken;
        NextToken();

        var right = ParseExpression(Precedence.Prefix);
        if (right is null) return null;

        return new PrefixExpr(opToken, opToken.Literal, right);
    }

    // (a + b)   Los paréntesis no crean nodo: solo reinician la precedencia (parsean desde Lowest).
    private Expr? ParseGroupedExpression()
    {
        NextToken();

        var expression = ParseExpression(Precedence.Lowest);
        if (expression is null) return null;

        if (!ExpectPeek(TokenType.RParen)) return null;

        return expression;
    }

    // Condición de if / elif / while, con o sin paréntesis. Entra en la palabra reservada, sale en '{'.
    private Expr? ParseCondition()
    {
        NextToken();

        var condition = ParseExpression(Precedence.Lowest);
        if (condition is null) return null;

        if (!ExpectPeek(TokenType.LBrace)) return null;

        return condition;
    }

    // if (c) {...} [elif (c) {...}]... [else {...}]
    // Un elif es un else que contiene otro if: al ver 'elif' se llama recursivamente a esta misma
    // función y el resultado se mete en un bloque que sirve de Alternative.
    private Expr? ParseIfExpression()
    {
        var ifToken = _curToken;

        var condition = ParseCondition();
        if (condition is null) return null;

        var consequence = ParseBlockStatement();

        BlockStmt? alternative = null;

        if (PeekTokenIs(TokenType.Elif))
        {
            NextToken();
            var elifToken = _curToken;

            var nested = ParseIfExpression();
            if (nested is null) return null;

            alternative = new BlockStmt(elifToken,
                new List<Stmt> { new ExpressionStmt(elifToken, nested) });
        }
        else if (PeekTokenIs(TokenType.Else))
        {
            NextToken();
            if (!ExpectPeek(TokenType.LBrace)) return null;

            alternative = ParseBlockStatement();
        }

        return new IfExpr(ifToken, condition, consequence, alternative);
    }

    // while (c) {...}
    private Expr? ParseWhileExpression()
    {
        var whileToken = _curToken;

        var condition = ParseCondition();
        if (condition is null) return null;

        var body = ParseBlockStatement();

        return new WhileExpr(whileToken, condition, body);
    }

    // for (<inicio>; <condición>; <actualización>) {...}   Cualquier parte puede omitirse (queda null).
    private Expr? ParseForExpression()
    {
        var forToken = _curToken;

        if (!ExpectPeek(TokenType.LParen)) return null;
        NextToken();

        // <inicio> es una sentencia: ParseStatement ya consume su ';', así que _curToken debe quedar en ';'.
        Stmt? init = null;
        if (!CurTokenIs(TokenType.Semicolon))
        {
            init = ParseStatement();
            if (init is null) return null;
            if (!CurTokenIs(TokenType.Semicolon))
            {
                PeekError(TokenType.Semicolon);
                return null;
            }
        }
        NextToken();

        // <condición> es una expresión: ParseExpression no consume el ';', por eso se exige.
        Expr? condition = null;
        if (!CurTokenIs(TokenType.Semicolon))
        {
            condition = ParseExpression(Precedence.Lowest);
            if (condition is null) return null;
            if (!ExpectPeek(TokenType.Semicolon)) return null;
        }
        NextToken();

        Expr? update = null;
        if (!CurTokenIs(TokenType.RParen))
        {
            update = ParseExpression(Precedence.Lowest);
            if (update is null) return null;
            if (!ExpectPeek(TokenType.RParen)) return null;
        }

        if (!ExpectPeek(TokenType.LBrace)) return null;
        var body = ParseBlockStatement();

        return new ForExpr(forToken, init, condition, update, body);
    }

    // function(<parámetros>) {...}
    private Expr? ParseFunctionLiteral()
    {
        var functionToken = _curToken;

        if (!ExpectPeek(TokenType.LParen)) return null;

        var parameters = ParseFunctionParameters();
        if (parameters is null) return null;

        if (!ExpectPeek(TokenType.LBrace)) return null;
        var body = ParseBlockStatement();

        return new FunctionLiteral(functionToken, parameters, body);
    }

    // (x, y, z)  Entra en '(', sale en ')'. Lista vacía si no hay parámetros; null si hay error.
    private List<Identifier>? ParseFunctionParameters()
    {
        var identifiers = new List<Identifier>();

        if (PeekTokenIs(TokenType.RParen))
        {
            NextToken();
            return identifiers;
        }

        if (!ExpectPeek(TokenType.Ident)) return null;
        identifiers.Add(new Identifier(_curToken, _curToken.Literal));

        while (PeekTokenIs(TokenType.Comma))
        {
            NextToken();
            if (!ExpectPeek(TokenType.Ident)) return null;
            identifiers.Add(new Identifier(_curToken, _curToken.Literal));
        }

        if (!ExpectPeek(TokenType.RParen)) return null;

        return identifiers;
    }

    // [1, 2 + 3]
    private Expr? ParseArrayLiteral()
    {
        var bracketToken = _curToken;

        var elements = ParseExpressionList(TokenType.RBracket);
        if (elements is null) return null;

        return new ArrayLiteral(bracketToken, elements);
    }

    // {"clave": valor, ...}  Un '{' al inicio de una expresión se lee como hash.
    private Expr? ParseHashLiteral()
    {
        var braceToken = _curToken;
        var pairs = new List<HashPair>();

        while (!PeekTokenIs(TokenType.RBrace))
        {
            NextToken();
            var key = ParseExpression(Precedence.Lowest);
            if (key is null) return null;

            if (!ExpectPeek(TokenType.Colon)) return null;

            NextToken();
            var value = ParseExpression(Precedence.Lowest);
            if (value is null) return null;

            pairs.Add(new HashPair(key, value));

            // Después de cada pareja: ',' (hay más) o '}' (fin).
            if (!PeekTokenIs(TokenType.RBrace) && !ExpectPeek(TokenType.Comma)) return null;
        }

        if (!ExpectPeek(TokenType.RBrace)) return null;

        return new HashLiteral(braceToken, pairs);
    }

    // ----- Funciones infijas (reciben el lado izquierdo; _curToken = el operador) -----

    // a + b, a ** b, a == b ...
    private Expr? ParseInfixExpression(Expr left)
    {
        var opToken = _curToken;

        // El lado derecho se parsea con la precedencia de ESTE operador: en "1 + 2 + 3" el segundo '+'
        // no es mayor, no se absorbe y queda ((1 + 2) + 3) → asociatividad por la izquierda.
        var rightPrecedence = CurPrecedence();

        // '**' asocia por la DERECHA: se baja un nivel (Exponent - 1 = Prefix) para que el siguiente '**'
        // sí sea mayor y se agrupe primero: 2 ** 3 ** 2 = 2 ** (3 ** 2).
        if (opToken.Type == TokenType.Pow) rightPrecedence = Precedence.Prefix;

        NextToken();
        var right = ParseExpression(rightPrecedence);
        if (right is null) return null;

        return new InfixExpr(opToken, left, opToken.Literal, right);
    }

    // x = 5   arr[0] = 1   Asocia por la DERECHA (a = b = 3 es a = (b = 3)): el valor se parsea con
    // Assign - 1 = Lowest, así el siguiente '=' sí se absorbe en la derecha.
    private Expr? ParseAssignExpression(Expr left)
    {
        var assignToken = _curToken;

        // Solo se puede asignar a una variable o a una posición (a[0]).
        if (left is not (Identifier or IndexExpr))
        {
            AddError(assignToken, $"no se puede asignar a {left}");
            return null;
        }

        NextToken();
        var value = ParseExpression(Precedence.Lowest);
        if (value is null) return null;

        return new AssignExpr(assignToken, left, value);
    }

    // suma(1, 2)   '(' actúa como infijo con precedencia Call. Sale en ')'.
    private Expr? ParseCallExpression(Expr function)
    {
        var parenToken = _curToken;

        var arguments = ParseExpressionList(TokenType.RParen);
        if (arguments is null) return null;

        return new CallExpr(parenToken, function, arguments);
    }

    // arr[0]   '[' actúa como infijo con la precedencia más alta: "a * b[0]" es (a * (b[0])). Sale en ']'.
    private Expr? ParseIndexExpression(Expr left)
    {
        var bracketToken = _curToken;

        NextToken();
        var index = ParseExpression(Precedence.Lowest);
        if (index is null) return null;

        if (!ExpectPeek(TokenType.RBracket)) return null;

        return new IndexExpr(bracketToken, left, index);
    }

    // Expresiones separadas por comas hasta 'end': argumentos f(1, 2) con ')' y elementos [1, 2] con ']'.
    // Lista vacía si no hay elementos; null si hay error.
    private List<Expr>? ParseExpressionList(TokenType end)
    {
        var list = new List<Expr>();

        if (PeekTokenIs(end))
        {
            NextToken();
            return list;
        }

        NextToken();
        var first = ParseExpression(Precedence.Lowest);
        if (first is null) return null;
        list.Add(first);

        while (PeekTokenIs(TokenType.Comma))
        {
            NextToken();
            NextToken();
            var next = ParseExpression(Precedence.Lowest);
            if (next is null) return null;
            list.Add(next);
        }

        if (!ExpectPeek(end)) return null;

        return list;
    }
}