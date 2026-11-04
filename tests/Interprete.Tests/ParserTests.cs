// Pruebas del parser. Se corren con:  dotnet test
// ToString() del Program muestra la agrupación con paréntesis; Assert.IsType<...> revisa la estructura del árbol.

using System.Collections.Generic;
using Interprete.Ast;
using Interprete.Lexing;
using Interprete.Parsing;
using Xunit;

namespace Interprete.Tests;

public class ParserTests
{
    // ----- Ayudantes -----

    private static Interprete.Ast.Program ParseOk(string input)
    {
        var parser = new Parser(new Lexer(input));
        var program = parser.ParseProgram();
        Assert.Empty(parser.Errors);
        return program;
    }

    private static IReadOnlyList<string> ParseErrors(string input)
    {
        var parser = new Parser(new Lexer(input));
        parser.ParseProgram();
        return parser.Errors;
    }

    private static Expr ParseExpr(string input)
    {
        var program = ParseOk(input);
        Assert.Equal(1, program.Statements.Count);
        var stmt = Assert.IsType<ExpressionStmt>(program.Statements[0]);
        return stmt.Expression;
    }

    // ----- Precedencia y asociatividad -----

    [Theory]
    [InlineData("-a * b", "((-a) * b)")]
    [InlineData("!-a", "(!(-a))")]
    [InlineData("a + b + c", "((a + b) + c)")]
    [InlineData("a + b - c", "((a + b) - c)")]
    [InlineData("a * b * c", "((a * b) * c)")]
    [InlineData("a * b / c", "((a * b) / c)")]
    [InlineData("a + b / c", "(a + (b / c))")]
    [InlineData("a + b * c + d / e - f", "(((a + (b * c)) + (d / e)) - f)")]
    [InlineData("3 + 4; -5 * 5", "(3 + 4)((-5) * 5)")]
    [InlineData("5 > 4 == 3 < 4", "((5 > 4) == (3 < 4))")]
    [InlineData("5 < 4 != 3 > 4", "((5 < 4) != (3 > 4))")]
    [InlineData("3 + 4 * 5 == 3 * 1 + 4 * 5", "((3 + (4 * 5)) == ((3 * 1) + (4 * 5)))")]
    [InlineData("10 % 3 * 2", "((10 % 3) * 2)")]
    [InlineData("2 ** 3 ** 2", "(2 ** (3 ** 2))")]      // ** asocia por la DERECHA
    [InlineData("-2 ** 2", "(-(2 ** 2))")]                // ** gana al prefijo
    [InlineData("1 + (2 + 3) + 4", "((1 + (2 + 3)) + 4)")]
    [InlineData("(5 + 5) * 2", "((5 + 5) * 2)")]
    [InlineData("-(5 + 5)", "(-(5 + 5))")]
    [InlineData("!(true == true)", "(!(true == true))")]
    [InlineData("x = 1 + 2", "x = (1 + 2)")]              // = es la más baja
    public void Operadores_RespetanPrecedenciaYAsociatividad(string input, string expected)
    {
        Assert.Equal(expected, ParseOk(input).ToString());
    }

    [Theory]
    [InlineData("a + add(b * c) + d", "((a + add((b * c))) + d)")]
    [InlineData("add(a, b, 1, 2 * 3, 4 + 5, add(6, 7 * 8))", "add(a, b, 1, (2 * 3), (4 + 5), add(6, (7 * 8)))")]
    [InlineData("add(a + b + c * d / f + g)", "add((((a + b) + ((c * d) / f)) + g))")]
    [InlineData("a * [1, 2, 3, 4][b * c] * d", "((a * ([1, 2, 3, 4][(b * c)])) * d)")]
    [InlineData("add(a * b[2], b[1], 2 * [1, 2][1])", "add((a * (b[2])), (b[1]), (2 * ([1, 2][1])))")]
    [InlineData("myArray[1 + 1]", "(myArray[(1 + 1)])")]
    [InlineData("m[i][j]", "((m[i])[j])")]
    [InlineData("f()", "f()")]
    public void LlamadasEIndices_TienenLaPrecedenciaMasAlta(string input, string expected)
    {
        Assert.Equal(expected, ParseOk(input).ToString());
    }

    // ----- Sentencias -----

