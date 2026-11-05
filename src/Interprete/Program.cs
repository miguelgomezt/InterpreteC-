using Interprete.Errors;
using Interprete.Evaluation;
using Interprete.Lexing;
using Interprete.Parsing;

// Punto de entrada del intérprete.
//   dotnet run --project src/Interprete                     -> REPL
//   dotnet run --project src/Interprete -- archivo.ssg      -> ejecuta un archivo

if (args.Length > 0)
{
    RunFile(args[0]);
}
else
{
    RunRepl();
}

// ---------------------------------------------------------------------------
// Modo archivo
// ---------------------------------------------------------------------------

static void RunFile(string path)
{
    if (!File.Exists(path))
    {
        PrintError($"No se encontró el archivo: {path}");
        return;
    }

    string source = File.ReadAllText(path);
    Run(source, Mode.Evaluador, new Interprete.Evaluation.Environment());
}

// ---------------------------------------------------------------------------
// Modo interactivo (REPL)
// ---------------------------------------------------------------------------

static void RunRepl()
{
    var mode = Mode.Evaluador;
    var env = new Interprete.Evaluation.Environment();   // las variables duran toda la sesión
    Console.WriteLine("Intérprete — escribe :ayuda para ver los comandos.");

    while (true)
    {
        Console.Write($"{mode.ToString().ToLower()}>> ");
        string? line = Console.ReadLine();

        // null = Ctrl+Z / Ctrl+D (fin de la entrada)
        if (line == null || line.Trim() == ":salir") break;
        if (string.IsNullOrWhiteSpace(line)) continue;

        switch (line.Trim())
        {
            case ":lexer":
                mode = Mode.Lexer;
                break;
            case ":parser":
                mode = Mode.Parser;
                break;
            case ":evaluador":
                mode = Mode.Evaluador;
                break;
            case ":ayuda":
                PrintHelp();
                break;
            default:
                Run(line, mode, env);
                break;
        }
    }
}

// ---------------------------------------------------------------------------
// Ejecución según el modo
// ---------------------------------------------------------------------------

static void Run(string source, Mode mode, Interprete.Evaluation.Environment env)
{
    // 1. Lexer
    if (mode == Mode.Lexer)
    {
        PrintTokens(source);
        return;
    }

    // 2. Parser
    var parser = new Parser(new Lexer(source));
    var program = parser.ParseProgram();

    if (parser.Errors.Count > 0)
    {
        foreach (string error in parser.Errors)
        {
            PrintError(error);
        }
        return;
    }

    if (mode == Mode.Parser)
    {
        Console.WriteLine(program.ToString());
        return;
    }

    // 3. Evaluador
    try
    {
        Value result = Evaluator.Eval(program, env);
        if (result is not NullValue)
        {
            Console.WriteLine(result.Inspect());
        }
    }
    catch (InterpreterException error)
    {
        ErrorReporter.Report(error, source);
    }
}

// Muestra cada token con su tipo, texto y posición.
static void PrintTokens(string source)
{
    var lexer = new Lexer(source);
    Token token;
    do
    {
        token = lexer.NextToken();
        Console.WriteLine($"  {token.Type,-10} '{token.Literal}'  (línea {token.Line}, columna {token.Column})");
    } while (token.Type != TokenType.Eof);
}

static void PrintError(string message)
{
    var colorAnterior = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(message);
    Console.ForegroundColor = colorAnterior;
}

static void PrintHelp()
{
    Console.WriteLine("  :lexer      muestra los tokens de cada línea");
    Console.WriteLine("  :parser     muestra el AST de cada línea");
    Console.WriteLine("  :evaluador  ejecuta cada línea (modo por defecto)");
    Console.WriteLine("  :ayuda      muestra esta ayuda");
    Console.WriteLine("  :salir      cierra el intérprete");
}

// Qué hace el intérprete con cada entrada.
enum Mode
{
    Lexer,
    Parser,
    Evaluador
}