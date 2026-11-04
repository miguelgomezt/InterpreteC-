namespace Interprete.Errors;

// Muestra los errores al usuario: tipo, posición y la línea del código con una marca (^).
public static class ErrorReporter
{
    // Muestra un error. Si se pasa el código fuente, señala dónde ocurrió.
    public static void Report(InterpreterException error, string? source = null)
    {
        var colorAnterior = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;

        Console.WriteLine(error.ToString());

        string? linea = GetLine(source, error.Line);
        if (linea != null && error.Column > 0)
        {
            Console.WriteLine($"    {linea}");
            Console.WriteLine($"    {new string(' ', error.Column - 1)}^");
        }

        Console.ForegroundColor = colorAnterior;
    }

    // Muestra varios errores (por ejemplo, todos los que encontró el parser).
    public static void Report(IEnumerable<InterpreterException> errors, string? source = null)
    {
        foreach (var error in errors)
        {
            Report(error, source);
        }
    }

    // Devuelve el texto de la línea indicada (empieza en 1), o null si no existe.
    private static string? GetLine(string? source, int line)
    {
        if (source == null || line <= 0) return null;

        string[] lineas = source.Replace("\r\n", "\n").Split('\n');
        return line <= lineas.Length ? lineas[line - 1] : null;
    }
}