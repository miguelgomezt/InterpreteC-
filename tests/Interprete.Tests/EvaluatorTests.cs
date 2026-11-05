// Pruebas del evaluador. Se corren con:  dotnet test
// Cada prueba: código fuente → Lexer → Parser → Evaluator → se revisa el Value resultante.

using Interprete.Ast;
using Interprete.Errors;
using Interprete.Evaluation;
using Interprete.Lexing;
using Interprete.Parsing;
using Xunit;

namespace Interprete.Tests;

public class EvaluatorTests
{
    // ----- Ayudantes -----

    // Nombre completo: 'Environment' también existe en System.
    private static Value Run(string input)
    {
        var parser = new Parser(new Lexer(input));
        var program = parser.ParseProgram();
        Assert.Empty(parser.Errors);
        return Evaluator.Eval(program, new Interprete.Evaluation.Environment());
    }

    private static InterpreterException RunError(string input) =>
        Assert.Throws<InterpreterException>(() => Run(input));

    private static void AssertInteger(long expected, Value value) =>
        Assert.Equal(expected, Assert.IsType<IntegerValue>(value).Value);

    private static void AssertString(string expected, Value value) =>
        Assert.Equal(expected, Assert.IsType<StringValue>(value).Value);

    // ----- Aritmética -----

    [Theory]
    [InlineData("5", 5)]
    [InlineData("-5", -5)]
    [InlineData("2 + 3 * 4", 14)]
    [InlineData("(2 + 3) * 4", 20)]
    [InlineData("7 - 10", -3)]
    [InlineData("10 / 3", 3)]          // división entera
    [InlineData("10 % 3", 1)]
    [InlineData("2 ** 10", 1024)]
    [InlineData("2 ** 3 ** 2", 512)]   // 2 ** 9
    [InlineData("-2 ** 2", -4)]        // -(2 ** 2)
    public void Enteros_SeEvaluanConPrecedencia(string input, long expected)
    {
        AssertInteger(expected, Run(input));
    }

    // ----- Booleanos y comparaciones -----

    [Theory]
    [InlineData("1 < 2", true)]
    [InlineData("1 >= 2", false)]
    [InlineData("3 <= 3", true)]
    [InlineData("1 == 1", true)]
    [InlineData("1 != 1", false)]
    [InlineData("true == true", true)]
    [InlineData("true != false", true)]
    [InlineData("\"a\" == \"a\"", true)]
    [InlineData("1 == true", false)]   // tipos distintos nunca son iguales
    [InlineData("!true", false)]
    [InlineData("!5", false)]
    [InlineData("!!0", true)]          // 0 es verdadero
    public void Booleanos_YComparaciones(string input, bool expected)
    {
        Assert.Equal(expected, Assert.IsType<BooleanValue>(Run(input)).Value);
    }

    [Theory]
    [InlineData("if (0) { 1 } else { 2 }", 1)]
    [InlineData("if (\"\") { 1 } else { 2 }", 1)]
    [InlineData("if ([]) { 1 } else { 2 }", 1)]
    [InlineData("if (false) { 1 } else { 2 }", 2)]
    public void SoloFalseYNull_SonFalsos(string input, long expected)
    {
        AssertInteger(expected, Run(input));
    }

    // ----- Cadenas -----

    [Fact]
    public void Cadenas_SeConcatenanConMas()
    {
        AssertString("Hola mundo", Run("\"Hola\" + \" \" + \"mundo\""));
    }

    // ----- Variables -----

    [Fact]
    public void Let_YAsignacion()
    {
        AssertInteger(6, Run("let x = 5; x = x + 1; x"));
    }

    // ----- Condicionales -----

    [Theory]
    [InlineData(-1, "negativo")]
    [InlineData(0, "cero")]
    [InlineData(7, "positivo")]
    public void IfElifElse_TomaLaRamaCorrecta(long n, string expected)
    {
        var source = "let n = " + n + "; " +
                     "if (n < 0) { \"negativo\" } elif (n == 0) { \"cero\" } else { \"positivo\" }";
        AssertString(expected, Run(source));
    }

    [Fact]
    public void If_SinElseYCondicionFalsa_DevuelveNull()
    {
        Assert.IsType<NullValue>(Run("if (false) { 1 }"));
    }

    // ----- Ciclos -----

    [Fact]
    public void While_SumaDel1Al10()
    {
        AssertInteger(55, Run("let i = 1; let total = 0; while (i <= 10) { total = total + i; i = i + 1; } total"));
    }

    [Fact]
    public void For_SumaDel1Al10()
    {
        AssertInteger(55, Run("let total = 0; for (let i = 1; i <= 10; i = i + 1) { total = total + i; } total"));
    }

    // ----- Funciones y return -----

    [Fact]
    public void Funcion_RecibeArgumentosYRetorna()
    {
        AssertInteger(7, Run("let suma = function(a, b) { return a + b; }; suma(3, 4)"));
    }

    [Fact]
    public void Funcion_SinReturn_DevuelveLaUltimaExpresion()
    {
        AssertInteger(10, Run("let doble = function(x) { x * 2 }; doble(5)"));
    }

    [Fact]
    public void Return_SaleDeLaFuncionDesdeDentroDeUnCiclo()
    {
        AssertInteger(7, Run("let f = function() { while (true) { return 7; } }; f()"));
    }

    [Fact]
    public void Return_SaleDesdeIfsAnidados()
    {
        var source = """
            let tamano = function(n) {
                if (n > 0) {
                    if (n > 5) { return "grande"; }
                    return "pequeno";
                }
                return "otro";
            };
            tamano(9) + "-" + tamano(2) + "-" + tamano(0)
            """;
        AssertString("grande-pequeno-otro", Run(source));
    }

