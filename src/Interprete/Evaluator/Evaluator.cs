// Evaluador: recorre el AST y produce valores (equivale a evaluator.go).
// Recibe el AST del parser + un Environment, y devuelve un Value o lanza un error de ejecución.

// Los using van debajo de 'namespace' a propósito (igual que en Parser.cs): así 'Program'
// es Interprete.Ast.Program y no la clase Program del punto de entrada.
namespace Interprete.Evaluation;

using Interprete.Ast;
using Interprete.Errors;
using Interprete.Lexing;

public static class Evaluator
{
    // ===== Entrada: un programa completo =====
    public static Value Eval(Program program, Environment env)
    {
        Value result = NullValue.Instance;
        foreach (var stmt in program.Statements)
        {
            result = EvalStmt(stmt, env);
            if (result is ReturnValue r) return r.Inner;   // return fuera de función: termina el programa
        }
        return result;
    }

    // ===== Sentencias =====
    private static Value EvalStmt(Stmt stmt, Environment env) => stmt switch
    {
        ExpressionStmt s => EvalExpr(s.Expression, env),
        LetStmt s        => EvalLet(s, env),
        ReturnStmt s     => new ReturnValue(EvalExpr(s.ReturnValue, env)),
        BlockStmt s      => EvalBlock(s, env),
        _ => throw Error(stmt.Token, $"sentencia desconocida: {stmt.GetType().Name}")
    };

    private static Value EvalLet(LetStmt s, Environment env)
    {
        var value = EvalExpr(s.Value, env);
        env.Define(s.Name.Value, value);
        return NullValue.Instance;
    }

    private static Value EvalBlock(BlockStmt block, Environment env)
    {
        Value result = NullValue.Instance;
        foreach (var stmt in block.Statements)
        {
            result = EvalStmt(stmt, env);
            if (result is ReturnValue) return result;   // NO se desenvuelve: sigue subiendo hasta la función
        }
        return result;
    }

    // ===== Expresiones =====
    private static Value EvalExpr(Expr expr, Environment env) => expr switch
    {
        IntegerLiteral e => new IntegerValue(e.Value),
        BooleanLiteral e => BooleanValue.From(e.Value),
        StringLiteral e  => new StringValue(e.Value),
        Identifier e     => EvalIdentifier(e, env),
        PrefixExpr e     => EvalPrefix(e, EvalExpr(e.Right, env)),
        InfixExpr e      => EvalInfix(e, EvalExpr(e.Left, env), EvalExpr(e.Right, env)),
        AssignExpr e     => EvalAssign(e, env),
        IfExpr e         => EvalIf(e, env),
        WhileExpr e      => EvalWhile(e, env),
        ForExpr e        => EvalFor(e, env),
        FunctionLiteral e => new FunctionValue(e.Parameters, e.Body, env),
        CallExpr e        => EvalCall(e, env),
        ArrayLiteral e    => new ArrayValue(e.Elements.Select(x => EvalExpr(x, env)).ToList()),
        IndexExpr e       => EvalIndex(e, env),
        HashLiteral e     => EvalHash(e, env),
        _ => throw Error(expr.Token, $"expresión no soportada: {expr.GetType().Name}")
    };

        private static Value EvalIdentifier(Identifier id, Environment env)
    {
        var value = env.Get(id.Value);
        if (value is not null) return value;
        if (Builtins.All.TryGetValue(id.Value, out var builtin)) return builtin;
        throw Error(id.Token, $"variable no definida: {id.Value}");
    }

    private static Value EvalAssign(AssignExpr e, Environment env)
    {
        var value = EvalExpr(e.Value, env);
        if (e.Left is Identifier id)
        {
            if (!env.Assign(id.Value, value))
                throw Error(id.Token, $"variable no definida: {id.Value}");
            return value;
        }
                if (e.Left is IndexExpr ix)
        {
            var target = EvalExpr(ix.Left, env);
            var index = EvalExpr(ix.Index, env);
            switch (target)
            {
                case ArrayValue arr when index is IntegerValue i:
                    arr.Elements[CheckIndex(ix.Token, arr, i.Value)] = value;
                    return value;
                case HashValue hash:
                    CheckHashable(ix.Token, index);
                    hash.Pairs[index] = value;
                    return value;
            }
            throw Error(ix.Token, $"no se puede asignar en {target.TypeName}[{index.TypeName}]");
        }
        throw Error(e.Token, "solo se puede asignar a una variable o a arr[i]");
    }

        // ===== Control de flujo =====
    private static Value EvalIf(IfExpr e, Environment env)
    {
        if (EvalExpr(e.Condition, env).IsTruthy)
            return EvalBlock(e.Consequence, env);
        if (e.Alternative is not null)
            return EvalBlock(e.Alternative, env);   // aquí adentro puede venir otro IfExpr (elif)
        return NullValue.Instance;
    }

    private static Value EvalWhile(WhileExpr e, Environment env)
    {
        while (EvalExpr(e.Condition, env).IsTruthy)
        {
            var result = EvalBlock(e.Body, env);
            if (result is ReturnValue) return result;   // un return dentro del ciclo sale de la función
        }
        return NullValue.Instance;
    }