    [Fact]
    public void Let_CreaLetStmtConNombreYValor()
    {
        var program = ParseOk("let x = 5;");

        Assert.Equal(1, program.Statements.Count);
        var letStmt = Assert.IsType<LetStmt>(program.Statements[0]);
        Assert.Equal("let", letStmt.TokenLiteral);
        Assert.Equal("x", letStmt.Name.Value);
        var value = Assert.IsType<IntegerLiteral>(letStmt.Value);
        Assert.Equal(5L, value.Value);
        Assert.Equal("let x = 5;", program.ToString());
    }

    [Fact]
    public void VariasSentencias_SeParseanEnOrden()
    {
        var program = ParseOk("let x = 1; let y = 2; x + y");

        Assert.Equal(3, program.Statements.Count);
        Assert.IsType<LetStmt>(program.Statements[0]);
        Assert.IsType<LetStmt>(program.Statements[1]);
        Assert.IsType<ExpressionStmt>(program.Statements[2]);
        Assert.Equal("let x = 1;let y = 2;(x + y)", program.ToString());
    }

    [Fact]
    public void Return_CreaReturnStmt()
    {
        var program = ParseOk("return a + b;");

        var ret = Assert.IsType<ReturnStmt>(program.Statements[0]);
        Assert.IsType<InfixExpr>(ret.ReturnValue);
        Assert.Equal("return (a + b);", program.ToString());
    }

    [Fact]
    public void PuntoYComa_EsOpcional()
    {
        Assert.Equal("(5 + 5)", ParseOk("5 + 5").ToString());
        Assert.Equal("let y = true;", ParseOk("let y = true").ToString());
    }

    [Fact]
    public void ProgramaVacio_NoTieneSentencias()
    {
        Assert.Equal(0, ParseOk("").Statements.Count);
        Assert.Equal(0, ParseOk(";;").Statements.Count);   // sentencias vacías se ignoran
    }

    // ----- Literales y operadores -----

    [Fact]
    public void Literales_TienenElTipoYValorCorrectos()
    {
        Assert.Equal(42L, Assert.IsType<IntegerLiteral>(ParseExpr("42")).Value);
        Assert.True(Assert.IsType<BooleanLiteral>(ParseExpr("true")).Value);
        Assert.False(Assert.IsType<BooleanLiteral>(ParseExpr("false")).Value);
        Assert.Equal("hola", Assert.IsType<StringLiteral>(ParseExpr("\"hola\"")).Value);
        Assert.Equal("x", Assert.IsType<Identifier>(ParseExpr("x")).Value);
    }

    [Fact]
    public void Prefijo_GuardaOperadorYOperando()
    {
        var neg = Assert.IsType<PrefixExpr>(ParseExpr("-5"));
        Assert.Equal("-", neg.Operator);
        Assert.IsType<IntegerLiteral>(neg.Right);

        var bang = Assert.IsType<PrefixExpr>(ParseExpr("!true"));
        Assert.Equal("!", bang.Operator);
    }

    [Theory]
    [InlineData("5 + 3", "+")]
    [InlineData("5 - 3", "-")]
    [InlineData("5 * 3", "*")]
    [InlineData("5 / 3", "/")]
    [InlineData("5 % 3", "%")]
    [InlineData("5 ** 3", "**")]
    [InlineData("5 == 3", "==")]
    [InlineData("5 != 3", "!=")]
    [InlineData("5 < 3", "<")]
    [InlineData("5 > 3", ">")]
    [InlineData("5 <= 3", "<=")]
    [InlineData("5 >= 3", ">=")]
    public void Infijo_GuardaOperadorYAmbosLados(string input, string op)
    {
        var infix = Assert.IsType<InfixExpr>(ParseExpr(input));
        Assert.Equal(op, infix.Operator);
        Assert.Equal(5L, Assert.IsType<IntegerLiteral>(infix.Left).Value);
        Assert.Equal(3L, Assert.IsType<IntegerLiteral>(infix.Right).Value);
    }

    [Fact]
    public void Potencia_AsociaPorLaDerecha()
    {
        // 2 ** 3 ** 2 → 2 ** (3 ** 2): el hijo derecho es el otro **.
        var outer = Assert.IsType<InfixExpr>(ParseExpr("2 ** 3 ** 2"));
        Assert.IsType<IntegerLiteral>(outer.Left);
        Assert.IsType<InfixExpr>(outer.Right);
    }