    // ----- Recursión -----

    [Fact]
    public void Recursion_Factorial()
    {
        var source = """
            let fact = function(n) {
                if (n <= 1) { return 1; }
                return n * fact(n - 1);
            };
            fact(5)
            """;
        AssertInteger(120, Run(source));
    }

    [Fact]
    public void Recursion_Fibonacci()
    {
        var source = """
            let fib = function(n) {
                if (n <= 1) { return n; }
                return fib(n - 1) + fib(n - 2);
            };
            fib(15)
            """;
        AssertInteger(610, Run(source));
    }

    // ----- Ámbitos -----

    [Fact]
    public void LetDentroDeFuncion_NoCambiaLaVariableDeAfuera()
    {
        AssertInteger(1, Run("let x = 1; let f = function() { let x = 99; x }; f(); x"));
    }

    [Fact]
    public void VariableDeAdentro_TapaALaDeAfuera()
    {
        AssertInteger(21, Run("let x = 1; let f = function() { let x = 2; x * 10 }; f() + x"));
    }

    [Fact]
    public void Asignacion_ModificaLaVariableDeAfuera()
    {
        AssertInteger(2, Run("let c = 0; let inc = function() { c = c + 1; }; inc(); inc(); c"));
    }

    [Fact]
    public void Parametro_TapaAVariableGlobalConElMismoNombre()
    {
        AssertInteger(105, Run("let n = 100; let f = function(n) { n + 1 }; f(4) + n"));
    }

    // ----- Closures -----

    [Fact]
    public void Closure_RecuerdaElEntornoDondeNacio()
    {
        var source = """
            let sumador = function(x) { function(y) { x + y } };
            let mas2 = sumador(2);
            mas2(3)
            """;
        AssertInteger(5, Run(source));
    }

    [Fact]
    public void Closure_ContadorGuardaSuEstadoEntreLlamadas()
    {
        var source = """
            let crearContador = function() {
                let c = 0;
                function() { c = c + 1; c }
            };
            let contar = crearContador();
            contar();
            contar();
            contar()
            """;
        AssertInteger(3, Run(source));
    }

    [Fact]
    public void Closure_CadaContadorTieneSuPropioEstado()
    {
        var source = """
            let crearContador = function() { let c = 0; function() { c = c + 1; c } };
            let a = crearContador();
            let b = crearContador();
            a(); a(); a();
            b()
            """;
        AssertInteger(1, Run(source));
    }

    // ----- Arreglos -----

    [Fact]
    public void Arreglo_SeIndexaDesdeCero()
    {
        AssertInteger(20, Run("[10, 20, 30][1]"));
    }

    [Fact]
    public void Arreglo_SeModificaPorIndice()
    {
        AssertInteger(40, Run("let a = [1, 2, 3]; a[0] = 10 * 4; a[0]"));
    }

    [Fact]
    public void Arreglo_SeConcatenaConMas()
    {
        var array = Assert.IsType<ArrayValue>(Run("[1] + [2, 3]"));
        Assert.Equal(3, array.Elements.Count);
        Assert.Equal("[1, 2, 3]", array.Inspect());
    }

    // ----- Hashes -----

    [Fact]
    public void Hash_AceptaClavesEnteroCadenaYBooleano()
    {
        AssertInteger(1, Run("let h = {\"a\": 1, 2: \"dos\", true: 3}; h[\"a\"]"));
        AssertString("dos", Run("let h = {\"a\": 1, 2: \"dos\", true: 3}; h[2]"));
        AssertInteger(3, Run("let h = {\"a\": 1, 2: \"dos\", true: 3}; h[true]"));
    }

    [Fact]
    public void Hash_ClaveInexistente_DevuelveNull()
    {
        Assert.IsType<NullValue>(Run("let h = {\"a\": 1}; h[\"b\"]"));
    }

    [Fact]
    public void Hash_SeAgreganClavesConAsignacion()
    {
        AssertInteger(5, Run("let h = {}; h[\"b\"] = 5; h[\"b\"]"));
    }

    // ----- Errores de ejecución -----

    [Theory]
    [InlineData("x", "variable no definida: x")]
    [InlineData("y = 3", "variable no definida: y")]
    [InlineData("5 / 0", "división por cero")]
    [InlineData("5 % 0", "división por cero")]
    [InlineData("1 + true", "tipos incompatibles")]
    [InlineData("\"a\" - \"b\"", "tipos incompatibles")]
    [InlineData("-true", "no se puede aplicar '-'")]
    [InlineData("[1, 2][5]", "índice fuera de rango")]
    [InlineData("let f = function(a) { a }; f(1, 2)", "se esperaban 1 argumentos")]
    [InlineData("let n = 5; n(1)", "no es una función")]
    [InlineData("{[1]: 2}", "no se puede usar ARREGLO como clave")]
    public void ErroresDeEjecucion_TienenMensajeClaro(string input, string expectedMessage)
    {
        var error = RunError(input);
        Assert.Equal(ErrorKind.Ejecucion, error.Kind);
        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void ErrorDeEjecucion_IncluyeLineaYColumna()
    {
        var error = RunError("let a = 1;\nb");
        Assert.Equal(2, error.Line);
        Assert.Equal(1, error.Column);
    }

    [Fact]
    public void VariableDeFuncion_NoExisteAfuera()
    {
        var error = RunError("let f = function() { let local = 1; }; f(); local");
        Assert.Contains("variable no definida: local", error.Message);
    }
}