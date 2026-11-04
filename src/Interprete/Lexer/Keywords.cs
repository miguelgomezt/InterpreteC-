namespace Interprete.Lexing;

//Palabras reservadas. Se leen como identificadores y luego se leen aquí. 

public static class Keywords
{
    private static readonly Dictionary<string, TokenType> Map = new()
    {
        ["function"] = TokenType.Function,
        ["let"] = TokenType.Let,
        ["true"] = TokenType.True,
        ["false"] = TokenType.False, 
        ["if"] = TokenType.If,
        ["elif"] = TokenType.Elif,
        ["else"] = TokenType.Else,
        ["return"] = TokenType.Return, 
        ["for"] = TokenType.For,
        ["while"] = TokenType.While,
    };

    public static TokenType Lookup(string ident) =>
    Map.TryGetValue(ident, out var type) ? type : TokenType.Ident;
}