    [Fact]
    public void Suma_AsociaPorLaIzquierda()
    {
        // 1 + 2 + 3 → (1 + 2) + 3: el hijo izquierdo es el otro +.
        var outer = Assert.IsType<InfixExpr>(ParseExpr("1 + 2 + 3"));
        Assert.IsType<InfixExpr>(outer.Left);
        Assert.IsType<IntegerLiteral>(outer.Right);
    }

    // ----- Asignación -----

    [Fact]
    public void Asignacion_AVariable()
    {
        var assign = Assert.IsType<AssignExpr>(ParseExpr("x = 5"));
        Assert.Equal("x", Assert.IsType<Identifier>(assign.Left).Value);
        Assert.IsType<IntegerLiteral>(assign.Value);
    }

    [Fact]
    public void Asignacion_APosicionDeArreglo()
    {
        var assign = Assert.IsType<AssignExpr>(ParseExpr("arr[0] = 5"));
        Assert.IsType<IndexExpr>(assign.Left);
        Assert.Equal("(arr[0]) = 5", assign.ToString());
    }

    [Fact]
    public void Asignacion_AsociaPorLaDerecha()
    {
        // a = b = 3 → a = (b = 3)
        var outer = Assert.IsType<AssignExpr>(ParseExpr("a = b = 3"));
        Assert.Equal("a", Assert.IsType<Identifier>(outer.Left).Value);
        var inner = Assert.IsType<AssignExpr>(outer.Value);
        Assert.Equal("b", Assert.IsType<Identifier>(inner.Left).Value);
    }

    // ----- Condicionales -----

    [Fact]
    public void If_SinElse_AlternativeEsNull()
    {
        var ifExpr = Assert.IsType<IfExpr>(ParseExpr("if (x < y) { x }"));
        Assert.IsType<InfixExpr>(ifExpr.Condition);
        Assert.Equal(1, ifExpr.Consequence.Statements.Count);
        Assert.Null(ifExpr.Alternative);
    }

    [Fact]
    public void If_ConElse_TieneAlternative()
    {
        var ifExpr = Assert.IsType<IfExpr>(ParseExpr("if (x < y) { x } else { y }"));
        Assert.NotNull(ifExpr.Alternative);
        Assert.Equal("if (x < y) { x } else { y }", ifExpr.ToString());
    }

    [Fact]
    public void If_ParentesisSonOpcionales()
    {
        Assert.Equal("if (x > 1) { 1 }", ParseOk("if x > 1 { 1 }").ToString());
    }

    [Fact]
    public void Elif_SeRepresentaComoIfAnidadoEnElAlternative()
    {
        // if (a) {1} elif (b) {2} else {3} ≡ if (a) {1} else { if (b) {2} else {3} }
        var outer = Assert.IsType<IfExpr>(ParseExpr("if (a) { 1 } elif (b) { 2 } else { 3 }"));

        Assert.NotNull(outer.Alternative);
        Assert.Equal(1, outer.Alternative!.Statements.Count);
        var wrapper = Assert.IsType<ExpressionStmt>(outer.Alternative.Statements[0]);
        var nested = Assert.IsType<IfExpr>(wrapper.Expression);
        Assert.Equal("b", Assert.IsType<Identifier>(nested.Condition).Value);
        Assert.NotNull(nested.Alternative);   // el else final

        Assert.Equal("if a { 1 } else { if b { 2 } else { 3 } }", outer.ToString());
    }

    [Fact]
    public void Elif_EncadenaVariosSinElse()
    {
        var program = ParseOk("if (a) { 1 } elif (b) { 2 } elif (c) { 3 }");
        Assert.Equal("if a { 1 } else { if b { 2 } else { if c { 3 } } }", program.ToString());
    }

    // ----- Ciclos -----

    [Fact]
    public void While_TieneCondicionYCuerpo()
    {
        var loop = Assert.IsType<WhileExpr>(ParseExpr("while (x > 0) { x = x - 1 }"));
        Assert.IsType<InfixExpr>(loop.Condition);
        Assert.Equal(1, loop.Body.Statements.Count);
        Assert.Equal("while (x > 0) { x = (x - 1) }", loop.ToString());
    }

    [Fact]
    public void For_TieneInicioCondicionActualizacionYCuerpo()
    {
        var loop = Assert.IsType<ForExpr>(ParseExpr("for (let i = 0; i < 10; i = i + 1) { i }"));

        Assert.IsType<LetStmt>(loop.Init);              // el inicio es una SENTENCIA
        Assert.IsType<InfixExpr>(loop.Condition);
        Assert.IsType<AssignExpr>(loop.Update);         // la actualización es una EXPRESIÓN
        Assert.Equal(1, loop.Body.Statements.Count);
        Assert.Equal("for (let i = 0; (i < 10); i = (i + 1)) { i }", loop.ToString());
    }

