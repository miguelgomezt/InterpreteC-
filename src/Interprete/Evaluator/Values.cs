// Valores que produce el evaluador en tiempo de ejecución (equivale a objects.go).
namespace Interprete.Evaluation;

/// <summary>Raíz: todo valor del lenguaje es un Value.</summary>
public abstract record Value
{
    public abstract string TypeName { get; }      // para mensajes de error: "ENTERO", "CADENA"...
    public abstract string Inspect();             // cómo se ve al imprimirlo
    public sealed override string ToString() => Inspect();

    // Solo false y null son falsos; 0, "" y [] son verdaderos.
    public bool IsTruthy => this is not NullValue && this is not BooleanValue { Value: false };
}

public sealed record IntegerValue(long Value) : Value
{
    public override string TypeName => "ENTERO";
    public override string Inspect() => Value.ToString();
}

public sealed record BooleanValue(bool Value) : Value
{
    public static readonly BooleanValue True = new(true);
    public static readonly BooleanValue False = new(false);
    public static BooleanValue From(bool b) => b ? True : False;

    public override string TypeName => "BOOLEANO";
    public override string Inspect() => Value ? "true" : "false";
}

public sealed record NullValue : Value
{
    public static readonly NullValue Instance = new();
    private NullValue() { }

    public override string TypeName => "NULO";
    public override string Inspect() => "null";
}

public sealed record StringValue(string Value) : Value
{
    public override string TypeName => "CADENA";
    public override string Inspect() => Value;
}

/// <summary>Lista mutable para poder hacer arr[0] = 5.</summary>
public sealed record ArrayValue(List<Value> Elements) : Value
{
    public override string TypeName => "ARREGLO";
    public override string Inspect() => $"[{string.Join(", ", Elements)}]";
}

public sealed record HashValue(Dictionary<Value, Value> Pairs) : Value
{
    public override string TypeName => "DICCIONARIO";
    public override string Inspect() =>
        "{" + string.Join(", ", Pairs.Select(p => $"{p.Key}: {p.Value}")) + "}";

    // Solo estos tipos pueden ser clave.
    public static bool IsHashable(Value key) => key is IntegerValue or StringValue or BooleanValue;
}

/// <summary>Envuelve el valor de un return para que suba por los bloques hasta la función.</summary>
public sealed record ReturnValue(Value Inner) : Value
{
    public override string TypeName => "RETORNO";
    public override string Inspect() => Inner.Inspect();
}

/// <summary>Función integrada escrita en C# (las define Miguel en Builtins.cs).</summary>
public sealed record BuiltinValue(string Name, Func<IReadOnlyList<Value>, Value> Function) : Value
{
    public override string TypeName => "INTEGRADA";
    public override string Inspect() => $"<integrada {Name}>";
}