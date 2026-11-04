namespace Interprete.Errors;

//Etapa en la que se detectó el error.
public enum ErrorKind
{
    Lexico, 
    Sintactico, 
    Ejecucion
}

//Error del interprete con su tipo y posición en el código fuente.
public class InterpreterException : Exception{
    public ErrorKind Kind { get; }
    public int Line { get; }
    public int Column { get; }

    public InterpreterException(ErrorKind kind, string message, int line = 0, int column = 0)
    : base (message)
    {
        Kind = kind; 
        Line = line; 
        Column = column; 
    }

    public override string ToString()
    {
        string etapa = Kind switch
        {
            ErrorKind.Lexico => "Error lexico",
            ErrorKind.Sintactico => "Error sintactico",
            _=> "Error de ejecucion"
        };
        return Line > 0
             ? $"{etapa} (línea {Line}, columna {Column}): {Message}"
            : $"{etapa}: {Message}";
    }
}