    [Fact]
    public void For_PartesOmitidasQuedanEnNull()
    {
        var infinite = Assert.IsType<ForExpr>(ParseExpr("for (;;) { 1 }"));
        Assert.Null(infinite.Init);
        Assert.Null(infinite.Condition);
        Assert.Null(infinite.Update);

        var noUpdate = Assert.IsType<ForExpr>(ParseExpr("for (i = 0; i < 3;) { i }"));
        Assert.NotNull(noUpdate.Init);
        Assert.NotNull(noUpdate.Condition);
        Assert.Null(noUpdate.Update);
    }

    // ----- Funciones -----

    [Fact]
    public void Function_GuardaParametrosYCuerpo()
    {
        var program = ParseOk("let f = function(a, b) { return a + b; };");

        var letStmt = Assert.IsType<LetStmt>(program.Statements[0]);
        var fn = Assert.IsType<FunctionLiteral>(letStmt.Value);
        Assert.Equal(2, fn.Parameters.Count);
        Assert.Equal("a", fn.Parameters[0].Value);
        Assert.Equal("b", fn.Parameters[1].Value);
        Assert.Equal(1, fn.Body.Statements.Count);
        Assert.Equal("let f = function(a, b) { return (a + b); };", program.ToString());
    }

    [Fact]
    public void Function_SinParametros()
    {
        var fn = Assert.IsType<FunctionLiteral>(ParseExpr("function() { 1 }"));
        Assert.Equal(0, fn.Parameters.Count);
    }

    [Fact]
    public void Call_GuardaFuncionYArgumentos()
    {
        var call = Assert.IsType<CallExpr>(ParseExpr("suma(1, 2 * 3, x)"));
        Assert.Equal("suma", Assert.IsType<Identifier>(call.Function).Value);
        Assert.Equal(3, call.Arguments.Count);
        Assert.IsType<IntegerLiteral>(call.Arguments[0]);
        Assert.IsType<InfixExpr>(call.Arguments[1]);
        Assert.IsType<Identifier>(call.Arguments[2]);
    }

    [Fact]
    public void Call_SePuedeLlamarUnLiteralDeFuncion()
    {
        var call = Assert.IsType<CallExpr>(ParseExpr("function(x) { x * 2 }(5)"));
        Assert.IsType<FunctionLiteral>(call.Function);
        Assert.Equal(1, call.Arguments.Count);
        Assert.Equal("function(x) { (x * 2) }(5)", call.ToString());
    }

    // ----- Estructuras de datos -----

    [Fact]
    public void Array_GuardaSusElementosEnOrden()
    {
        var array = Assert.IsType<ArrayLiteral>(ParseExpr("[1, 2 * 2, 3 + 3]"));
        Assert.Equal(3, array.Elements.Count);
        Assert.Equal("[1, (2 * 2), (3 + 3)]", array.ToString());
    }

    [Fact]
    public void Array_Vacio()
    {
        Assert.Equal(0, Assert.IsType<ArrayLiteral>(ParseExpr("[]")).Elements.Count);
    }

    [Fact]
    public void Index_GuardaElementoEIndice()
    {
        var index = Assert.IsType<IndexExpr>(ParseExpr("arr[1 + 1]"));
        Assert.Equal("arr", Assert.IsType<Identifier>(index.Left).Value);
        Assert.IsType<InfixExpr>(index.Index);
    }

    [Fact]
    public void Hash_ConservaElOrdenDeLasParejas()
    {
        var hash = Assert.IsType<HashLiteral>(ParseExpr("{\"one\": 1, \"two\": 2}"));
        Assert.Equal(2, hash.Pairs.Count);
        Assert.Equal("one", Assert.IsType<StringLiteral>(hash.Pairs[0].Key).Value);
        Assert.Equal(1L, Assert.IsType<IntegerLiteral>(hash.Pairs[0].Value).Value);
        Assert.Equal("two", Assert.IsType<StringLiteral>(hash.Pairs[1].Key).Value);
        Assert.Equal("{\"one\": 1, \"two\": 2}", hash.ToString());
    }

