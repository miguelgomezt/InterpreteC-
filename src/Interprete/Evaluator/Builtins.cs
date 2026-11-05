// Funciones integradas. El evaluador las busca aquí por nombre.
using Interprete.Errors;

namespace Interprete.Evaluation;

public static class Builtins
{
    public static readonly Dictionary<string, BuiltinValue> All = new()
    {
        ["imprimir"] = new("imprimir", Imprimir),
        ["longitud"] = new("longitud", Longitud),
        ["texto"] = new("texto", Texto),
        ["entero"] = new("entero", Entero),
    };

    // imprimir(a, b, ...): muestra los valores separados por espacio.
    private static Value Imprimir(IReadOnlyList<Value> args)
    {
        Console.WriteLine(string.Join(" ", args.Select(a => a.Inspect())));
        return NullValue.Instance;
    }

    // longitud(x): tamaño de una cadena, un arreglo o un diccionario.
    private static Value Longitud(IReadOnlyList<Value> args)
    {
        CheckArgs("longitud", args, 1);

        return args[0] switch
        {
            StringValue s => new IntegerValue(s.Value.Length),
            ArrayValue a => new IntegerValue(a.Elements.Count),
            HashValue h => new IntegerValue(h.Pairs.Count),
            var otro => throw Error($"longitud no acepta {otro.TypeName}")
        };
    }

    // texto(x): convierte cualquier valor a cadena.
    private static Value Texto(IReadOnlyList<Value> args)
    {
        CheckArgs("texto", args, 1);
        return new StringValue(args[0].Inspect());
    }

    // entero(x): convierte una cadena con dígitos a entero.
    private static Value Entero(IReadOnlyList<Value> args)
    {
        CheckArgs("entero", args, 1);

        return args[0] switch
        {
            IntegerValue i => i,
            StringValue s when long.TryParse(s.Value.Trim(), out long n) => new IntegerValue(n),
            StringValue s => throw Error($"no se puede convertir \"{s.Value}\" a entero"),
            var otro => throw Error($"entero no acepta {otro.TypeName}")
        };
    }

    // Verifica la cantidad de argumentos.
    private static void CheckArgs(string name, IReadOnlyList<Value> args, int expected)
    {
        if (args.Count != expected)
        {
            throw Error($"{name} espera {expected} argumento(s) y recibió {args.Count}");
        }
    }

    // Sin línea ni columna: el evaluador le pone la posición de la llamada.
    private static InterpreterException Error(string message) =>
        new(ErrorKind.Ejecucion, message);
}