    private static Value EvalFor(ForExpr e, Environment env)
    {
        if (e.Init is not null) EvalStmt(e.Init, env);

        while (e.Condition is null || EvalExpr(e.Condition, env).IsTruthy)
        {
            var result = EvalBlock(e.Body, env);
            if (result is ReturnValue) return result;
            if (e.Update is not null) EvalExpr(e.Update, env);
        }
        return NullValue.Instance;
    }

        // ===== Funciones =====
    private static Value EvalCall(CallExpr e, Environment env)
    {
        var function = EvalExpr(e.Function, env);
        var args = e.Arguments.Select(a => EvalExpr(a, env)).ToList();

        switch (function)
        {
            case FunctionValue f:
                if (args.Count != f.Parameters.Count)
                    throw Error(e.Token, $"se esperaban {f.Parameters.Count} argumentos y llegaron {args.Count}");

                var callEnv = new Environment(f.Env);          // ámbito nuevo; su "afuera" es donde nació la función
                for (int i = 0; i < args.Count; i++)
                    callEnv.Define(f.Parameters[i].Value, args[i]);

                var result = EvalBlock(f.Body, callEnv);
                return result is ReturnValue r ? r.Inner : result;   // aquí se desenvuelve el return

            case BuiltinValue b:
                try { return b.Function(args); }
                catch (InterpreterException ex) when (ex.Line == 0)
                {
                    throw Error(e.Token, ex.Message);           // le ponemos la posición de la llamada
                }

            default:
                throw Error(e.Token, $"no es una función: {function.TypeName}");
        }
    }

    // ===== Arreglos y hashes =====
    private static Value EvalIndex(IndexExpr e, Environment env)
    {
        var left = EvalExpr(e.Left, env);
        var index = EvalExpr(e.Index, env);

        switch (left)
        {
            case ArrayValue arr when index is IntegerValue i:
                return arr.Elements[CheckIndex(e.Token, arr, i.Value)];
            case HashValue hash:
                CheckHashable(e.Token, index);
                return hash.Pairs.TryGetValue(index, out var v) ? v : NullValue.Instance;
        }
        throw Error(e.Token, $"no se puede indexar {left.TypeName} con {index.TypeName}");
    }

    private static Value EvalHash(HashLiteral e, Environment env)
    {
        var pairs = new Dictionary<Value, Value>();
        foreach (var p in e.Pairs)
        {
            var key = EvalExpr(p.Key, env);
            CheckHashable(p.Key.Token, key);
            pairs[key] = EvalExpr(p.Value, env);
        }
        return new HashValue(pairs);
    }

    private static int CheckIndex(Token token, ArrayValue arr, long i)
    {
        if (i < 0 || i >= arr.Elements.Count)
            throw Error(token, $"índice fuera de rango: {i} (tamaño {arr.Elements.Count})");
        return (int)i;
    }

    private static void CheckHashable(Token token, Value key)
    {
        if (!HashValue.IsHashable(key))
            throw Error(token, $"no se puede usar {key.TypeName} como clave");
    }

    // ===== Operadores =====
    private static Value EvalPrefix(PrefixExpr e, Value right) => e.Operator switch
    {
        "!" => BooleanValue.From(!right.IsTruthy),
        "-" when right is IntegerValue i => new IntegerValue(-i.Value),
        _ => throw Error(e.Token, $"no se puede aplicar '{e.Operator}' a {right.TypeName}")
    };

    private static Value EvalInfix(InfixExpr e, Value left, Value right)
    {
        if (left is IntegerValue li && right is IntegerValue ri)
            return EvalIntegerInfix(e, li.Value, ri.Value);

        if (left is StringValue ls && right is StringValue rs && e.Operator == "+")
            return new StringValue(ls.Value + rs.Value);

        if (left is ArrayValue la && right is ArrayValue ra && e.Operator == "+")
            return new ArrayValue([.. la.Elements, .. ra.Elements]);

        return e.Operator switch
        {
            "==" => BooleanValue.From(left.Equals(right)),
            "!=" => BooleanValue.From(!left.Equals(right)),
            _ => throw Error(e.Token,
                    $"tipos incompatibles: {left.TypeName} {e.Operator} {right.TypeName}")
        };
    }

    private static Value EvalIntegerInfix(InfixExpr e, long l, long r) => e.Operator switch
    {
        "+"  => new IntegerValue(l + r),
        "-"  => new IntegerValue(l - r),
        "*"  => new IntegerValue(l * r),
        "/"  => r == 0 ? throw Error(e.Token, "división por cero") : new IntegerValue(l / r),
        "%"  => r == 0 ? throw Error(e.Token, "división por cero") : new IntegerValue(l % r),
        "**" => r < 0  ? throw Error(e.Token, "exponente negativo") : new IntegerValue(Power(l, r)),
        "<"  => BooleanValue.From(l < r),
        ">"  => BooleanValue.From(l > r),
        "<=" => BooleanValue.From(l <= r),
        ">=" => BooleanValue.From(l >= r),
        "==" => BooleanValue.From(l == r),
        "!=" => BooleanValue.From(l != r),
        _ => throw Error(e.Token, $"operador desconocido: ENTERO {e.Operator} ENTERO")
    };

    private static long Power(long b, long exp)
    {
        long result = 1;
        for (long i = 0; i < exp; i++) result *= b;
        return result;
    }

    // ===== Errores =====
    private static InterpreterException Error(Token token, string message) =>
        new(ErrorKind.Ejecucion, message, token.Line, token.Column);
}