    [Fact]
    public void Hash_ClavesYValoresPuedenSerExpresiones()
    {
        Assert.Equal("{\"a\": (1 + 2), b: (3 * 4)}", ParseOk("{\"a\": 1 + 2, b: 3 * 4}").ToString());
    }

    [Fact]
    public void Hash_Vacio()
    {
        Assert.Equal(0, Assert.IsType<HashLiteral>(ParseExpr("{}")).Pairs.Count);
    }

    // ----- Programas completos y posiciones -----

    [Fact]
    public void ProgramaMultilinea_ConRecursionYCiclos_SeParseaSinErrores()
    {
        var source = """
            let fibonacci = function(n) {
                if (n <= 1) {
                    return n;
                }
                return fibonacci(n - 1) + fibonacci(n - 2);
            };

            let suma = function(lista) {
                let total = 0;
                for (let i = 0; i < 3; i = i + 1) {
                    total = total + lista[i];
                }
                return total;
            };

            let x = 10;
            while (x > 0) {
                x = x - 3;
            }
            """;

        var program = ParseOk(source);
        Assert.Equal(4, program.Statements.Count);
    }

    [Fact]
    public void Nodos_GuardanLaPosicionDeSuToken()
    {
        // Se asume línea y columna desde 1 (contrato con el lexer).
        var program = ParseOk("let x = 5;\nlet y = 6;");

        var first = Assert.IsType<LetStmt>(program.Statements[0]);
        var second = Assert.IsType<LetStmt>(program.Statements[1]);
        Assert.Equal(1, first.Token.Line);
        Assert.Equal(1, first.Token.Column);
        Assert.Equal(2, second.Token.Line);
        Assert.Equal(1, second.Token.Column);
    }

    // ----- Errores sintácticos -----

    [Theory]
    [InlineData("let = 5;", "se esperaba un identificador")]
    [InlineData("let x 5;", "se esperaba '='")]
    [InlineData("(1 + 2", "se esperaba ')'")]
    [InlineData("[1, 2", "se esperaba ']'")]
    [InlineData("f(1, 2", "se esperaba ')'")]
    [InlineData("if (x) { 1", "bloque sin cerrar: falta '}'")]
    [InlineData("if (x) 1", "se esperaba '{'")]
    [InlineData("if (a) { 1 } else 2", "se esperaba '{'")]
    [InlineData("function(1) {}", "se esperaba un identificador")]
    [InlineData("{\"a\" 1}", "se esperaba ':'")]
    [InlineData("for (let i = 0 i < 3; i = i + 1) {}", "se esperaba ';'")]
    [InlineData("5 = 3", "no se puede asignar a 5")]
    [InlineData("a + b = 3", "no se puede asignar a (a + b)")]
    [InlineData(")", "no se puede iniciar una expresión con ')'")]
    [InlineData("* 3", "no se puede iniciar una expresión con '*'")]
    [InlineData("let x = ;", "no se puede iniciar una expresión con ';'")]
    [InlineData("@", "no se puede iniciar una expresión con '@'")]
    [InlineData("99999999999999999999", "no se pudo convertir")]
    public void EntradaInvalida_ReportaElErrorEsperado(string input, string expectedMessage)
    {
        var errors = ParseErrors(input);

        Assert.NotEmpty(errors);
        Assert.Contains(expectedMessage, errors[0]);
    }

    [Fact]
    public void Errores_IncluyenLineaYColumna()
    {
        // El '=' inesperado está en la línea 2, columna 5.
        var errors = ParseErrors("let a = 1;\nlet = 5;");

        Assert.Contains("línea 2, columna 5", errors[0]);
    }

    [Fact]
    public void Errores_TienenSiempreElMismoFormato()
    {
        foreach (var error in ParseErrors("let = 5; let y 3; (1 + 2"))
            Assert.Matches(@"^Error de sintaxis \(línea \d+, columna \d+\): ", error);
    }

    [Fact]
    public void Parser_NoSeDetieneEnElPrimerError()
    {
        var errors = ParseErrors("let = 5;\nlet y 3;");

        Assert.True(errors.Count >= 2, "debe reportar errores de las dos líneas");
    }

    [Fact]
    public void SentenciaInvalida_NoApareceEnElArbol()
    {
        // Si una sentencia falla, no se crea su nodo.
        var parser = new Parser(new Lexer("let x = ;"));
        var program = parser.ParseProgram();

        Assert.NotEmpty(parser.Errors);
        Assert.Equal(0, program.Statements.Count);
    }
}