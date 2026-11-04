using Interprete.Lexing;

namespace Interprete.Tests;

// Pruebas del analizador léxico: entradas válidas y entradas con errores.
public class LexerTests
{
    // Lee todos los tokens de una entrada, incluido el Eof final.
    private static List<Token> Tokenize(string input)
    {
        var lexer = new Lexer(input);
        var tokens = new List<Token>();
        Token token;
        do
        {
            token = lexer.NextToken();
            tokens.Add(token);
        } while (token.Type != TokenType.Eof);
        return tokens;
    }

    // Solo los tipos, para comparar más fácil.
    private static List<TokenType> Types(string input) =>
        Tokenize(input).Select(t => t.Type).ToList();

    [Fact]
    public void ReconoceDeclaracionLet()
    {
        var tokens = Tokenize("let x = 5;");

        Assert.Equal(
            new[] { TokenType.Let, TokenType.Ident, TokenType.Assign, TokenType.Int, TokenType.Semicolon, TokenType.Eof },
            tokens.Select(t => t.Type));
        Assert.Equal("x", tokens[1].Literal);
        Assert.Equal("5", tokens[3].Literal);
    }

    [Fact]
    public void ReconoceOperadoresDeUnCaracter()
    {
        Assert.Equal(
            new[] { TokenType.Plus, TokenType.Minus, TokenType.Asterisk, TokenType.Slash, TokenType.Mod,
             TokenType.Lt, TokenType.Gt, TokenType.Bang, TokenType.Assign, TokenType.Eof },
            Types("+ - * / % < > ! ="));
    }

    [Fact]
    public void ReconoceOperadoresDeDosCaracteres()
    {
        var tokens = Tokenize("== != <= >= **");

        Assert.Equal(
            new[] { TokenType.Eq, TokenType.NotEq, TokenType.Lte, TokenType.Gte, TokenType.Pow, TokenType.Eof },
            tokens.Select(t => t.Type));
        Assert.Equal("**", tokens[4].Literal);
    }

    [Fact]
    public void ReconoceDelimitadores()
    {
        Assert.Equal(
            new[] { TokenType.LParen, TokenType.RParen, TokenType.LBrace, TokenType.RBrace,
             TokenType.LBracket, TokenType.RBracket, TokenType.Comma, TokenType.Semicolon,
             TokenType.Colon, TokenType.Eof },
            Types("( ) { } [ ] , ; :"));
    }

    [Theory]
    [InlineData("function", TokenType.Function)]
    [InlineData("let", TokenType.Let)]
    [InlineData("true", TokenType.True)]
    [InlineData("false", TokenType.False)]
    [InlineData("if", TokenType.If)]
    [InlineData("elif", TokenType.Elif)]
    [InlineData("else", TokenType.Else)]
    [InlineData("return", TokenType.Return)]
    [InlineData("for", TokenType.For)]
    [InlineData("while", TokenType.While)]
    [InlineData("edad", TokenType.Ident)]
    [InlineData("mi_variable2", TokenType.Ident)]
    public void DistinguePalabrasReservadasDeIdentificadores(string input, TokenType esperado)
    {
        Assert.Equal(esperado, Tokenize(input)[0].Type);
    }

    [Fact]
    public void ReconoceCadenas()
    {
        var token = Tokenize("\"hola mundo\"")[0];

        Assert.Equal(TokenType.String, token.Type);
        Assert.Equal("hola mundo", token.Literal);
    }

    [Fact]
    public void IgnoraComentariosYEspacios()
    {
        Assert.Equal(
            new[] { TokenType.Let, TokenType.Ident, TokenType.Assign, TokenType.Int, TokenType.Semicolon, TokenType.Eof },
            Types("  // comentario\n\tlet a = 1; // otro\n"));
    }

    [Fact]
    public void RegistraLineaYColumna()
    {
        var tokens = Tokenize("let x = 1;\n  x");

        Assert.Equal((1, 1), (tokens[0].Line, tokens[0].Column)); // let
        Assert.Equal((1, 5), (tokens[1].Line, tokens[1].Column)); // x
        Assert.Equal((2, 3), (tokens[5].Line, tokens[5].Column)); // x de la línea 2
    }

    // ----- Entradas con errores léxicos -----

    [Fact]
    public void CaracterDesconocidoEsIlegal()
    {
        var tokens = Tokenize("let x = @;");

        Assert.Equal(TokenType.Illegal, tokens[3].Type);
        Assert.Equal("@", tokens[3].Literal);
        Assert.Equal(9, tokens[3].Column);
    }

    [Fact]
    public void CadenaSinCerrarEsIlegal()
    {
        var token = Tokenize("\"sin cerrar")[0];

        Assert.Equal(TokenType.Illegal, token.Type);
        Assert.Equal("\"sin cerrar", token.Literal);
    }

    [Fact]
    public void EntradaVaciaSoloDevuelveEof()
    {
        Assert.Equal(new[] { TokenType.Eof }, Types(""));
    }
}